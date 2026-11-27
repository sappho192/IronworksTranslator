# Ironworks Translator 4B v0.1 (`ironworks-4b-r1`) LoRA SFT Experiment Report

- **Date**: 2026-11-27
- **Model Identifier**: `sappho192/ironworks-translator-4b-v0.1`
- **Base Model**: `xiaomi-research/MiLMMT-46-4B-v1.0` (HF commit `aa3262750cf493cc638fc9b82fcd26de8b0068fb`, Gemma 3 text backbone)
- **Selected Release Candidate**: `ironworks-4b-r1` with linear LoRA delta scaling `alpha = 0.70` (`Q4_K_M` recommended default + `Q8_0` high-precision option)

---

## 1. Reproducibility Manifest

| Item | Value |
| --- | --- |
| Hardware | Single local NVIDIA GeForce RTX 4080 SUPER (16 GB VRAM) |
| Training Script | `tools/milmmt-dataset-builder/train_milmmt_lora.py` |
| GGUF Export Script | `tools/milmmt-dataset-builder/export_gguf.py` (444 text-only Gemma 3 tensors; vision tower stripped; exact 262,208-token SentencePiece vocab copied from official `MiLMMT-46-4B-v1.0.Q4_K_M.gguf`, `add_bos_token=false`, `add_space_prefix=false`) |
| Quantizer / Server | `llama.cpp` build `9873 (a4107133a)` (`llama-quantize.exe`, `llama-server.exe`) |
| Evaluation Decoding | Greedy (`temperature=0.0, top_k=1, top_p=1.0, min_p=0.0, repeat_penalty=1.0, frequency_penalty=0.30, presence_penalty=0.0, repeat_last_n=16, seed=1`) — exact match to `IronworksTranslator` Step 0 |
| Runtime Glossary Baseline | `2026-11-27.2` (`54,153` terms, includes Ultimate raid shorthands) |
| Train Dataset (`2026-11-27.3`) | `17,500` FFXIV + colloquial examples (`SHA-256: 88942ce6320be45d9a94e0c67a9b0f2cbbe473963edd5822d3272dd4a84e4c5f`, `eval_overlap=0`, `leaked_eval_skipped=2`) + `5,000` 4B on-policy general replay examples across all 6 directions (`JA->KO` 2,500, `EN->KO` 1,500, `DE->KO` 250, `FR->KO` 250, `KO->JA` 250, `KO->EN` 250; ~22.2% replay ratio) → `22,495` used (`5` skipped over `max_len=320`) |
| Val Dataset (`2026-11-27.3`) | `500` examples (`SHA-256: 3b2aafa06046c31d202f8d88735e7611cc488b435581bfa3692347d8dd9c37a6`) |
| Hyperparameters | `lora_r=16, lora_alpha=32, lora_dropout=0.05`, `exclude_modules=["vision_tower", "multi_modal_projector"]` (`32,788,480` trainable / `4,332,867,952` total params), `lr=3e-5`, cosine schedule, `epochs=2.0` (`704` steps), `bs=4 x grad_accum=16` (`eff_bs=64`), `max_len=320`, `sdpa`, gradient checkpointing, length grouping, masked-only `lm_head` CE loss, `vram_fraction=0.82` |
| Training Runtime & VRAM | `2,976.02s` (49m 36s), peak VRAM allocated `11.49 GB` (`11,485,059,072` bytes), 0 WDDM spillover, `reload_or_merge_verified=true` |
| Loss Trajectory | `first_loss=1.9665` → `final_loss=1.1479` (`train_runtime_loss=1.2835`); monotonic `eval_loss`: `1.612` (step 150) → `1.536` (300) → `1.496` (450) → `1.491` (600) → `1.491` (final) |

### Release GGUF Artifacts (`alpha = 0.70`)

| File | Quantization | Bytes | BPW | SHA-256 |
| --- | :---: | ---: | :---: | --- |
| `ironworks-translator-4b-v0.1.Q4_K_M.gguf` | `Q4_K_M` (default) | `2,489,892,704` | `5.12` | `07e235125d266e2f5e79d73d33b73c0193e84773caec1d2e862ecd8588d97dd6` |
| `ironworks-translator-4b-v0.1.Q8_0.gguf` | `Q8_0` | `4,130,400,864` | `8.50` | `0039cf32ec0d474433b78305c9ec9e10544066638a84ec0607c545550a111a8f` |

---

## 2. Stability & Safety Gates (Reported First)

All evaluations ran on actual quantized GGUFs served by `llama-server.exe` with identical Step 0 decoding (`n=15` hold-out items + `n=25` distractor + `n=16` counterfactual + `n=24` general multilingual + `n=6` short triggers = `86` prompts per model).

| Model (Quant) | Hold-out Drift (`n=15`) | Hold-out Loops (`n=15`) | Distractor False-Insert (`n=25`) | Distractor Drift/Loops | General Errors (`n=24`, loop/drift/empty/trunc) | Short Errors (`n=6`, loop/trunc) | Game Tag Preservation (`n=1`) |
| --- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| Baseline `MiLMMT-46-4B-v1.0` (`Q4_K_M`) | `1/15` | `0/15` | `0/25` (0.0%) | `0 / 0` | `0 / 0 / 0 / 0` | `0 / 0` | `1/1` |
| `ironworks-4b-r1` `alpha=1.00` (`Q4_K_M`) | `0/15` | `0/15` | `0/25` (0.0%) | `0 / 0` | `0 / 0 / 0 / 0` | `0 / 0` | `1/1` |
| `ironworks-4b-r1` `alpha=0.85` (`Q4_K_M`) | `0/15` | `0/15` | `0/25` (0.0%) | `0 / 0` | `0 / 0 / 0 / 0` | `0 / 0` | `1/1` |
| **`ironworks-4b-r1` `alpha=0.70` (`Q4_K_M`, Selected)** | **`0/15`** | **`0/15`** | **`0/25` (0.0%)** | **`0 / 0`** | **`0 / 0 / 0 / 0`** | **`0 / 0`** | **`1/1`** |
| **`ironworks-4b-r1` `alpha=0.70` (`Q8_0`, Selected)** | **`0/15`** | **`0/15`** | **`0/25` (0.0%)** | **`0 / 0`** | **`0 / 0 / 0 / 0`** | **`0 / 0`** | **`1/1`** |

---

## 3. Content & Glossary Control Metrics (`alpha` Pareto Sweep & 1B vs 4B)

| Model (Quant) | Hold-out Term (No Glossary) | Hold-out Term (With Glossary, Strict Regex*) | Hold-out Term (With Glossary, Human-Verified) | Counterfactual Prompt Hits (`n=16`) | Counterfactual Pair Swaps (`n=8`) | General 24-Sentence Qualitative Fidelity |
| --- | :---: | :---: | :---: | :---: | :---: | --- |
| Baseline `MiLMMT-46-4B-v1.0` (`Q4_K_M`) | `2/9` (22.2%) | `2/9` (22.2%) | `2/9` (22.2%) | `5/16` (31.3%) | `0/8` (0.0%) | Reference baseline (some literalisms) |
| `ironworks-4b-r1` `alpha=1.00` (`Q4_K_M`) | `3/9` (33.3%) | `8/9` (88.9%) | `9/9` (100.0%) | `16/16` (100.0%) | `8/8` (100.0%) | **Regressions**: EN maintenance notice hallucination, FR meaning flip (`협력을 부탁드립니다`), DE over-truncation |
| `ironworks-4b-r1` `alpha=0.85` (`Q4_K_M`) | — | `7/9` (77.8%) | `9/9` (100.0%) | `16/16` (100.0%) | `8/8` (100.0%) | Clean on DE/FR/EN, minor EN multi-clause & CF wording roughness (`pulling` → `딜을 하기 전에`) |
| **`ironworks-4b-r1` `alpha=0.70` (`Q4_K_M`)** | `1/9` (11.1%) | `7/9` (77.8%) | **`9/9` (100.0%)** | **`16/16` (100.0%)** | **`8/8` (100.0%)** | **Clean across all 24 sentences** (JA/EN/DE/FR→KO, KO→JA/EN); preserves "pulling" nuance and improves JA/EN naturalness over baseline |
| **`ironworks-4b-r1` `alpha=0.70` (`Q8_0`)** | `1/9` (11.1%) | `7/9` (77.8%) | **`9/9` (100.0%)** | **`16/16` (100.0%)** | **`8/8` (100.0%)** | Identical gate parity to `Q4_K_M`; slightly tighter phrasing on multi-clause EN dialogue |
| *(Reference)* `ironworks-translator-1b-v0.1` (`Q8_0`, `alpha=0.65`) | `3/9` (33.3%) | `9/9` (100.0%) | `9/9` (100.0%) | `16/16` (100.0%) | `8/8` (100.0%) | Clean; smaller 1B backbone |
| *(Reference)* `ironworks-translator-1b-v0.1` (`Q4_K_M`, `alpha=0.65`) | `3/9` (33.3%) | `9/9` (100.0%) | `9/9` (100.0%) | `14/16` (87.5%) | `6/8` (75.0%) | Drops 2/8 CF pairs at `Q4_K_M` |

\* **Note on `eval_gguf.py` strict regex (`7/9` vs `9/9`)**: `eval_gguf.py` strips all whitespace (`out.replace(" ", "")`) before testing `target + (end | punctuation | particle)`. For `alpha=0.70` (`Q4_K_M` and `Q8_0`), the two items flagged as regex misses both emitted the exact requested glossary string verbatim followed by a space + non-particle Korean word (`로민사 공중` and `골드 소서 안에`). Both the unmodified script score (`7/9`) and the human-verified verbatim count (`9/9`) are reported above for full transparency.

---

## 4. Analysis: Evidence, Interpretation, Limitations, and Next Decision

### Evidence
- Adding 4B on-policy general replay across all 6 directions (including `FR->KO`, which was absent in the 1B replay pass) eliminated the severe FR/EN sentence-dropping observed in earlier unscaled 1B runs, yet `alpha=1.00` still exhibited subtle FFXIV-domain boilerplate bleed on 3/24 general sentences (EN maintenance, DE thanks, FR thanks).
- Scaling the LoRA delta linearly to `alpha=0.70` completely eliminated all 3 general-domain regressions while retaining **100% (9/9 verbatim)** hold-out glossary compliance, **100% (8/8 pairs, 16/16 prompts)** counterfactual swapping even at `Q4_K_M`, **0/25** distractor false insertions, and **0** stability failures.
- Unlike 1B (which lost 2/8 counterfactual swap pairs when quantized from `Q8_0` to `Q4_K_M`), 4B retained **100% (8/8)** counterfactual parity at `Q4_K_M` (~2.32 GiB), making `Q4_K_M` a strong default quantization for 4B.

### Interpretation
- The 4B backbone absorbs the `Glossary:` conditioning interface strongly enough that `alpha=0.70` sits at a strict Pareto optimum: full glossary controllability with zero general-domain stylistic contamination.
- The drop from `2/9` (baseline no-glossary) to `1/9` (`alpha=0.70` no-glossary) paired with `9/9` (`alpha=0.70` with-glossary) is desirable behavior: the model defers terminology decisions to the injected runtime glossary rather than memorizing stale mappings in weights.

### Limitations
- Hold-out `eval.jsonl` (`n=15`, `9` glossary checks) is small and primarily tests JA/EN→KO plus short Japanese chat; broader live in-game chat (especially slang, abbreviations, and multi-line NPC quests) requires interactive testing in `IronworksTranslator`.
- Very short ambiguous Japanese chat typos/onomatopoeia (e.g., 2–3 character clipped forms) remain inherently uncertain without surrounding conversational context.
- `eval_gguf.py`'s whitespace-stripping regex under-counts valid Korean compound noun phrases (`<term> <noun>`); fixing the regex was intentionally deferred to keep all scores directly comparable to prior baseline logs.

### Next Decision
1. Validate `Ironworks_4B` (`Q4_K_M` and `Q8_0`) interactively in the Debug `IronworksTranslator` build against live FFXIV chat and NPC dialogue alongside runtime glossary `2026-11-27.2`.
2. Upon user confirmation, publish `sappho192/ironworks-translator-4b-v0.1` (`Q4_K_M`, `Q8_0`, `README.md`) to HuggingFace Hub and push commits in `IronworksTranslator` and `ironworks-db-endpoint`.
