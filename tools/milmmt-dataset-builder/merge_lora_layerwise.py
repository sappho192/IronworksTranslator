#!/usr/bin/env python3
"""Layer-wise alpha LoRA merge for MiLMMT-46 (Gemma 3) without re-training.

Applies a depth-dependent LoRA scaling ramp from `alpha_low` (layer 0, preserving general
multilingual representations) to `alpha_high` (final layer, sharpening domain terminology and
Korean speech-style endings). Setting `alpha_low == alpha_high` performs a uniform merge.
"""

from __future__ import annotations

import argparse
import os
import re
from pathlib import Path

# Set HF cache / offline defaults BEFORE importing transformers/huggingface_hub so offline
# runs resolve local cached weights deterministically.
if "HF_HOME" not in os.environ:
    default_cache = Path("artifacts/hf_cache")
    if default_cache.exists():
        os.environ["HF_HOME"] = str(default_cache.resolve())

RE_LAYER = re.compile(r"\.layers\.(\d+)\.")


def main() -> int:
    parser = argparse.ArgumentParser(description="Layer-wise alpha LoRA merge for MiLMMT-46.")
    parser.add_argument("--base-model", required=True, help="HF model ID or local base model directory.")
    parser.add_argument("--adapter-dir", type=Path, required=True, help="PEFT LoRA adapter directory.")
    parser.add_argument("--alpha-low", type=float, required=True, help="LoRA scale multiplier at layer 0.")
    parser.add_argument("--alpha-high", type=float, required=True, help="LoRA scale multiplier at final layer.")
    parser.add_argument("--power", type=float, default=1.0, help="Ramp exponent (1.0=linear, 2.0=late-weighted).")
    parser.add_argument("--out-dir", type=Path, required=True, help="Destination directory for merged bf16 model.")
    parser.add_argument("--online", action="store_true", help="Allow Hugging Face Hub network lookups.")
    args = parser.parse_args()

    local_only = not args.online
    if local_only:
        os.environ.setdefault("HF_HUB_OFFLINE", "1")

    import torch
    from peft import PeftModel
    from transformers import AutoModelForCausalLM, AutoTokenizer

    tokenizer = AutoTokenizer.from_pretrained(args.base_model, trust_remote_code=True, local_files_only=local_only)
    base = AutoModelForCausalLM.from_pretrained(
        args.base_model,
        dtype=torch.bfloat16,
        device_map="cpu",
        trust_remote_code=True,
        local_files_only=local_only,
    )
    model = PeftModel.from_pretrained(base, str(args.adapter_dir))

    layer_indices: set[int] = set()
    for name, module in model.named_modules():
        if hasattr(module, "scaling") and isinstance(module.scaling, dict):
            m = RE_LAYER.search(name)
            if m:
                layer_indices.add(int(m.group(1)))
    max_layer = max(layer_indices) if layer_indices else 1

    scaled_count = 0
    alpha_by_layer: dict[int, float] = {}
    for name, module in model.named_modules():
        if hasattr(module, "scaling") and isinstance(module.scaling, dict):
            m = RE_LAYER.search(name)
            if m:
                idx = int(m.group(1))
                frac = (idx / max_layer) ** args.power
                layer_alpha = args.alpha_low + (args.alpha_high - args.alpha_low) * frac
                alpha_by_layer[idx] = round(layer_alpha, 4)
            else:
                layer_alpha = (args.alpha_low + args.alpha_high) / 2.0
            for k in list(module.scaling.keys()):
                module.scaling[k] = module.scaling[k] * layer_alpha
            scaled_count += 1

    merged = model.merge_and_unload()
    args.out_dir.mkdir(parents=True, exist_ok=True)
    merged.save_pretrained(str(args.out_dir), safe_serialization=True)
    tokenizer.save_pretrained(str(args.out_dir))
    mean_alpha = sum(alpha_by_layer.values()) / max(len(alpha_by_layer), 1)
    print(
        f"Saved layer-wise merged model ({args.alpha_low:.2f}->{args.alpha_high:.2f}, p={args.power:.1f}, "
        f"layers={len(alpha_by_layer)}, mean_alpha={mean_alpha:.4f}, modules={scaled_count}) -> {args.out_dir}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
