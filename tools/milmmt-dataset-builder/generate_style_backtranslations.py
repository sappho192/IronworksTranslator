#!/usr/bin/env python3
"""Generate 1:1 semantically aligned EN/DE/FR source sentences via local on-policy back-translation
of official human-localized Korean FFXIV dialogue (using base MiLMMT-46-4B-v1.0 GGUF).

Why:
  FFXIV's official English quest script is heavily transcreated from Japanese, whereas the Korean
  script is translated faithfully from Japanese. Training `Style:` on raw EN->KO quest pairs
  teaches paraphrase drift/hallucination. Back-translating official KR dialogue (`KO -> EN/DE/FR`)
  keeps 100% authentic human Square Enix Korean targets while guaranteeing 1:1 semantic alignment
  with neutral English/German/French inputs.
"""

from __future__ import annotations

import argparse
import csv
import json
import random
import re
import subprocess
import sys
import time
import urllib.request
from collections import Counter, defaultdict
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

_THIS_DIR = Path(__file__).resolve().parent
_GLOSSARY_DIR = _THIS_DIR.parent / "milmmt-glossary-builder"
for _p in (str(_THIS_DIR), str(_GLOSSARY_DIR)):
    if _p not in sys.path:
        sys.path.insert(0, _p)

from build_sft_dataset import (  # noqa: E402
    LANG_CODE_TO_NAME,
    clean_dialogue_text,
    is_valid_parallel,
    render_prompt,
    resolve_lang_csv_dir,
)
from eval_gguf import RE_KANA, RE_LOOP  # noqa: E402
from korean_style import KOREAN_SPEECH_STYLES, classify_korean_sentence_style  # noqa: E402

RE_HANGUL = re.compile(r"[\uAC00-\uD7A3]")
RE_LATIN = re.compile(r"[A-Za-zÀ-ÖØ-öø-ÿ]")


def collect_official_ko_by_style(
    datamining_dir: Path,
    seed: int,
    max_per_style: int = 1200,
) -> dict[str, list[tuple[str, str]]]:
    """Collect unique clean official KR FFXIV dialogue sentences per Korean speech style."""
    ko_dir = resolve_lang_csv_dir(datamining_dir, "ko")
    files: list[Path] = []
    for sub in ("quest", "cut_scene", "custom"):
        sub_dir = ko_dir / sub
        if sub_dir.is_dir():
            files.extend(sorted(sub_dir.rglob("*.csv")))
    rng = random.Random(seed)
    rng.shuffle(files)

    buckets: dict[str, list[tuple[str, str]]] = defaultdict(list)
    seen_ko: set[str] = set()
    rare_styles = {"애교 존댓말", "고어체", "하오체", "하게체"}

    for fp in files:
        if all(len(buckets[st]) >= max_per_style for st in KOREAN_SPEECH_STYLES):
            break
        origin = f"bt_{fp.relative_to(ko_dir).parts[0]}"
        with fp.open("r", encoding="utf-8", errors="replace", newline="") as handle:
            reader = csv.reader(handle)
            hdr = next(reader, [])
            ci = hdr.index("Text") if "Text" in hdr else (2 if len(hdr) > 2 else 1)
            for row in reader:
                if not row or len(row) <= ci or not row[ci]:
                    continue
                if len(row) > 1 and ("_SEQ_" in row[1] or "_TODO_" in row[1]):
                    continue
                ck = clean_dialogue_text(row[ci])
                if not ck or len(ck) < 6 or len(ck) > 220 or ck in seen_ko:
                    continue
                st = classify_korean_sentence_style(ck)
                if not st:
                    continue
                if st not in rare_styles and len(buckets[st]) >= max_per_style:
                    continue
                seen_ko.add(ck)
                buckets[st].append((ck, origin))

    for st in KOREAN_SPEECH_STYLES:
        rng.shuffle(buckets[st])
        buckets[st] = buckets[st][:max_per_style]
    return buckets


def query_bt(port: int, prompt: str, max_tokens: int = 128) -> str:
    body = json.dumps(
        {
            "prompt": prompt,
            "n_predict": max_tokens,
            "temperature": 0.0,
            "top_k": 1,
            "top_p": 1.0,
            "min_p": 0.0,
            "repeat_penalty": 1.0,
            "frequency_penalty": 0.30,
            "presence_penalty": 0.0,
            "repeat_last_n": 16,
            "seed": 1,
            "stop": ["<end_of_turn>", "<eos>", "</s>", "\n"],
            "cache_prompt": False,
        }
    ).encode("utf-8")
    req = urllib.request.Request(
        f"http://127.0.0.1:{port}/completion",
        data=body,
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=60) as resp:
        data = json.loads(resp.read().decode("utf-8"))
    return str(data.get("content", "")).strip()


def main() -> int:
    parser = argparse.ArgumentParser(description="Back-translate official KR FFXIV dialogue to EN/DE/FR using local MiLMMT-46-4B-v1.0.")
    parser.add_argument("--datamining-dir", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True, help="Official base MiLMMT-46-4B-v1.0 GGUF.")
    parser.add_argument("--server-bin", type=Path, default=Path(r"D:\REPO\llama.cpp\build\bin\Release\llama-server.exe"))
    parser.add_argument("--out-jsonl", type=Path, required=True)
    parser.add_argument("--en-per-style", type=int, default=750)
    parser.add_argument("--de-per-style", type=int, default=160)
    parser.add_argument("--fr-per-style", type=int, default=160)
    parser.add_argument("--parallel", type=int, default=8)
    parser.add_argument("--port", type=int, default=18955)
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    t0 = time.time()
    max_needed = max(args.en_per_style, args.de_per_style, args.fr_per_style) + 120
    buckets = collect_official_ko_by_style(args.datamining_dir, seed=args.seed, max_per_style=max_needed)
    print("Collected official KR dialogue per style:", {st: len(buckets[st]) for st in KOREAN_SPEECH_STYLES})

    jobs: list[tuple[str, str, str, str]] = []  # (target_src_lang, style, ko_text, origin)
    for st in KOREAN_SPEECH_STYLES:
        pool = buckets[st]
        if not pool:
            continue
        for ko_text, orig in pool[: args.en_per_style]:
            jobs.append(("en", st, ko_text, orig))
        # Take offset slices for DE and FR (wrapping cleanly if pool < offset, e.g. 애교 존댓말 ~750)
        n = len(pool)
        for i in range(min(args.de_per_style, n)):
            ko_text, orig = pool[(args.en_per_style + i) % n]
            jobs.append(("de", st, ko_text, orig))
        for i in range(min(args.fr_per_style, n)):
            ko_text, orig = pool[(args.en_per_style + args.de_per_style + i) % n]
            jobs.append(("fr", st, ko_text, orig))

    print(f"Total back-translation jobs: {len(jobs):,} (parallel={args.parallel})")

    srv = subprocess.Popen(
        [
            str(args.server_bin),
            "-m",
            str(args.model),
            "--host",
            "127.0.0.1",
            "--port",
            str(args.port),
            "-c",
            str(512 * args.parallel),
            "-np",
            str(args.parallel),
            "-ngl",
            "99",
            "--log-disable",
        ],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        ready = False
        for _ in range(120):
            try:
                with urllib.request.urlopen(f"http://127.0.0.1:{args.port}/health", timeout=1) as resp:
                    if resp.status == 200:
                        ready = True
                        break
            except Exception:
                time.sleep(0.25)
        if not ready:
            raise RuntimeError("llama-server did not become healthy in time")

        def run_one(job: tuple[str, str, str, str]) -> dict | None:
            sl, st, ko_text, orig = job
            prompt = render_prompt("ko", sl, ko_text)
            bt = query_bt(args.port, prompt, max_tokens=140)
            bt = re.sub(r"\s+", " ", bt).strip()
            if not bt or len(bt) < 6 or len(bt) > 240:
                return None
            if RE_HANGUL.search(bt) or RE_KANA.search(bt) or not RE_LATIN.search(bt) or RE_LOOP.search(bt):
                return None
            if not is_valid_parallel(sl, "ko", bt, ko_text):
                return None
            return {
                "source_language": LANG_CODE_TO_NAME[sl],
                "target_language": "Korean",
                "source_text": bt,
                "target_text": ko_text,
                "style": st,
                "origin": orig,
            }

        results: list[dict] = []
        done = 0
        with ThreadPoolExecutor(max_workers=args.parallel) as ex:
            futs = [ex.submit(run_one, j) for j in jobs]
            for fut in as_completed(futs):
                done += 1
                rec = fut.result()
                if rec is not None:
                    results.append(rec)
                if done % 1000 == 0 or done == len(jobs):
                    elapsed = time.time() - t0
                    rate = done / max(elapsed, 0.1)
                    print(f"  progress: {done:,}/{len(jobs):,} kept={len(results):,} ({rate:.1f} it/s, {elapsed:.1f}s)", flush=True)

        args.out_jsonl.parent.mkdir(parents=True, exist_ok=True)
        args.out_jsonl.write_text(
            "\n".join(json.dumps(r, ensure_ascii=False) for r in results) + "\n",
            encoding="utf-8",
        )
        by_lang = Counter(r["source_language"] for r in results)
        by_style = Counter(r["style"] for r in results)
        print(
            f"Wrote {len(results):,} back-translated pairs -> {args.out_jsonl} in {time.time() - t0:.1f}s\n"
            f"  by_lang={dict(by_lang)}\n"
            f"  by_style={dict(by_style)}"
        )
        return 0
    finally:
        srv.terminate()
        try:
            srv.wait(timeout=5)
        except Exception:
            srv.kill()


if __name__ == "__main__":
    raise SystemExit(main())
