#!/usr/bin/env python3
"""Reproducible bf16 LoRA SFT script for MiLMMT-46-1B/4B-v1.0 on Colab Pro+.

Strict alignment with verified MiLMMT-46-1B-v1.0.Q8_0.gguf tokenizer contract:
  - add_special_tokens=False (no <bos>)
  - Prompt ends directly with `{TargetName}:`
  - Output starts immediately with target text (NO leading space) + <eos> (id=1)
  - Loss computed strictly on target + <eos> tokens (prompt tokens masked with -100)

Supports:
  --smoke-only : 5 optimizer steps, finite-loss check, adapter save/reload, sample inference.
  Full mode    : Full LoRA SFT + optional Axis-3 replay mix + adapter save + bf16 merge export.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import random
import time
from pathlib import Path
from typing import Any

import torch
from peft import LoraConfig, PeftModel, get_peft_model
from torch.utils.data import Dataset
from transformers import (
    AutoModelForCausalLM,
    AutoTokenizer,
    Trainer,
    TrainingArguments,
)


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(4 * 1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def load_jsonl(path: Path) -> list[dict[str, Any]]:
    rows = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line:
            rows.append(json.loads(line))
    return rows


def replay_to_sft_rows(replay_path: Path, max_rows: int | None = None, seed: int = 42) -> list[dict[str, str]]:
    raw = load_jsonl(replay_path)
    rng = random.Random(seed)
    rng.shuffle(raw)
    if max_rows is not None:
        raw = raw[:max_rows]
    out = []
    for r in raw:
        sl = r["source_language"]
        dl = r["target_language"]
        src = str(r["source_text"]).strip()
        tgt = str(r["target_text"]).strip()
        if not src or not tgt:
            continue
        prompt = f"Translate this from {sl} to {dl}:\n{sl}: {src}\n{dl}:"
        out.append({"instruction": prompt, "input": "", "output": tgt})
    return out


class MiLMMTSftDataset(Dataset):
    """Tokenizes each (instruction, output) pair with add_special_tokens=False and prompt masking."""

    def __init__(self, rows: list[dict[str, Any]], tokenizer: Any, max_len: int = 1024) -> None:
        self.examples: list[tuple[list[int], list[int]]] = []
        eos_id = tokenizer.eos_token_id
        assert eos_id is not None, "Tokenizer eos_token_id must not be None"

        skipped_long = 0
        for r in rows:
            prompt = r["instruction"]
            target = r["output"]
            prompt_ids = tokenizer.encode(prompt, add_special_tokens=False)
            target_ids = tokenizer.encode(target, add_special_tokens=False) + [eos_id]
            input_ids = prompt_ids + target_ids
            if len(input_ids) > max_len:
                skipped_long += 1
                continue
            labels = [-100] * len(prompt_ids) + target_ids
            self.examples.append((input_ids, labels))
        self.lengths = [len(inp) for inp, _ in self.examples]
        self.skipped_long = skipped_long

    def __len__(self) -> int:
        return len(self.examples)

    def __getitem__(self, idx: int) -> dict[str, list[int]]:
        input_ids, labels = self.examples[idx]
        return {
            "input_ids": input_ids,
            "attention_mask": [1] * len(input_ids),
            "labels": labels,
        }


class RightPadCollator:
    def __init__(self, pad_token_id: int) -> None:
        self.pad_token_id = pad_token_id

    def __call__(self, features: list[dict[str, list[int]]]) -> dict[str, torch.Tensor]:
        max_len = max(len(f["input_ids"]) for f in features)
        # Pad to multiple of 8 for tensor core efficiency
        max_len = ((max_len + 7) // 8) * 8
        input_ids = []
        attention_mask = []
        labels = []
        for f in features:
            pad_n = max_len - len(f["input_ids"])
            input_ids.append(f["input_ids"] + [self.pad_token_id] * pad_n)
            attention_mask.append(f["attention_mask"] + [0] * pad_n)
            labels.append(f["labels"] + [-100] * pad_n)
        return {
            "input_ids": torch.tensor(input_ids, dtype=torch.long),
            "attention_mask": torch.tensor(attention_mask, dtype=torch.long),
            "labels": torch.tensor(labels, dtype=torch.long),
        }


def run_quick_inference(model: Any, tokenizer: Any, prompts: list[str]) -> list[dict[str, str]]:
    model.eval()
    outputs = []
    for p in prompts:
        enc = tokenizer(p, return_tensors="pt", add_special_tokens=False).to(model.device)
        in_len = enc["input_ids"].shape[1]
        with torch.no_grad():
            gen = model.generate(
                **enc,
                max_new_tokens=64,
                do_sample=False,
                eos_token_id=tokenizer.eos_token_id,
                pad_token_id=tokenizer.pad_token_id,
            )
        text = tokenizer.decode(gen[0, in_len:], skip_special_tokens=True).strip()
        outputs.append({"prompt": p, "output": text})
    return outputs


def main() -> int:
    parser = argparse.ArgumentParser(description="Train MiLMMT-46-1B/4B LoRA SFT.")
    parser.add_argument("--model-id", default="xiaomi-research/MiLMMT-46-1B-v1.0")
    parser.add_argument("--train-jsonl", type=Path, required=True)
    parser.add_argument("--val-jsonl", type=Path, required=True)
    parser.add_argument("--replay-jsonl", type=Path, help="Optional Axis-3 general replay JSONL to mix into train.")
    parser.add_argument("--replay-max", type=int, default=4000)
    parser.add_argument("--out-dir", type=Path, required=True)
    parser.add_argument("--merged-dir", type=Path, help="Optional directory to save merged bf16 model.")
    parser.add_argument("--epochs", type=float, default=2.0)
    parser.add_argument("--lr", type=float, default=5e-5)
    parser.add_argument("--batch-size", type=int, default=8)
    parser.add_argument("--grad-accum", type=int, default=8)
    parser.add_argument("--lora-r", type=int, default=16)
    parser.add_argument("--lora-alpha", type=int, default=32)
    parser.add_argument("--lora-dropout", type=float, default=0.05)
    parser.add_argument("--max-len", type=int, default=1024)
    parser.add_argument("--gradient-checkpointing", action="store_true", help="Enable gradient checkpointing to cut VRAM.")
    parser.add_argument("--group-by-length", action="store_true", help="Group similar-length examples to reduce padding and VRAM spikes.")
    parser.add_argument("--attn-impl", default="eager", choices=["eager", "sdpa"], help="Attention implementation (eager or sdpa).")
    parser.add_argument("--vram-fraction", type=float, default=0.78 if os.name == "nt" else 0.0, help="Cap per-process CUDA VRAM fraction (0 disables; prevents Windows WDDM spillover).")
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--smoke-only", action="store_true", help="Run 5-step smoke test + save/reload check only.")
    args = parser.parse_args()

    t0 = time.time()
    torch.manual_seed(args.seed)
    random.seed(args.seed)

    assert torch.cuda.is_available(), "CUDA GPU is required"
    bf16_ok = torch.cuda.is_bf16_supported(including_emulation=False)
    assert bf16_ok, "Native BF16 is required for this recipe"
    if args.vram_fraction and 0.1 <= args.vram_fraction <= 0.95:
        torch.cuda.set_per_process_memory_fraction(args.vram_fraction, 0)

    tokenizer = AutoTokenizer.from_pretrained(args.model_id, trust_remote_code=True)
    if tokenizer.pad_token_id is None:
        tokenizer.pad_token_id = tokenizer.eos_token_id

    # Verify tokenizer contract upfront
    probe_prompt = "Translate this from Japanese to Korean:\nJapanese: こんにちは\nKorean:"
    probe_ids = tokenizer.encode(probe_prompt, add_special_tokens=False)
    assert probe_ids[0] != tokenizer.bos_token_id, f"Unexpected BOS token in add_special_tokens=False: {probe_ids[:3]}"

    train_rows = load_jsonl(args.train_jsonl)
    val_rows = load_jsonl(args.val_jsonl)
    replay_count = 0
    if args.replay_jsonl and args.replay_jsonl.exists():
        replay_rows = replay_to_sft_rows(args.replay_jsonl, max_rows=args.replay_max, seed=args.seed)
        replay_count = len(replay_rows)
        train_rows.extend(replay_rows)
        random.Random(args.seed).shuffle(train_rows)

    if args.smoke_only:
        train_rows = train_rows[:64]
        val_rows = val_rows[:16]

    train_ds = MiLMMTSftDataset(train_rows, tokenizer, max_len=args.max_len)
    val_ds = MiLMMTSftDataset(val_rows, tokenizer, max_len=args.max_len)

    # Verify prompt masking on example 0
    ex0 = train_ds[0]
    masked_count = sum(1 for x in ex0["labels"] if x == -100)
    supervised_count = len(ex0["labels"]) - masked_count
    assert masked_count > 0 and supervised_count >= 2, f"Bad label mask: masked={masked_count}, sup={supervised_count}"
    assert ex0["labels"][-1] == tokenizer.eos_token_id, "Target must end with eos_token_id"

    base_model = AutoModelForCausalLM.from_pretrained(
        args.model_id,
        dtype=torch.bfloat16,
        device_map="auto",
        attn_implementation=args.attn_impl,
        trust_remote_code=True,
    )
    base_model.config.use_cache = False
    if args.gradient_checkpointing:
        base_model.gradient_checkpointing_enable(gradient_checkpointing_kwargs={"use_reentrant": False})
        base_model.enable_input_require_grads()

    exclude_modules = ["vision_tower", "multi_modal_projector"] if hasattr(base_model, "vision_tower") or hasattr(getattr(base_model, "model", None), "vision_tower") else None
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
    trainable_params = sum(p.numel() for p in model.parameters() if p.requires_grad)
    total_params = sum(p.numel() for p in model.parameters())

    args.out_dir.mkdir(parents=True, exist_ok=True)
    adapter_dir = args.out_dir / "adapter"

    eff_bs = (args.batch_size if not args.smoke_only else 4) * (args.grad_accum if not args.smoke_only else 2)
    est_total_steps = 5 if args.smoke_only else max(1, int(math.ceil(len(train_ds) / eff_bs) * args.epochs))
    warmup_steps = 1 if args.smoke_only else max(10, int(est_total_steps * 0.05))

    train_args = TrainingArguments(
        output_dir=str(args.out_dir / "checkpoints"),
        per_device_train_batch_size=args.batch_size if not args.smoke_only else 4,
        gradient_accumulation_steps=args.grad_accum if not args.smoke_only else 2,
        per_device_eval_batch_size=args.batch_size if not args.smoke_only else 4,
        learning_rate=args.lr,
        lr_scheduler_type="cosine",
        warmup_steps=warmup_steps,
        num_train_epochs=args.epochs,
        max_steps=5 if args.smoke_only else -1,
        bf16=True,
        gradient_checkpointing=args.gradient_checkpointing,
        gradient_checkpointing_kwargs={"use_reentrant": False} if args.gradient_checkpointing else None,
        logging_steps=1 if args.smoke_only else 25,
        eval_strategy="steps",
        eval_steps=5 if args.smoke_only else 150,
        save_strategy="no" if args.smoke_only else "steps",
        save_steps=150,
        save_total_limit=2,
        load_best_model_at_end=(not args.smoke_only),
        metric_for_best_model="eval_loss",
        greater_is_better=False,
        report_to="none",
        seed=args.seed,
        dataloader_num_workers=0 if os.name == "nt" else 2,
        prediction_loss_only=True,
        remove_unused_columns=False,
    )

    class _ListSampler(torch.utils.data.Sampler[int]):
        def __init__(self, indices: list[int]) -> None:
            self.indices = indices

        def __iter__(self):
            return iter(self.indices)

        def __len__(self) -> int:
            return len(self.indices)

    class LengthGroupedTrainer(Trainer):
        def _get_train_sampler(self, train_dataset=None):
            ds = train_dataset if train_dataset is not None else self.train_dataset
            if not args.group_by_length or not hasattr(ds, "lengths"):
                return super()._get_train_sampler(train_dataset)
            bs = self.args.per_device_train_batch_size
            mega = bs * 50
            rng = random.Random(self.args.seed + int(self.state.epoch or 0))
            indices = list(range(len(ds)))
            rng.shuffle(indices)
            batches = []
            for i in range(0, len(indices), mega):
                chunk = indices[i : i + mega]
                chunk.sort(key=lambda idx: ds.lengths[idx], reverse=True)
                for j in range(0, len(chunk), bs):
                    batches.append(chunk[j : j + bs])
            rng.shuffle(batches)
            return _ListSampler([idx for b in batches for idx in b])

        def compute_loss(self, model, inputs, return_outputs=False, num_items_in_batch=None):
            labels = inputs.get("labels")
            if labels is None or return_outputs:
                return super().compute_loss(
                    model, inputs, return_outputs=return_outputs, num_items_in_batch=num_items_in_batch
                )
            base = model.get_base_model() if hasattr(model, "get_base_model") else model
            outputs = base.model(
                input_ids=inputs["input_ids"],
                attention_mask=inputs.get("attention_mask"),
                use_cache=False,
            )
            shift_hidden = outputs.last_hidden_state[:, :-1, :]
            shift_labels = labels[:, 1:]
            valid_mask = shift_labels.ne(-100)
            valid_hidden = shift_hidden[valid_mask]
            valid_labels = shift_labels[valid_mask]
            logits = base.lm_head(valid_hidden)
            softcap = getattr(base.config, "final_logit_softcapping", None)
            if softcap is not None:
                logits = torch.tanh(logits / softcap) * softcap
            if num_items_in_batch is not None:
                loss = torch.nn.functional.cross_entropy(logits.float(), valid_labels, reduction="sum")
                loss = loss / num_items_in_batch
            else:
                loss = torch.nn.functional.cross_entropy(logits.float(), valid_labels, reduction="mean")
            return loss

    trainer = LengthGroupedTrainer(
        model=model,
        args=train_args,
        train_dataset=train_ds,
        eval_dataset=val_ds,
        data_collator=RightPadCollator(tokenizer.pad_token_id),
    )

    train_result = trainer.train()
    eval_metrics = trainer.evaluate()

    # Check finite losses across all logged steps
    losses = [h["loss"] for h in trainer.state.log_history if "loss" in h]
    assert losses and all(math.isfinite(x) for x in losses), f"Non-finite training loss detected: {losses}"
    assert math.isfinite(eval_metrics["eval_loss"]), f"Non-finite eval loss: {eval_metrics}"

    model.save_pretrained(str(adapter_dir))
    tokenizer.save_pretrained(str(adapter_dir))

    # Reload verification & diagnostic prompts
    sample_prompts = [
        "Glossary:\n踊り子 => 무도가\n\nTranslate this from Japanese to Korean:\nJapanese: ギアセット20「踊り子」に装備変更しました。\nKorean:",
        "Glossary:\nレストエリア => 휴식 지역\n\nTranslate this from Japanese to Korean:\nJapanese: レストエリアに入った！\nKorean:",
        "Glossary:\nLimsa Lominsa => 림사 로민사\nMizzenmast => 미젠마스트\n\nTranslate this from English to Korean:\nEnglish: Welcome to the heart of Limsa Lominsa─the Mizzenmast!\nKorean:",
        "Translate this from Japanese to Korean:\nJapanese: おつかれさまでした！\nKorean:",
    ]

    model.config.use_cache = True
    quick_samples = run_quick_inference(model, tokenizer, sample_prompts)

    # Verify adapter reload from disk
    reload_ok = False
    if args.smoke_only:
        del model, base_model, trainer
        torch.cuda.empty_cache()
        reloaded_base = AutoModelForCausalLM.from_pretrained(
            args.model_id,
            dtype=torch.bfloat16,
            device_map="auto",
            attn_implementation="eager",
            trust_remote_code=True,
        )
        reloaded = PeftModel.from_pretrained(reloaded_base, str(adapter_dir))
        reloaded_samples = run_quick_inference(reloaded, tokenizer, sample_prompts[:1])
        reload_ok = reloaded_samples[0]["output"] == quick_samples[0]["output"]
    elif args.merged_dir:
        args.merged_dir.mkdir(parents=True, exist_ok=True)
        merged = model.merge_and_unload()
        merged.save_pretrained(str(args.merged_dir), safe_serialization=True)
        tokenizer.save_pretrained(str(args.merged_dir))
        reload_ok = (args.merged_dir / "model.safetensors").exists() or any(
            args.merged_dir.glob("model-*.safetensors")
        )

    peak_vram = int(torch.cuda.max_memory_allocated())
    elapsed = round(time.time() - t0, 2)

    summary = {
        "mode": "smoke" if args.smoke_only else "full",
        "model_id": args.model_id,
        "model_revision": getattr(tokenizer, "name_or_path", args.model_id),
        "eos_token_id": tokenizer.eos_token_id,
        "bos_token_id": tokenizer.bos_token_id,
        "gpu_name": torch.cuda.get_device_name(0),
        "gpu_memory_bytes": int(torch.cuda.get_device_properties(0).total_memory),
        "peak_vram_allocated_bytes": peak_vram,
        "trainable_params": trainable_params,
        "total_params": total_params,
        "train_examples": len(train_ds),
        "val_examples": len(val_ds),
        "replay_examples_added": replay_count,
        "skipped_over_max_len": train_ds.skipped_long,
        "train_jsonl_sha256": sha256_file(args.train_jsonl),
        "val_jsonl_sha256": sha256_file(args.val_jsonl),
        "train_steps": trainer.state.global_step if not args.smoke_only else 5,
        "first_loss": losses[0],
        "final_loss": losses[-1],
        "train_runtime_loss": float(train_result.training_loss),
        "eval_loss": float(eval_metrics["eval_loss"]),
        "reload_or_merge_verified": reload_ok,
        "elapsed_seconds": elapsed,
        "quick_samples": quick_samples,
    }
    summary_path = args.out_dir / ("smoke_summary.json" if args.smoke_only else "train_summary.json")
    summary_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
