# MiLMMT Report Curator CLI

Cloudflare D1(`ironworks-reports`)에 수집된 사용자 번역 개선 제보를 로컬에서 검수하고, 학습/평가/용어집 데이터셋으로 추출하는 CLI입니다. (Python 3.10+ 표준 라이브러리만 사용)

> **프라이버시 주의**: 내려받은 원본 제보(`raw/`), 검수 로그(`curated/`), 추출 데이터(`exports/`)는 기본 경로인 `artifacts/curation/`(`.gitignore` 대상)에 저장하며 **절대 Git에 커밋하지 않습니다.**

## 워크플로

### 1. 원격 D1에서 제보 내려받기 (`pull`)
```powershell
python tools\milmmt-report-curator\curate_reports.py pull
```
이미 캡처해 둔 wrangler JSON 파일에서 불러올 때:
```powershell
python tools\milmmt-report-curator\curate_reports.py pull --from-json path\to\d1_dump.json
```

### 2. 대기열·배지·용어 힌트 요약 확인 (`summary`)
```powershell
python tools\milmmt-report-curator\curate_reports.py summary --verbose
```

### 3. 대화형 검수 (`review`) 또는 일괄 반영 (`apply`)
```powershell
python tools\milmmt-report-curator\curate_reports.py review
```
- 원본 `suggested_translation`은 유지하고, 문체·공식 용어 미세 교정 시 `curated_translation`을 별도로 기록합니다.
- `draft_source IN ('papago', 'deepl') AND draft_edited = 0` 레코드는 자동으로 `REFERENCE_ONLY` 배지와 기본 split(`reference_only`)이 지정됩니다.
- `artifacts/glossary/glossary.full.json`이 있으면 원문에서 매칭된 공식 용어집 후보와 diff 구간을 자동으로 함께 보여줍니다.

### 4. 원격 D1 `review_status` 동기화 (`sync-status`)
```powershell
# 먼저 DRY-RUN으로 SQL 확인
python tools\milmmt-report-curator\curate_reports.py sync-status

# 확인 후 실제 반영
python tools\milmmt-report-curator\curate_reports.py sync-status --execute
```

### 5. 데이터셋 및 Tier-0 용어집 추출 (`export`)
```powershell
python tools\milmmt-report-curator\curate_reports.py export --version 2026-11-27.1
```
출력물 (`artifacts/curation/exports/<version>/`):
- `eval.jsonl`: 고정 평가셋 (`source_hash` 기준으로 학습셋과 영구 격리)
- `sft_user.jsonl`: 사람 교정본 기반 SFT 데이터
- `cpo_user.jsonl`: `engine='MiLMMT'` 선호도 쌍 (`model_sha256`별 중복 제거 키 분리)
- `reference_only.jsonl`: 타 번역기 무수정본(`draft_edited = 0`) 분리 보관
- `overrides.json`: `build_glossary.py --overrides-json`에 바로 투입 가능한 Tier-0 용어집
- `export_summary.json`: 추출 통계 및 데이터 누수(`eval_train_overlap == 0`) 검증 결과

## 테스트
```powershell
python -m unittest discover -s tools\milmmt-report-curator -v
```
