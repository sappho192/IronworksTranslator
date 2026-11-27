# MiLMMT-46-1B-v1.0 FFXIV Glossary-Conditioned LoRA SFT 1차 실험 리포트

- **작성일**: 2026-11-27
- **Run ID**: `milmmt-1b-r7` (및 선형 가중치 병합 `milmmt-1b-r7-a50`, `milmmt-1b-r7-a65`)
- **선행 설계 문서**: [2026-11-27-milmmt-dataset-and-training-blueprint.md](file:///d:/REPO/IronworksTranslator/docs/agent-log/2026-11-27/2026-11-27-milmmt-dataset-and-training-blueprint.md)
- **권장 후보 모델**: `MiLMMT-46-1B-v1.0-ffxiv-r7-a65.Q8_0.gguf` (LoRA delta scaling `α = 0.65`)

---

## 1. 실험 목적과 가설

- **목적**: `xiaomi-research/MiLMMT-46-1B-v1.0`에 런타임 `Glossary:` 프롬프트 준수 능력을 부여하되, ① 비관련(distractor) 용어 오삽입을 3% 이하로 억제하고, ② 일반 비-FFXIV 다국어 번역 품질을 보존하며, ③ 런타임 용어 변경(글로벌 음차 vs 한국 서버 공식명)에 즉각 반응하는지 검증한다.
- **검증 대상 모델**: 학습 손실이나 BF16 병합 성공이 아니라, 실제 배포 형식인 양자화 GGUF(`Q8_0`, `Q4_K_M`)를 앱과 동일한 Step 0 디코딩 파라미터(`temperature=0.0, top_k=1, repeat_penalty=1.0, frequency_penalty=0.30, repeat_last_n=16`)로 평가한다.

---

## 2. 재현성 매니페스트 (Reproducibility Manifest)

| 항목 | 값 |
| --- | --- |
| Base Repo Commit | `0da367b5195318e5aeef75913f14b4cce675fed0` (`master`, 작업 트리 변경 포함) |
| Base Model | `xiaomi-research/MiLMMT-46-1B-v1.0` (HF snapshot `4fc480b6c58dec29c159dcdf9fde0f6d5c354995`, Gemma3ForCausalLM, 262,144 vocab) |
| Hardware | NVIDIA GeForce RTX 4080 SUPER (16,376 MiB VRAM, Native BF16) |
| Python / Packages | Python 3.12.11, `torch==2.11.0+cu128`, `transformers==5.24.1`, `peft==0.22.0`, `accelerate==1.16.0`, `datasets==5.2.0`, `gguf==0.19.0`, `sentencepiece==0.2.3` |
| Quantizer | `llama.cpp` build `9873 (a4107133a)` (`convert_hf_to_gguf` 기반 [export_gguf.py](file:///d:/REPO/IronworksTranslator/tools/milmmt-dataset-builder/export_gguf.py) + `llama-quantize.exe`) |
| Tokenizer Contract | `add_bos_token=False`, `add_eos_token=False`, `add_space_prefix=False`, `eos_token_id=1`, `tokenizer.ggml.model=llama` (공식 `MiLMMT-46-1B-v1.0.Q8_0.gguf`에서 262,144 토큰/스코어/타입 100% 동일 복사) |
| Train Dataset (`17,500`) | `artifacts/dataset/2026-11-27.2/train_sft.jsonl` (`SHA-256: 9554e536d373231df51c7e3b0524874fe3a9e6f55eb200dfe7f13601445a43b1`) |
| Val Dataset (`500`) | `artifacts/dataset/2026-11-27.2/val_sft.jsonl` (`SHA-256: d572c74447092cf9bad4bc8cd03ffc84b3907cfdfe51599e0c1c56540f8782f9`) |
| Axis-3 Replay (`3,325`) | `artifacts/dataset/2026-11-27.2/replay_general.jsonl` (`SHA-256: 454409151e0e460bee0ad910e00975d8eeb01769098a365144ecb9ec0787f74d`) |
| Hold-out Eval (`15`) | `artifacts/curation/exports/2026-11-27.1/eval.jsonl` (`SHA-256: 79119707de89466677606b7334bba808ff70de44aebf0d591d8fe255bdf03b5a`, `eval_train_overlap = 0`) |
| LoRA Config | `r=16`, `alpha=32`, `dropout=0.05`, `target_modules=[q,k,v,o,gate,up,down]_proj` (`13,045,760` trainable / `1,012,931,712` total, 1.29%) |
| Training Hyperparams | `epochs=2.0`, `lr=5e-5` (cosine, 5% warmup = 32 steps), `effective_batch=64` (`bs=8 × grad_accum=8`), `max_len=384` (`skipped_over_max_len=0`), `seed=42`, `bf16=True`, `sdpa`, `gradient_checkpointing=True`, `group_by_length=True`, masked-only `lm_head` CE loss, `vram_fraction=0.78` |
| Winning GGUF (`a65 Q8_0`) | `artifacts/runs/milmmt-1b-r7/MiLMMT-46-1B-v1.0-ffxiv-r7-a65.Q8_0.gguf` (`1,069,304,608` B, `SHA-256: c520ade5f4ebc82c0ddca705acca805d4a412e0b8af225fd286523d56e7ede84`) |
| Winning GGUF (`a65 Q4_K_M`) | `artifacts/runs/milmmt-1b-r7/MiLMMT-46-1B-v1.0-ffxiv-r7-a65.Q4_K_M.gguf` (`806,056,480` B, `SHA-256: c1dbcb27ed5ec0e3ed5693a9b49f89e29b68205f61fe5f2f57544d1c7f6cf13d`) |
| Full-Merge GGUF (`a100 Q8_0`) | `artifacts/runs/milmmt-1b-r7/MiLMMT-46-1B-v1.0-ffxiv-r7.Q8_0.gguf` (`1,069,304,608` B, `SHA-256: 32bc27aa12eb0f31a7e7796923127cecec80a3f6bd0e8fb5ec362c0569eb6b65`) |
| Full-Merge GGUF (`a100 Q4_K_M`) | `artifacts/runs/milmmt-1b-r7/MiLMMT-46-1B-v1.0-ffxiv-r7.Q4_K_M.gguf` (`806,056,480` B, `SHA-256: 2c6911381c4d45fc2a24e0a1d0c20fa5a94d38306bf609c96ee213e1826f41cf`) |

---

## 3. 실행 이력 및 실패 기록 (Preserved Attempt Log)

| 시도 | 환경 / 설정 | 결과 | 원인 및 조치 |
| --- | --- | --- | --- |
| Colab L4 smoke | Colab CLI L4 (24 GB) | **PASS** (5 steps, 20.6s, 6.1 GiB) | `torchao` 충돌 제거 후 정상 검증, 세션 종료 |
| Colab G4 / A100 할당 | `colab new --gpu G4/A100` | **FAIL (503)** | `POST /tun/m/assign` ~30.3초 타임아웃 503 (`googlecolab/google-colab-cli#292` 확인). 로컬 4080 SUPER로 전환 |
| `milmmt-1b-r1` / `r2` | 4080 SUPER, `bs=8x8`/`4x16`, `max_len=1024`, full logits | **CANCELLED** | Gemma3 262k vocab float32 logits로 VRAM 15.9 GiB 도달 → Windows WDDM spillover (60W, 10–14s/it) |
| `milmmt-1b-r3` | 4080 SUPER, `bs=2x32`, `max_len=512`, grad ckpt | **CANCELLED** | Spillover는 해소(10.4 GiB)되었으나 32회 마이크로배치로 5.56s/it(~60분) 저효율 |
| `milmmt-1b-r4` / `r5` | `bs=8x8`, `sdpa`, `group_by_length` (grad ckpt 미적용) | **CANCELLED** | `transformers 5.24.1`의 `group_by_length` 인자 제거 → 커스텀 `LengthGroupedTrainer` 구현. grad ckpt 미적용 시 긴 배치에서 15.9 GiB 도달 |
| `milmmt-1b-r6` | `bs=8x8`, grad ckpt + masked-only loss + `max_split_size_mb:128` | **CANCELLED** | 가변 길이 `[valid, 262144]` 텐서(>128 MB)에 `max_split_size_mb:128`이 캐시 분할 재사용을 막아 단편화 폭증 |
| **`milmmt-1b-r7`** | `bs=8x8`, `max_len=384`, `sdpa`, `group_by_length`, grad ckpt, masked-only `lm_head` loss, `vram_fraction=0.78` | **COMPLETE (16m 57s)** | Peak allocated **7.07 GB** (총 GPU 점유 ~12.4 GiB 상한 유지, 161W, `1.36–1.56s/it`, spillover 0) |

---

## 4. 관측 결과 (Evidence)

### 4.1 `milmmt-1b-r7` 학습 수렴 곡선

- 전체 20,825개 샘플(`train_sft` 17,500 + `replay_general` 3,325, `skipped_over_max_len=0`), 총 652 optimizer steps, 모든 loss 유한(finite).
- Train loss: `2.274` (step 25) → `1.497` (step 300) → `1.425` (step 650), 평균 `1.544`.
- Validation loss (`n=500`):
  - Step 150 (`epoch 0.46`): `1.839`
  - Step 300 (`epoch 0.92`): `1.754`
  - Step 450 (`epoch 1.38`): `1.726`
  - Step 600 (`epoch 1.84`): `1.715`
  - Final (`epoch 2.00`): `1.715` (단조 감소, 과적합 반등 없음)

### 4.2 안정성 지표 우선 보고 (Stability Failures First — 전체 86개 프롬프트 기준)

모든 모델은 동일한 Step 0 디코딩(`temp=0.0, top_k=1, repeat_penalty=1.0, frequency_penalty=0.30, repeat_last_n=16`)으로 평가했다.

| 모델 (GGUF) | Eval Drift (`n=15`) | Eval Loops (`n=15`) | Distractor Loops/Drifts (`n=25`) | General Loops/Drifts/Empty/Trunc (`n=24`) | Short Loops/Trunc (`n=6`) |
| --- | :---: | :---: | :---: | :---: | :---: |
| Baseline `v1.0.Q8_0` (with glossary) | **1/15** (일어 원문 누출) | 0/15 | 0 / 0 | 0 / 0 / 0 / 0 | 0 / 0 |
| `r7-a50.Q8_0` (`α=0.50`) | **0/15** | 0/15 | 0 / 0 | 0 / 0 / 0 / 0 | 0 / 0 |
| **`r7-a65.Q8_0` (`α=0.65`)** | **0/15** | **0/15** | **0 / 0** | **0 / 0 / 0 / 0** | **0 / 0** |
| **`r7-a65.Q4_K_M` (`α=0.65`)** | **0/15** | **0/15** | **0 / 0** | **0 / 0 / 0 / 0** | **0 / 0** |
| `r7-a100.Q8_0` (`α=1.00`) | **0/15** | 0/15 | 0 / 0 | 0 / 0 / 0 / 0 | 0 / 0 |
| `r7-a100.Q4_K_M` (`α=1.00`) | **0/15** | 0/15 | 0 / 0 | 0 / 0 / 0 / 0 | 0 / 0 |

### 4.3 콘텐츠 및 용어집 준수 지표 (LoRA Scaling `α` Pareto 비교)

| 모델 (GGUF) | Hold-out Term Acc (with glossary, `n=9`) | Hold-out Tags (`n=1`) | Distractor 오삽입 (`n=25`, Gate `≤ 3%`) | Counterfactual 프롬프트 적중 (`n=16`) | Counterfactual 쌍 스왑 (`n=8`) | 일반/장문 산문 회귀 (정성 점검) |
| --- | :---: | :---: | :---: | :---: | :---: | --- |
| Baseline `v1.0.Q8_0` (no glossary) | 0/9 (0.0%) | 1/1 | — | — | — | 기준선 (양호) |
| Baseline `v1.0.Q8_0` (with glossary) | 4/9 (44.4%) | 1/1 | 0/25 (0.0%) | 4/16 (25.0%) | 0/8 (0.0%) | 기준선 (단, `踊り子` 원문 누출 1건) |
| `r7-a50.Q8_0` (`α=0.50`) | **9/9 (100.0%)** | 1/1 | **0/25 (0.0%)** | 13/16 (81.3%) | 5/8 (62.5%) | 기준선 동등 이상 (회귀 없음) |
| **`r7-a65.Q8_0` (`α=0.65`)** | **9/9 (100.0%)** | **1/1** | **0/25 (0.0%)** | **16/16 (100.0%)** | **8/8 (100.0%)** | **기준선 동등 이상 (회귀 해소)** |
| `r7-a65.Q4_K_M` (`α=0.65`) | **9/9 (100.0%)** | 1/1 | **0/25 (0.0%)** | 14/16 (87.5%) | 6/8 (75.0%) | 양호 |
| `r7-a100.Q8_0` (`α=1.00`) | **9/9 (100.0%)** | 1/1 | **0/25 (0.0%)** | **16/16 (100.0%)** | **8/8 (100.0%)** | **EN→KO 장문 누락/반말화, FR→KO 어색함 발생** |
| `r7-a100.Q4_K_M` (`α=1.00`) | **9/9 (100.0%)** | 1/1 | **0/25 (0.0%)** | **16/16 (100.0%)** | **8/8 (100.0%)** | `a100 Q8_0`과 유사한 장문 산문 드리프트 |

### 4.4 정성 비교 핵심 사례 (공개 벤치마크 / 일반 문장)

1. **Counterfactual 런타임 용어 스왑 (`r7-a65.Q8_0`, 8/8 통과)**:
   - `Limsa Lominsa / Mizzenmast`: `Mizzenmast => 미젠마스트`와 `Mizzenmast => 뒷돛대`를 각각 지정 시 두 번역 모두 정확히 지정 용어를 반영.
   - `コンテンツサポーター`: `콘텐츠 서포터` ↔ `임무 지원` 양방향 전환 성공.
   - `踊り子`: `무도가` ↔ `댄서` 양방향 전환 성공 (Baseline의 일본어 원문 `踊り子` 누출 완전 해소).
2. **일반 번역 회귀 해소 (`a100` → `a65`)**:
   - EN→KO `"Nice work everyone, that went much smoother than expected!"`:
     - Baseline: `모두 잘해 주셨습니다. 예상보다 훨씬 더 원활하게 진행되었습니다!`
     - `r7-a100`: `모두 잘해줘서 고맙다!` (후반절 누락 + 반말화)
     - **`r7-a65`**: `모두 잘해 주셔서 기대 이상으로 훨씬 더 원활하게 진행되었습니다!` (완전 복원)
   - EN→KO `"Could you share the link to the document when you have a moment?"`:
     - `r7-a100`: `잠시만 시간을 내주시면 문서를 링크해 드릴 수 있을까요?` (주어/행위자 전도)
     - **`r7-a65`**: `잠시 시간을 내주시면 문서 링크를 공유해 주실 수 있나요?` (완전 복원)
   - FR→KO `"Merci beaucoup pour votre patience et votre aide."`:
     - `r7-a100`: `많은 양의 기다림과 도움을 주셔서 감사합니다.` (직역 어색함)
     - **`r7-a65`**: `당신의 인내와 도움에 진심으로 감사드립니다.` (Baseline 100% 복원)
   - Hold-out EN→KO 2문장 대사(지명/시설 설명): `r7-a100`에서 발생한 2번째 문장 누락·오역이 `r7-a65`에서 완전히 복원되고, 기준선 대비 용어 정합성도 개선됨.

---

## 5. 해석 (Interpretation)

1. **Glossary-Conditioned SFT 레시피 검증 완료**: 무합성 1차 데이터(`ffxiv-datamining` + Koala + Counterfactual Swapping + Distractor)만으로도 1B 모델이 용어집을 **단순 암기가 아닌 런타임 조건부 제약**으로 학습했다(Counterfactual 스왑 0/8 → 8/8, Distractor 오삽입 0/25).
2. **`α = 1.0`에서의 산문 드리프트 원인과 선형 병합 효과**: `r7`은 `lr=5e-5 × 2 epochs`에 Axis-3 Replay 비중이 16%(3,325/20,825)였고 `FR->KO` replay가 누락(0건)되어, 전체 delta(`α=1.0`)를 그대로 합치면 FFXIV NPC 대화체(짧은 반말/단문 압축)로 분포가 다소 과이동했다. LoRA delta를 `α = 0.65`로 스케일링해 병합하자 용어집 순응력(`9/9`, `8/8`)은 100% 유지하면서 일반 EN/FR/KO 산문 충실도가 Baseline 수준으로 복원되었다.
3. **양자화 민감도 (`Q8_0` vs `Q4_K_M`)**: `r7-a65`에서 `Q8_0`은 Counterfactual `8/8`, `Q4_K_M`은 `6/8`로 소폭 하락했다. 1B 모델의 경우 `Q8_0`(1.07 GB)이 `Q4_K_M`(806 MB) 대비 용량 차이는 ~260 MB에 불과하므로, 1B는 `Q8_0` 기본 권장(저사양은 `Q4_K_M`)이 합리적이다.

---

## 6. 한계 및 주의사항 (Limitations)

1. **Hold-out Eval 규모(`n=15`, 용어 포함 9항목)**: 초기 실제 제보 18건 중 격리된 15건이므로 통계적 분산이 크다. 추후 D1 제보가 50~100건 이상 누적되면 정식 hold-out 세트를 확장해야 한다.
2. **`FR->KO` Axis-3 Replay 누락**: 이번 `replay_general.jsonl` 생성 시 `opus-100` 언어 쌍 이름(`en-fr` vs `fr-en`) 버그로 `FR->KO` replay가 0건이었다(이후 [generate_replay.py](file:///d:/REPO/IronworksTranslator/tools/milmmt-dataset-builder/generate_replay.py)는 수정 완료). 다음 학습(4B 또는 1B v2) 시 `replay_general.jsonl`을 재생성해 `FR->KO`를 포함하고 Replay 비중을 25% 이상으로 높이는 것이 바람직하다.
3. **초단문 일본어 구어/신조어 은어**: 1B 베이스 모델과 FFXIV 공식/코알라 데이터 모두에 없는 초단문 히라가나 구어체(hold-out 일부 항목)는 여전히 1B 한계로 오역이 남는다. 이는 `4B` 모델 확장 + 추후 실사용 제보 누적/구어체 강화로 해결해야 할 영역이다.
4. **로컬 실행 도구 vs 앱 통합**: 본 실험은 `llama-server.exe`에서 앱과 동일한 디코딩 파라미터로 검증했으나, 아직 `IronworksTranslator` 앱 본체에 `SupportsGlossaryPrompt` 및 런타임 용어 매칭(Step 4)을 통합해 실제 인게임 UI에서 엔드투엔드 확인하는 단계는 남아 있다.

---

## 7. 다음 결정 (Next Decisions)

- **권장안 A (즉시 진행 가능) — `MiLMMT-46-4B-v1.0` 동일 레시피 확장**:
  - 수정된 `generate_replay.py`로 `FR->KO` 포함 Replay(~5,000건, 22~25% 비중)를 보강하고, `lr=3e-5` 또는 `α` 튜닝 포함해 4B LoRA SFT 실행 (로컬 RTX 4080 SUPER에서 masked-only loss + grad ckpt, 또는 Colab CLI `L4` 24 GB / Colab 웹 UI `G4`/`A100`).
- **권장안 B — 앱 & 서버 Step 4 통합 (`SupportsGlossaryPrompt` + `/v2/glossary`)**:
  - 검증된 `r7-a65.Q8_0.gguf`를 실제 IronworksTranslator 앱에 연결할 수 있도록 `MiLMMTTranslator`의 런타임 용어집 매처(카타카나 경계 검사 + `Glossary:` 프롬프트 조건부 활성화)와 `ironworks-db-endpoint`의 `GET /v2/glossary` 배포 파이프라인을 구현.
- **권장안 C — 현재 코드/문서/툴링 정리 커밋**:
  - `MiLMMTTranslator.cs` Step 0 수정 + 단위 테스트(283 passed), 3개 Python 도구(`tools/milmmt-*`, 17 passed), 설계·실험 리포트를 깔끔하게 커밋 (비공개 데이터/모델 `artifacts/`는 `.gitignore`로 완전 제외).
