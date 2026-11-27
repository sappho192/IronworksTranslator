#!/usr/bin/env python3
"""Generate Axis-3 general on-policy replay data (replay.jsonl) on Colab Pro+.

Runs on Colab (A100/L4) with `transformers` + `datasets` + `torch` to translate clean
general-domain source sentences across the 6 asymmetric Ironworks language pairs using
`xiaomi-research/MiLMMT-46-12B-v1.0` (or `4B-v1.0` / `1B-v1.0`), preserving v1.0's
general translation distribution without noisy external parallel corpora.

Example on Colab Pro+:
  pip install -q transformers datasets accelerate
  python generate_replay.py \
    --model xiaomi-research/MiLMMT-46-12B-v1.0 \
    --total-samples 4500 \
    --out-jsonl /content/drive/MyDrive/ironworks-milmmt/replay_general.jsonl
"""

from __future__ import annotations

import argparse
import json
import random
from pathlib import Path
from typing import Sequence

PAIR_WEIGHTS: dict[tuple[str, str], float] = {
    ("Japanese", "Korean"): 0.50,
    ("English", "Korean"): 0.30,
    ("German", "Korean"): 0.05,
    ("French", "Korean"): 0.05,
    ("Korean", "Japanese"): 0.05,
    ("Korean", "English"): 0.05,
}

FLORES_LANG_CODE: dict[str, str] = {
    "Japanese": "jpn_Jpan",
    "English": "eng_Latn",
    "German": "deu_Latn",
    "French": "fra_Latn",
    "Korean": "kor_Hang",
}


def render_plain_prompt(src_lang: str, dst_lang: str, text: str) -> str:
    return f"Translate this from {src_lang} to {dst_lang}:\n{src_lang}: {text.strip()}\n{dst_lang}:"


def load_source_sentences(lang_name: str, count: int, seed: int) -> list[str]:
    """Load clean general-domain source sentences from ungated public HF datasets (dev/train only)."""
    from datasets import load_dataset  # type: ignore

    rng = random.Random(seed)
    code = FLORES_LANG_CODE[lang_name]
    iso2_map = {"Japanese": "ja", "English": "en", "German": "de", "French": "fr", "Korean": "ko"}
    iso2 = iso2_map[lang_name]
    texts: list[str] = []
    seen: set[str] = set()

    def add_text(s: str) -> None:
        s = s.strip()
        if 8 <= len(s) <= 220 and "\n" not in s and s not in seen:
            seen.add(s)
            texts.append(s)

    # 1. Try ungated FLORES-200 dev split (devtest strictly reserved for final regression eval!)
    for ds_id, cfg, col in (
        ("Muennighoff/flores200", code, "sentence"),
        ("gsarti/flores_101", {"Japanese": "jpn", "English": "eng", "German": "deu", "French": "fra", "Korean": "kor"}[lang_name], "sentence"),
    ):
        if len(texts) >= count:
            break
        try:
            ds = load_dataset(ds_id, cfg, split="dev")
            for row in ds:
                add_text(str(row.get(col) or row.get("text") or ""))
        except Exception as exc:
            print(f"[info] Skipping {ds_id}/{cfg}: {exc}")

    # 2. Supplement from Helsinki-NLP/opus-100 (ungated parquet) to reach requested quota
    if len(texts) < count:
        pair_cfg = "de-en" if iso2 == "de" else (f"en-{iso2}" if iso2 != "en" else "en-ja")
        try:
            ds = load_dataset("Helsinki-NLP/opus-100", pair_cfg, split="train", streaming=True)
            for row in ds:
                tr = row.get("translation", {})
                add_text(str(tr.get(iso2, "")))
                if len(texts) >= count * 2:
                    break
        except Exception as exc:
            print(f"[warn] Could not supplement {lang_name} from opus-100 ({pair_cfg}): {exc}")

    rng.shuffle(texts)
    return texts[:count]


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate on-policy general replay JSONL on Colab Pro+.")
    parser.add_argument("--model", default="xiaomi-research/MiLMMT-46-12B-v1.0")
    parser.add_argument("--total-samples", type=int, default=4500)
    parser.add_argument("--batch-size", type=int, default=16)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--out-jsonl", type=Path, required=True)
    args = parser.parse_args(argv)

    import torch  # type: ignore
    from transformers import AutoModelForCausalLM, AutoTokenizer  # type: ignore

    tokenizer = AutoTokenizer.from_pretrained(args.model, trust_remote_code=True)
    tokenizer.padding_side = "left"
    if tokenizer.pad_token_id is None:
        tokenizer.pad_token_id = tokenizer.eos_token_id

    model = AutoModelForCausalLM.from_pretrained(
        args.model,
        dtype=torch.bfloat16,
        device_map="auto",
        trust_remote_code=True,
    )
    model.eval()

    args.out_jsonl.parent.mkdir(parents=True, exist_ok=True)
    written = 0
    with args.out_jsonl.open("w", encoding="utf-8", newline="\n") as out_f:
        for (src_lang, dst_lang), weight in PAIR_WEIGHTS.items():
            quota = max(50, int(args.total_samples * weight))
            sources = load_source_sentences(src_lang, quota, seed=args.seed)
            print(f"Generating {len(sources)} replay pairs for {src_lang} -> {dst_lang}...", flush=True)

            for i in range(0, len(sources), args.batch_size):
                batch_src = sources[i : i + args.batch_size]
                prompts = [render_plain_prompt(src_lang, dst_lang, s) for s in batch_src]
                enc = tokenizer(
                    prompts,
                    return_tensors="pt",
                    padding=True,
                    add_special_tokens=False,
                ).to(model.device)
                input_len = enc["input_ids"].shape[1]
                with torch.no_grad():
                    gen = model.generate(
                        **enc,
                        max_new_tokens=160,
                        do_sample=False,
                        eos_token_id=tokenizer.eos_token_id,
                        pad_token_id=tokenizer.pad_token_id,
                    )
                decoded = tokenizer.batch_decode(gen[:, input_len:], skip_special_tokens=True)
                for s, tgt in zip(batch_src, decoded):
                    tgt_clean = tgt.strip()
                    if not tgt_clean:
                        continue
                    rec = {
                        "source_language": src_lang,
                        "target_language": dst_lang,
                        "source_text": s,
                        "target_text": tgt_clean,
                        "origin": f"replay:{args.model.split('/')[-1]}",
                    }
                    out_f.write(json.dumps(rec, ensure_ascii=False) + "\n")
                    written += 1
                if (i // args.batch_size + 1) % 10 == 0 or (i + args.batch_size) >= len(sources):
                    out_f.flush()
                    print(f"  {src_lang}->{dst_lang}: {min(i + args.batch_size, len(sources))}/{len(sources)} (total={written})", flush=True)

    print(f"Wrote {written:,} general replay records -> {args.out_jsonl}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
