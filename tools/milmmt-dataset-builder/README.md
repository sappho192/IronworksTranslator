# MiLMMT SFT Dataset Builder & Colab Pro+ Training Kit

`MiLMMT-46-1B-v1.0` (1차) 및 `MiLMMT-46-4B-v1.0` (2차) 사후학습용 **무합성·비대칭 다국어 Glossary-SFT 데이터셋 생성기**, **GGUF 정량 평가 하니스**, **Colab Pro+ LlamaFactory 설정**입니다.

> 생성된 학습/평가 JSONL은 `.gitignore` 대상인 `artifacts/dataset/`에 로컬 비공개로 유지하며 절대 Git/HF에 커밋하지 않습니다.

## 검증된 토크나이저 규격 (`MiLMMT-46-1B-v1.0.Q8_0.gguf` 실측 완료)
- `template: empty`, `add_special_tokens: false` (`<bos>` 미추가)
- 프롬프트 끝 `{TargetName}:` 직후 **선행 공백 없이** 바로 번역 텍스트 시작
- 종료 토큰은 `<eos>` (`id = 1`)

## 1. (선택) Colab Pro+에서 Axis-3 일반 번역 Replay 생성
외부 저품질 코퍼스 대신 FLORES `dev`(+Tatoeba) 원문을 `MiLMMT-46-12B-v1.0`으로 번역해 망각 방지용 Replay를 생성합니다 (`devtest`는 최종 평가용으로 격리).

```bash
python generate_replay.py \
  --model xiaomi-research/MiLMMT-46-12B-v1.0 \
  --total-samples 4500 \
  --out-jsonl /content/drive/MyDrive/ironworks-milmmt/replay_general.jsonl
```

## 2. 로컬 데이터셋 생성

```powershell
python tools\milmmt-dataset-builder\build_sft_dataset.py `
  --datamining-dir D:\REPO\ffxiv-datamining `
  --glossary-full artifacts\glossary\glossary.full.json `
  --community-curated tools\milmmt-glossary-builder\community_curated.json `
  --eval-jsonl artifacts\curation\exports\2026-11-27.1\eval.jsonl `
  --total-samples 22000 `
  --out-dir artifacts\dataset\2026-11-27.1
```

On-policy 일반 번역 Replay(축 3)가 준비되면 `--replay-jsonl path\to\replay.jsonl`을 추가합니다.

## 2. GGUF 베이스라인 / 학습 후 정량 평가

```powershell
python tools\milmmt-dataset-builder\eval_gguf.py `
  --model "$env:LOCALAPPDATA\IronworksTranslator\data\model\milmmt-46-1b-v1.0\MiLMMT-46-1B-v1.0.Q8_0.gguf" `
  --eval-jsonl artifacts\curation\exports\2026-11-27.1\eval.jsonl `
  --out-json artifacts\dataset\eval_baseline_1b_v1.0.json
```

## 3. Colab Pro+ (A100/L4) LlamaFactory 학습 실행
1. Google Drive(비공개 폴더)에 `train_sft.jsonl`, `val_sft.jsonl` 업로드
2. LlamaFactory `data/dataset_info.json`에 `ironworks_sft_train` / `ironworks_sft_val` 등록 (Alpaca 포맷: `instruction`, `input`, `output`)
3. `llamafactory-cli train colab_train_1b.yaml` 실행 (~15~25분 on A100)
4. `llamafactory-cli export`로 LoRA 병합 → `llama.cpp` `convert_hf_to_gguf.py` → `llama-quantize` (`Q8_0` / `Q4_K_M`)
5. 로컬 `eval_gguf.py --with-glossary`로 5개 배포 게이트 검증 후 통과 시 `colab_train_4b.yaml`로 4B 확장

## 테스트
```powershell
python -m unittest discover -s tools\milmmt-dataset-builder -v
```
