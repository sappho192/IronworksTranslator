#!/usr/bin/env python3
"""On-Policy CPO-SimPO LoRA trainer for MiLMMT-46 (Gemma 3) with KL-Anchor Regularization.

Loss:
  - Preference pair (x, y_w, y_l):
      L = -log sigmoid(beta * (mean_lp(y_w|x) - mean_lp(y_l|x) - gamma)) - lambda_nll * mean_lp(y_w|x)
  - Optional KL-Anchor sample (x, y_anchor):
      L = -lambda_anchor * mean_lp(y_anchor|x)

Accepted JSONL schemas for --cpo-jsonl:
  1. Pre-rendered prompt: {"prompt": "...", "chosen": "...", "rejected": "..."}
     (also accepts "instruction" in place of "prompt").
  2. curate_reports.py `cpo_user.jsonl`:
     {"source_language": "en", "target_language": "ko", "source_text": "...",
      "chosen": "...", "rejected": "...", "glossary_terms": [...], "style": "..."}

Strict tokenizer contract:
  - add_special_tokens=False (no <bos>)
  - Target tokens = encode(target, add_special_tokens=False) + [eos_token_id]
  - Prompt tokens masked with -100; log-probabilities averaged strictly over target + <eos>.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import random
import time
from pathlib import Path
from typing import Any

# Configure local HF cache BEFORE importing transformers/huggingface_hub.
if "HF_HOME" not in os.environ:
    default_cache = Path("artifacts/hf_cache")
    if default_cache.exists():
        os.environ["HF_HOME"] = str(default_cache.resolve())

LANG_CODE_TO_NAME: dict[str, str] = {
    "en": "English",
    "ja": "Japanese",
    "ko": "Korean",
    "de": "German",
    "fr": "French",
    "zh": "Chinese",
}


def normalize_lang_name(lang: str) -> str:
    raw = str(lang).strip()
    return LANG_CODE_TO_NAME.get(raw.lower(), raw)


def render_cpo_prompt_from_fields(row: dict[str, Any]) -> str:
    sl = normalize_lang_name(row["source_language"])
    dl = normalize_lang_name(row["target_language"])
    src = str(row["source_text"]).strip()
    blocks: list[str] = []

    style = str(row.get("style") or "").strip()
    if style:
        blocks.append(f"Style: {style}")

    glossary_lines: list[str] = []
    for item in row.get("glossary_terms") or []:
        if isinstance(item, dict):
            s = str(item.get("source") or item.get("src") or "").strip()
            t = str(item.get("target") or item.get("dst") or "").strip()
            if s and t:
                glossary_lines.append(f"{s} => {t}")
        elif isinstance(item, (list, tuple)) and len(item) >= 2:
            s, t = str(item[0]).strip(), str(item[1]).strip()
            if s and t:
                glossary_lines.append(f"{s} => {t}")
    if glossary_lines:
        blocks.append("Glossary:\n" + "\n".join(glossary_lines))

    body = f"Translate this from {sl} to {dl}:\n{sl}: {src}\n{dl}:"
    if blocks:
        return "\n".join(blocks) + "\n\n" + body
    return body


def extract_cpo_record(row: dict[str, Any]) -> tuple[str, str, str] | None:
    """Return (prompt, chosen, rejected) from either pre-rendered or curate_reports cpo_user schema."""
    chosen = str(row.get("chosen") or "").strip()
    rejected = str(row.get("rejected") or "").strip()
    if not chosen or not rejected or chosen == rejected:
        return None
    prompt = str(row.get("prompt") or row.get("instruction") or "").strip()
    if not prompt:
        if all(k in row for k in ("source_language", "target_language", "source_text")):
            prompt = render_cpo_prompt_from_fields(row)
        else:
            return None
    return prompt, chosen, rejected


def extract_anchor_record(row: dict[str, Any]) -> tuple[str, str] | None:
    """Return (prompt, target) from an NLL KL-anchor record."""
    target = str(row.get("chosen") or row.get("output") or row.get("target_text") or "").strip()
    if not target:
        return None
    prompt = str(row.get("prompt") or row.get("instruction") or "").strip()
    if not prompt:
        if all(k in row for k in ("source_language", "target_language", "source_text")):
            prompt = render_cpo_prompt_from_fields(row)
        else:
            return None
    return prompt, target


def load_jsonl(path: Path) -> list[dict[str, Any]]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def encode_pair(tokenizer: Any, prompt: str, target: str, max_len: int) -> tuple[list[int], list[int]] | None:
    eos_id = tokenizer.eos_token_id
    assert eos_id is not None, "Tokenizer eos_token_id must not be None"
    p_ids = tokenizer.encode(prompt, add_special_tokens=False)
    t_ids = tokenizer.encode(target, add_special_tokens=False) + [eos_id]
    ids = p_ids + t_ids
    if len(ids) > max_len or len(p_ids) == 0 or len(t_ids) < 2:
        return None
    labels = [-100] * len(p_ids) + t_ids
    return ids, labels


def pad_sequences(
    seqs: list[tuple[list[int], list[int]]],
    pad_id: int,
) -> tuple[list[list[int]], list[list[int]], list[list[int]]]:
    max_l = max(len(x[0]) for x in seqs)
    max_l = ((max_l + 7) // 8) * 8
    ids_b, mask_b, lab_b = [], [], []
    for ids, labs in seqs:
        pad_n = max_l - len(ids)
        ids_b.append(ids + [pad_id] * pad_n)
        mask_b.append([1] * len(ids) + [0] * pad_n)
        lab_b.append(labs + [-100] * pad_n)
    return ids_b, mask_b, lab_b


def main() -> int:
    parser = argparse.ArgumentParser(description="Train On-Policy CPO-SimPO LoRA on MiLMMT-46.")
    parser.add_argument("--base-model", required=True, help="Local merged bf16 model directory or HF model ID.")
    parser.add_argument(
        "--cpo-jsonl",
        type=Path,
        action="append",
        required=True,
        help="Preference JSONL path (repeatable; accepts pre-rendered or curate_reports cpo_user.jsonl).",
    )
    parser.add_argument(
        "--anchor-jsonl",
        type=Path,
        action="append",
        default=[],
        help="Optional NLL KL-anchor JSONL path (repeatable; locks general/glossary distribution).",
    )
    parser.add_argument("--out-dir", type=Path, required=True, help="Output directory for adapter, merged bf16, and summary.")
    parser.add_argument("--epochs", type=float, default=3.0)
    parser.add_argument("--lr", type=float, default=2e-5)
    parser.add_argument("--grad-accum", type=int, default=32)
    parser.add_argument("--beta", type=float, default=2.0)
    parser.add_argument("--gamma", type=float, default=0.5)
    parser.add_argument("--lambda-nll", type=float, default=0.5)
    parser.add_argument("--lambda-anchor", type=float, default=1.0)
    parser.add_argument("--lora-r", type=int, default=8)
    parser.add_argument("--lora-alpha", type=int, default=16)
    parser.add_argument("--lora-dropout", type=float, default=0.05)
    parser.add_argument("--max-len", type=int, default=384)
    parser.add_argument("--vram-fraction", type=float, default=0.82 if os.name == "nt" else 0.0)
    parser.add_argument("--online", action="store_true", help="Allow Hugging Face Hub network lookups.")
    parser.add_argument("--skip-merge", action="store_true", help="Save only PEFT adapter without writing full merged bf16.")
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    local_only = not args.online
    if local_only:
        os.environ.setdefault("HF_HUB_OFFLINE", "1")

    import torch
    import torch.nn.functional as F
    from peft import LoraConfig, get_peft_model
    from transformers import AutoModelForCausalLM, AutoTokenizer, get_cosine_schedule_with_warmup

    def mean_log_prob(model: Any, input_ids: torch.Tensor, attention_mask: torch.Tensor, labels: torch.Tensor) -> torch.Tensor:
        out = model(input_ids=input_ids, attention_mask=attention_mask, use_cache=False)
        logits = out.logits[:, :-1, :].float()
        shift_labels = labels[:, 1:]
        mask = shift_labels != -100
        safe_labels = shift_labels.clamp(min=0)
        log_probs = F.log_softmax(logits, dim=-1)
        token_lp = log_probs.gather(dim=-1, index=safe_labels.unsqueeze(-1)).squeeze(-1)
        token_lp = token_lp * mask.float()
        denom = mask.float().sum(dim=-1).clamp(min=1.0)
        return token_lp.sum(dim=-1) / denom

    def to_batch(seqs: list[tuple[list[int], list[int]]], pad_id: int, device: torch.device):
        ids_b, mask_b, lab_b = pad_sequences(seqs, pad_id)
        return (
            torch.tensor(ids_b, dtype=torch.long, device=device),
            torch.tensor(mask_b, dtype=torch.long, device=device),
            torch.tensor(lab_b, dtype=torch.long, device=device),
        )

    t0 = time.time()
    random.seed(args.seed)
    torch.manual_seed(args.seed)
    assert torch.cuda.is_available(), "CUDA GPU is required"
    if args.vram_fraction and 0.1 <= args.vram_fraction <= 0.95:
        torch.cuda.set_per_process_memory_fraction(args.vram_fraction, 0)

    tokenizer = AutoTokenizer.from_pretrained(args.base_model, trust_remote_code=True, local_files_only=local_only)
    if tokenizer.pad_token_id is None:
        tokenizer.pad_token_id = tokenizer.eos_token_id
    pad_id = tokenizer.pad_token_id

    # Verify tokenizer contract upfront
    probe_ids = tokenizer.encode("Translate this from Japanese to Korean:\nJapanese: こんにちは\nKorean:", add_special_tokens=False)
    assert probe_ids[0] != tokenizer.bos_token_id, f"Unexpected BOS token in add_special_tokens=False: {probe_ids[:3]}"

    items: list[dict[str, Any]] = []
    skipped = 0
    for cpo_path in args.cpo_jsonl:
        for row in load_jsonl(cpo_path):
            rec = extract_cpo_record(row)
            if rec is None:
                skipped += 1
                continue
            prompt, chosen, rejected = rec
            w = encode_pair(tokenizer, prompt, chosen, args.max_len)
            l = encode_pair(tokenizer, prompt, rejected, args.max_len)
            if w is None or l is None:
                skipped += 1
                continue
            items.append({"kind": "cpo", "w": w, "l": l})

    for anc_path in args.anchor_jsonl:
        for row in load_jsonl(anc_path):
            rec = extract_anchor_record(row)
            if rec is None:
                skipped += 1
                continue
            prompt, target = rec
            a = encode_pair(tokenizer, prompt, target, args.max_len)
            if a is None:
                skipped += 1
                continue
            items.append({"kind": "anchor", "a": a})

    n_cpo = sum(1 for x in items if x["kind"] == "cpo")
    n_anc = sum(1 for x in items if x["kind"] == "anchor")
    assert n_cpo > 0, "No valid CPO preference pairs loaded"
    print(f"Loaded {len(items)} items (cpo={n_cpo}, anchor={n_anc}, skipped={skipped})")

    base_model = AutoModelForCausalLM.from_pretrained(
        args.base_model,
        dtype=torch.bfloat16,
        device_map="auto",
        attn_implementation="sdpa",
        trust_remote_code=True,
        local_files_only=local_only,
    )
    base_model.config.use_cache = False
    base_model.gradient_checkpointing_enable(gradient_checkpointing_kwargs={"use_reentrant": False})
    base_model.enable_input_require_grads()

    exclude_modules = (
        ["vision_tower", "multi_modal_projector"]
        if hasattr(base_model, "vision_tower") or hasattr(getattr(base_model, "model", None), "vision_tower")
        else None
    )
    lora_cfg = LoraConfig(
        r=args.lora_r,
        lora_alpha=args.lora_alpha,
        lora_dropout=args.lora_dropout,
        bias="none",
        task_type="CAUSAL_LM",
        target_modules=["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"],
        exclude_modules=exclude_modules,
    )
    model = get_peft_model(base_model, lora_cfg)
    model.train()
    device = next(model.parameters()).device

    trainable = sum(p.numel() for p in model.parameters() if p.requires_grad)
    total_params = sum(p.numel() for p in model.parameters())
    print(f"Trainable params: {trainable:,} / {total_params:,} ({trainable / total_params * 100:.2f}%)")

    schedule: list[dict[str, Any]] = []
    full_epochs = int(args.epochs)
    frac_epoch = args.epochs - full_epochs
    rng = random.Random(args.seed)
    for _ in range(full_epochs):
        ep = list(items)
        rng.shuffle(ep)
        schedule.extend(ep)
    if frac_epoch > 0:
        ep = list(items)
        rng.shuffle(ep)
        schedule.extend(ep[: int(len(ep) * frac_epoch)])

    total_opt_steps = max(1, math.ceil(len(schedule) / args.grad_accum))
    warmup_steps = max(5, int(total_opt_steps * 0.10))

    optimizer = torch.optim.AdamW(
        [p for p in model.parameters() if p.requires_grad],
        lr=args.lr,
        weight_decay=0.01,
    )
    scheduler = get_cosine_schedule_with_warmup(optimizer, warmup_steps, total_opt_steps)

    optimizer.zero_grad(set_to_none=True)
    opt_step = 0
    acc_loss = acc_pref = acc_nll = acc_anc = acc_margin = 0.0
    acc_pref_hits = acc_cpo_n = acc_anc_n = 0
    log_history: list[dict[str, Any]] = []

    for idx, ex in enumerate(schedule, start=1):
        if ex["kind"] == "cpo":
            ids, mask, labs = to_batch([ex["w"], ex["l"]], pad_id, device)
            lps = mean_log_prob(model, ids, mask, labs)
            lp_w, lp_l = lps[0], lps[1]
            margin = lp_w - lp_l
            pref_loss = -F.logsigmoid(args.beta * (margin - args.gamma))
            nll_loss = -lp_w
            loss = pref_loss + args.lambda_nll * nll_loss
            (loss / args.grad_accum).backward()

            acc_loss += float(loss.detach())
            acc_pref += float(pref_loss.detach())
            acc_nll += float(nll_loss.detach())
            acc_margin += float(margin.detach())
            acc_pref_hits += int(margin.detach().item() > 0)
            acc_cpo_n += 1
        else:
            ids, mask, labs = to_batch([ex["a"]], pad_id, device)
            lp_a = mean_log_prob(model, ids, mask, labs)[0]
            anc_loss = -lp_a * args.lambda_anchor
            (anc_loss / args.grad_accum).backward()

            acc_loss += float(anc_loss.detach())
            acc_anc += float(anc_loss.detach())
            acc_anc_n += 1

        if idx % args.grad_accum == 0 or idx == len(schedule):
            torch.nn.utils.clip_grad_norm_([p for p in model.parameters() if p.requires_grad], 1.0)
            optimizer.step()
            scheduler.step()
            optimizer.zero_grad(set_to_none=True)
            opt_step += 1

            if opt_step % 10 == 0 or opt_step == total_opt_steps or opt_step == 1:
                c_n = max(acc_cpo_n, 1)
                a_n = max(acc_anc_n, 1)
                rec = {
                    "step": opt_step,
                    "total_steps": total_opt_steps,
                    "lr": round(scheduler.get_last_lr()[0], 8),
                    "loss": round(acc_loss / max(acc_cpo_n + acc_anc_n, 1), 4),
                    "pref_loss": round(acc_pref / c_n, 4),
                    "nll_loss": round(acc_nll / c_n, 4),
                    "anchor_loss": round(acc_anc / a_n, 4) if acc_anc_n else 0.0,
                    "margin": round(acc_margin / c_n, 4),
                    "pref_acc": round(acc_pref_hits / c_n, 4),
                    "elapsed_s": round(time.time() - t0, 1),
                }
                log_history.append(rec)
                print(json.dumps(rec), flush=True)
                acc_loss = acc_pref = acc_nll = acc_anc = acc_margin = 0.0
                acc_pref_hits = acc_cpo_n = acc_anc_n = 0

    args.out_dir.mkdir(parents=True, exist_ok=True)
    adapter_dir = args.out_dir / "adapter"
    model.save_pretrained(str(adapter_dir))
    tokenizer.save_pretrained(str(adapter_dir))

    if not args.skip_merge:
        merged_dir = args.out_dir / "merged"
        merged = model.merge_and_unload()
        merged.save_pretrained(str(merged_dir), safe_serialization=True)
        tokenizer.save_pretrained(str(merged_dir))

    peak_vram = torch.cuda.max_memory_allocated()
    summary = {
        "base_model": str(args.base_model),
        "cpo_pairs": n_cpo,
        "anchor_rows": n_anc,
        "skipped": skipped,
        "epochs": args.epochs,
        "lr": args.lr,
        "beta": args.beta,
        "gamma": args.gamma,
        "lambda_nll": args.lambda_nll,
        "lambda_anchor": args.lambda_anchor,
        "lora_r": args.lora_r,
        "lora_alpha": args.lora_alpha,
        "opt_steps": opt_step,
        "peak_vram_bytes": peak_vram,
        "elapsed_seconds": round(time.time() - t0, 2),
        "log_history": log_history,
    }
    (args.out_dir / "cpo_summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(f"CPO-SimPO complete in {summary['elapsed_seconds']}s, peak VRAM={peak_vram / (1024**3):.2f} GiB -> {args.out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
