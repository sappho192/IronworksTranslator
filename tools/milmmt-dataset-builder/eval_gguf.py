#!/usr/bin/env python3
"""Evaluate any MiLMMT GGUF model against eval.jsonl using IronworksTranslator's exact decoding params.

Computes:
  - Term accuracy (without glossary vs with glossary)
  - Distractor false-insertion rate
  - Target-language drift rate (kana leak in Korean output)
  - Repetition loop rate (runaway repeated chars)
  - Game tag preservation rate ([HQ], ×N, 『』, G.A.T.E.)
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import time
import urllib.request
from pathlib import Path
from typing import Any

from build_sft_dataset import render_prompt

RE_KANA = re.compile(r"[\u3040-\u309F\u30A0-\u30FF]")
RE_LOOP = re.compile(r"(.{1,4})\1{7,}")
RE_TAGS = re.compile(r"(?:\[HQ\]|×\d+|G\.A\.T\.E\.)")


def query_completion(port: int, prompt: str, max_tokens: int) -> tuple[str, int]:
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
            "stop": ["<end_of_turn>", "<eos>", "</s>"],
            "cache_prompt": False,
        }
    ).encode("utf-8")
    req = urllib.request.Request(
        f"http://127.0.0.1:{port}/completion",
        data=body,
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        data = json.loads(resp.read().decode("utf-8"))
    return str(data.get("content", "")).strip(), int(data.get("tokens_predicted", 0))


def main() -> int:
    parser = argparse.ArgumentParser(description="Evaluate a MiLMMT GGUF model on eval.jsonl.")
    parser.add_argument("--server-bin", type=Path, default=Path(r"D:\REPO\llama.cpp\build\bin\Release\llama-server.exe"))
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--eval-jsonl", type=Path, required=True)
    parser.add_argument("--port", type=int, default=18931)
    parser.add_argument("--with-glossary", action="store_true", help="Inject eval item glossary_terms into prompt.")
    parser.add_argument("--out-json", type=Path)
    args = parser.parse_args()

    records = [json.loads(l) for l in args.eval_jsonl.read_text(encoding="utf-8").splitlines() if l.strip()]
    srv = subprocess.Popen(
        [str(args.server_bin), "-m", str(args.model), "--host", "127.0.0.1", "--port", str(args.port), "-c", "2048", "-ngl", "99", "--log-disable"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        for _ in range(80):
            try:
                with urllib.request.urlopen(f"http://127.0.0.1:{args.port}/health", timeout=1) as resp:
                    if resp.status == 200:
                        break
            except Exception:
                time.sleep(0.25)

        term_total = 0
        term_hits = 0
        drift_count = 0
        loop_count = 0
        tag_total = 0
        tag_hits = 0
        results: list[dict[str, Any]] = []

        for r in records:
            gl = [(g["source"], g["target"]) for g in r.get("glossary_terms", [])] if args.with_glossary else []
            prompt = render_prompt(r["source_language"], r["target_language"], r["source_text"], gl)
            max_tok = 256 if r.get("translation_kind") == "dialogue" else 64
            out, toks = query_completion(args.port, prompt, max_tok)

            out_nospace = out.replace(" ", "")
            item_term_ok = []
            for g in r.get("glossary_terms", []):
                term_total += 1
                tgt_ns = g["target"].replace(" ", "")
                # Require end-of-word or common Korean particle/punctuation after target
                # so e.g. '골드소서' does not false-match inside '골드소서러'.
                hit = bool(
                    re.search(
                        re.escape(tgt_ns) + r"(?:$|[\s.,!?'\"‘’“”()\[\]에의를을은는이가로와과도만부])",
                        out_nospace,
                    )
                )
                if hit:
                    term_hits += 1
                item_term_ok.append({"source": g["source"], "target": g["target"], "hit": hit})

            has_drift = r["target_language"] == "Korean" and bool(RE_KANA.search(out))
            has_loop = bool(RE_LOOP.search(out))
            if has_drift:
                drift_count += 1
            if has_loop:
                loop_count += 1

            for m in RE_TAGS.finditer(r["source_text"]):
                tag_total += 1
                if m.group(0) in out:
                    tag_hits += 1

            results.append(
                {
                    "id": r["id"],
                    "source_text": r["source_text"],
                    "reference": r["reference_translation"],
                    "output": out,
                    "tokens": toks,
                    "term_checks": item_term_ok,
                    "has_drift": has_drift,
                    "has_loop": has_loop,
                }
            )

        summary = {
            "model": args.model.name,
            "with_glossary": args.with_glossary,
            "eval_count": len(records),
            "term_accuracy": round(term_hits / term_total, 4) if term_total else None,
            "term_hits": f"{term_hits}/{term_total}",
            "language_drift_count": drift_count,
            "repetition_loop_count": loop_count,
            "tag_preservation": f"{tag_hits}/{tag_total}",
            "items": results,
        }
        print(
            f"Eval {args.model.name} (with_glossary={args.with_glossary}): "
            f"term={summary['term_hits']} ({summary['term_accuracy']}), "
            f"drift={drift_count}, loops={loop_count}, tags={summary['tag_preservation']}"
        )
        if args.out_json:
            args.out_json.parent.mkdir(parents=True, exist_ok=True)
            args.out_json.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        return 0
    finally:
        srv.terminate()
        try:
            srv.wait(timeout=5)
        except Exception:
            srv.kill()


if __name__ == "__main__":
    raise SystemExit(main())
