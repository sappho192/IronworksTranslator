---
name: train-eval-milmmt
description: Prepare datasets, run bf16 LoRA SFT or preference alignment, merge (uniform or layer-wise alpha), quantize (Q8_0 and lossless BF16 -> imatrix Q5_K_M), and execute 6-gate quantitative plus qualitative GGUF evaluation for MiLMMT-46-1B/4B Ironworks Translator models. Use when training, merging, quantizing, evaluating, or diagnosing regressions in Ironworks Translator MiLMMT models.
---

# Train and Evaluate Ironworks Translator (MiLMMT-46)

## 1. Non-negotiable evaluation invariants

- Never judge or promote a model from `eval_loss` or unquantized bf16 outputs alone. Always evaluate the **actual quantized GGUF** served by `llama-server.exe` using Step 0 greedy decoding:
  `temperature=0.0, top_k=1, top_p=1.0, min_p=0.0, repeat_penalty=1.0, frequency_penalty=0.30, repeat_last_n=16, seed=1`.
- Always report **stability failures first** with exact denominators before content metrics:
  loops, language drift, empty/truncated output, tag/prefix leaks (`Style:`, `Glossary:`, `Context:`, `Previous:`), and unprompted style bleed.
- Always run a **manual side-by-side qualitative audit** of all 24 `general` multilingual prompts, hold-out long prose (especially `5ddb5185`), and Gate 6 outputs before promoting a checkpoint.
- Never commit `artifacts/`, GGUF weights, datasets, calibration files, or local absolute paths (`C:\`, `D:\`, `file:///`) into Git or `docs/agent-log/`.

## 2. Environment setup (Windows / Offline HF / Sandbox loopback)

Always export environment variables **before** starting Python or PowerShell evaluation scripts:

```powershell
$env:HF_HOME = (Resolve-Path "artifacts\hf_cache").Path
$env:HF_HUB_OFFLINE = "1"
$env:NO_PROXY = "127.0.0.1,localhost"
$env:no_proxy = "127.0.0.1,localhost"
```

- Setting `NO_PROXY` prevents proxy interception on `http://127.0.0.1:<port>/completion` when running `llama-server.exe` locally.
- Setting `HF_HOME` and `HF_HUB_OFFLINE=1` before Python starts ensures `transformers` resolves cached `MiLMMT-46-1B-v1.0` and `MiLMMT-46-4B-v1.0` cleanly offline.

## 3. Dataset assembly checklist

1. **Leakage guard**: Guard all newly added FFXIV / contrastive / context sources against `artifacts/curation/exports/.../eval.jsonl` (`EvalLeakageGuard`, Jaccard < 0.65) AND `tools/milmmt-dataset-builder/eval_suite.py` (`STYLE_CONTROL_CASES` 5-gram Jaccard < 0.45).
2. **Replay floor (critical)**:
   - Require **general 6-direction replay ≥ 18–20%** (`5,500–6,000` rows for a `~23,400`-row FFXIV budget), including explicit coverage of `DE->KO`, `FR->KO`, `KO->JA`, and `KO->EN`.
   - Do **not** accidentally pass legacy `replay_general.jsonl` (`3,325` rows); use the full `5,000–6,000`-row pool.
3. **Budget discipline**:
   - Keep total FFXIV training rows near the verified baseline (`~23,400–24,000`) by **budget-replacing** from `plain` / redundant `glossary_only` rows rather than stacking extra rows on top.
   - Preserve **100% of verified official `style_only` and `style+glossary` rows** (never randomly drop official `고어체`/`해라체` rows).
   - Re-verify all `Style:` labels with the current `tools/milmmt-glossary-builder/korean_style.py`.
4. **Model-size specific rules**:
   - **1B**: Do not repeat the same short source sentence >3–4 times per epoch (6+ styles × 2 epochs causes long-sentence repetition on `5ddb5185`). Avoid heavy `Context:` SFT on `1B` (2-sentence input → 1-sentence output biases `1B` to drop sentence 2 on `KO->EN #24`).
   - **4B**: Do not expect `4B -> 4B` self-distillation to break the `~85%` SFT style ceiling; use external stronger data (official KO + 12B `KO->EN` back-translation) or preference alignment (DPO/CPO/SimPO) for adjacent-register boundaries (`고어체 -> 하오체`, `해라체 -> 하게체/반말`, `해요체 -> 하십시오체`).
   - **Context (Axis 5)**: Never use raw FFXIV EN consecutive quest pairs directly (EN transcreation diverges from KO). Use 1:1 `JA->KO` official pairs + `MiLMMT-46-12B` `KO->EN` back-translated consecutive pairs + ~30% distractor contexts + leak-free counterfactual disambiguation pairs.
5. **Token length check**: Measure `max_len` with the target tokenizer beforehand and set `--max-len` (e.g., `384`) so `skipped_over_max_len == 0`.
6. **Validation parity**: Keep `val_sft.jsonl` identical to the baseline SHA-256 for apples-to-apples `eval_loss` tracking.

## 4. Training pre-flight (match baseline hyperparameters)

Before launching `tools/milmmt-dataset-builder/train_milmmt_lora.py`, inspect the baseline's `checkpoints/*/trainer_state.json` and `adapter/adapter_config.json` to confirm exact `learning_rate`, `batch_size`, `epochs`, and LoRA `r`/`alpha`:

```powershell
# Verified RTX 4080 SUPER (16 GB VRAM) 4B SFT baseline (~68 min, peak ~10.9 GB VRAM):
artifacts\.venv-milmmt\Scripts\python.exe tools\milmmt-dataset-builder\train_milmmt_lora.py `
  --model-id xiaomi-research/MiLMMT-46-4B-v1.0 `
  --train-jsonl <TRAIN_JSONL> `
  --val-jsonl <VAL_JSONL> `
  --replay-jsonl <REPLAY_JSONL> `
  --replay-max 5500 `
  --out-dir <OUT_DIR> `
  --merged-dir <OUT_DIR>\merged `
  --epochs 2.0 --lr 5e-5 --batch-size 4 --grad-accum 16 --max-len 384 `
  --attn-impl sdpa --gradient-checkpointing --group-by-length --vram-fraction 0.82 --seed 42
```

- Check `v0.2` baseline LR (`5e-5`), **not** `v0.1` (`3e-5`).
- Confirm `reload_or_merge_verified: true` and `skipped_over_max_len: 0` in `train_summary.json`.

### 4.1 On-Policy CPO-SimPO preference alignment (breaking the 4B SFT ceiling)

When SFT plateaus on adjacent-register boundaries (`고어체 -> 하오체/해라체`, `해라체 -> 반말/하게체`, `해요체 -> 하십시오체`) or when incorporating user-curated preference pairs (`cpo_user.jsonl`), run `tools/milmmt-dataset-builder/train_milmmt_cpo.py` on top of the best merged bf16 SFT checkpoint (e.g., `merged_4b_lw_84_100`):

```powershell
artifacts\.venv-milmmt\Scripts\python.exe tools\milmmt-dataset-builder\train_milmmt_cpo.py `
  --base-model <MERGED_SFT_BF16_DIR> `
  --cpo-jsonl <PURE_ONPOLICY_CPO_JSONL> `
  --anchor-jsonl <ONPOLICY_KL_ANCHOR_JSONL> `
  --out-dir <OUT_DIR> `
  --epochs 3.0 --lr 2e-5 --grad-accum 32 --beta 2.0 --gamma 0.5 `
  --lambda-nll 0.5 --lambda-anchor 1.0 --lora-r 8 --lora-alpha 16 --max-len 384
```

- Filter mined on-policy failures to confirmed wrong-register outputs (`rejected_style != "None"`) so valid trailing vocatives are never falsely penalized.
- Pair CPO examples with **on-policy KL anchors** (the base checkpoint's own outputs on 6-direction general replay + glossary/style-glossary anchors) to guarantee `0 / 24` general regressions.
- Always sweep CPO delta scaling (`a50`, `a60`, `a75`, `lw40-75`) via `merge_lora_layerwise.py` instead of blindly shipping `a100` (which can overshoot into distractor/loop regressions). Empirically, `a50` (`Q8_0` + lossless `BF16 -> imatrix Q5_K_M`) broke `4B` from `85.0%` SFT up to `90.0–91.3%` with `0/182` stability failures and `0/24` general regressions.

## 5. Merging and quantization

1. **Merge sweep (`tools/milmmt-dataset-builder/merge_lora_layerwise.py`)**:
   - Always test at least `a92` (`0.92 -> 0.92`), **`lw84-100` (`0.84 -> 1.00`, recommended default for 4B)**, and `a100` (`merged`).
   - On `4B`, `lw84-100` preserves general `DE->KO` / `FR->KO` / `KO->EN` fidelity in lower layers while keeping full register sharpness in upper layers.
2. **GGUF export (`tools/milmmt-dataset-builder/export_gguf.py`)**:
   - Pass a verified reference GGUF of the matching size (`1B` or `4B`) via `--ref-gguf` so the exact 262,208-token SentencePiece vocabulary, `add_bos_token=false`, and `add_space_prefix=false` are preserved.
3. **Lossless `imatrix Q5_K_M` quantization**:
   - **Never** requantize directly from `Q8_0` (`llama-quantize` disables `q8_0` requantization by default and double-quantizing degrades quality).
   - Instead: compute `imatrix.dat` on the `Q8_0` GGUF with a leak-free FFXIV + multilingual calibration file (`--chunks 100 -c 512 -ngl 99`), export a temporary lossless **`bf16`** GGUF, run `llama-quantize.exe --imatrix <dat> <bf16.gguf> <out.Q5_K_M.gguf> Q5_K_M`, and delete the temporary `bf16` file.
   - Avoid `imatrix Q4_K_M` as a quality upgrade; empirical tests showed 4-bit precision ceiling drops style accuracy on both `1B` and `4B`.

## 6. Six-gate evaluation and qualitative audit

Run all applicable gates on every candidate GGUF (increment `--port` between calls):

```powershell
$py = "artifacts\.venv-milmmt\Scripts\python.exe"
& $py tools\milmmt-dataset-builder\eval_gguf.py --model <GGUF> --eval-jsonl <EVAL_JSONL> --with-glossary --port 19010 --out-json <OUT>\eval.json
& $py tools\milmmt-dataset-builder\eval_suite.py --model <GGUF> --eval-style --port 19011 --out-json <OUT>\suite.json
& $py tools\milmmt-dataset-builder\eval_context_gate.py --model <GGUF> --port 19012 --out-json <OUT>\gate6.json
```

### Mandatory promotion criteria
1. **Stability failures**: `0 / 151` (Gates 1–5) or `0 / 182` (Gates 1–6).
2. **Glossary & safety gates**: `eval.jsonl` `9/9`, Distractor `0/25`, Counterfactual `16/16 (8/8 pairs)`, Style+Glossary joint `24/24`, Gate 6 Distractor Context bleed `0/15`, Tag leaks `0`.
3. **General 24 qualitative audit (`0 / 24` regressions)**: Inspect every differing sentence vs baseline, paying special attention to `#02 (JA->KO)`, `#13 (EN->KO)`, `#15 & #17 (DE->KO)`, `#18 (FR->KO)`, `#22 (KO->JA)`, and `#24 (KO->EN)`.
4. **Long-sentence prose audit**: Inspect `eval.jsonl` `5ddb5185` for semantic completeness and absence of subtle sentence repetition that can evade n-gram loop detectors.
5. **Report hygiene**: Record results in `docs/agent-log/YYYY-MM-DD/` with stability first, exact denominators, per-register table, honest negative findings, and zero private paths or user chat text.
