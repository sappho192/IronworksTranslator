# MiLMMT 사후학습 및 번역 교정 데이터 활용 계획

- **작성일**: 2026-11-26
- **대상 모델**: `xiaomi-research/MiLMMT-46-1B-v1.0` (Gemma3-1B 기반, CPT → SFT → GRPO RL → Linear Merge)
- **연계 인프라**:
  - 클라이언트: `sappho192/IronworksTranslator` ([PR #93](https://github.com/sappho192/IronworksTranslator/pull/93), 머지 완료)
  - 백엔드: `sappho192/ironworks-db-endpoint` ([PR #1](https://github.com/sappho192/ironworks-db-endpoint/pull/1), Cloudflare D1 `ironworks-reports` 배포 완료)

---

## 1. 배경 및 현재 수집 인프라

### 모델 특성 요약
- `MiLMMT-46-1B-v1.0`은 시스템 프롬프트나 용어집 슬롯 없이 고정 프롬프트(`template: empty`, `add_special_tokens=False`)로만 학습되어 있다.
- 따라서 **원본 `v1.0`에 용어집 프롬프트를 zero-shot으로 끼워 넣는 방식은 안정적으로 동작하지 않는다.**
- 1B 파라미터 용량 특성상 희귀 게임 고유명사를 가중치에 전부 암기시키기 어렵고, 소량 도메인 데이터로 Full SFT를 수행하면 일반 번역 품질이 쉽게 저하된다.
- 가장 현실적인 전략은 **1회 Glossary-conditioned LoRA SFT로 "용어집을 참고해 번역하는 능력"을 부여**한 뒤, 패치마다 모델 재학습 없이 **용어집 JSON만 갱신**하고, `MiLMMT` 오역이 충분히 쌓이면 **CPO/DPO**로 직역·말투 습관을 보강하는 구조다.

### 수집 데이터 스키마 (`translation_reports`, Cloudflare D1)
현재 `POST https://biggs.sapphosound.com/v2/reports`로 수집되는 핵심 컬럼은 다음과 같다.

| 컬럼 | 역할 | 학습/활용 시 의미 |
| --- | --- | --- |
| `translation_kind` | `'chat' \| 'dialogue' \| 'manual'` | 채팅/NPC 대사 구분 |
| `npc_speaker` | NPC 원문 이름 (`dialogue`만 저장, `chat`/`manual`은 서버에서 `NULL` 강제) | NPC 말투 일관성 평가·학습 |
| `source_language`, `target_language` | 언어 쌍 | 언어 쌍별 학습셋 분리 |
| `source_text` | 원문 | 입력 프롬프트 |
| `engine`, `model_id`, `model_sha256` | 번역 엔진 및 MiLMMT 모델 식별자 | `MiLMMT` 오역(`rejected`) 필터링 |
| `original_translation` | 사용자가 본 원래 번역 | CPO/DPO `rejected` 후보 |
| `suggested_translation` | 사용자가 제안한 번역 | SFT 정답 / CPO `chosen` 후보 |
| `draft_source`, `draft_edited` | 초안 출처(`current`/`papago`/`deepl`) 및 직접 수정 여부(`0`/`1`) | 사람 교정본 vs 타 번역기 무수정본 분리 |
| `glossary_terms_json` | `[{"source": "...", "target": "..."}]` | FFXIV 용어집 후보 |
| `comment` | 맥락 메모 | 검수 참고용 (학습 프롬프트 미포함) |
| `review_status` | `'pending' \| 'approved' \| 'rejected'` (기본 `pending`) | 미검수/위조 요청 차단 게이트 |

---

## 2. 수집 데이터에서 도출되는 3가지 자산

1. **FFXIV 용어집 자산 (`glossary`)**
   - `review_status = 'approved'` 레코드의 `glossary_terms_json`을 집계해 `(source_language, target_language, source, target)` 사전으로 정제한다.
   - 출현 빈도와 최근 검수 상태를 함께 관리해 충돌(동일 원문에 다른 번역) 시 수동 확정한다.

2. **SFT 병렬 데이터 (`sft`)**
   - `review_status = 'approved' AND suggested_translation IS NOT NULL` 레코드에서 `(source_text, suggested_translation)` 쌍을 추출한다.
   - **품질·약관 필터링 정책**:
     - `draft_edited = 1`: 사람이 FFXIV 문맥에 맞게 다듬은 핵심 학습셋.
     - `draft_source IN ('papago', 'deepl') AND draft_edited = 0`: 타 번역기 출력을 무수정 전송한 레코드. 외부 번역기 약관 및 번역투 오염 리스크를 고려해 **기본 학습셋에서는 제외**하고, 필요 시 용어집 참고·평가 보조 용도로만 분리 보관한다.

3. **CPO / DPO 선호도 쌍 (`preference`)**
   - `review_status = 'approved' AND engine = 'MiLMMT' AND suggested_translation IS NOT NULL AND suggested_translation != original_translation` 조건을 만족하는 레코드를 `(prompt, chosen=suggested_translation, rejected=original_translation)`으로 변환한다.
   - `model_sha256`를 함께 기록해 두면 추후 특정 릴리스(`v1.0` vs `v1.1-ffxiv`)의 오역만 골라 학습할 수 있다.

---

## 3. 데이터 규모별 단계적 활용 로드맵

| 검수 누적 규모 | 권장 활용 | 목표 및 기대 효과 |
| --- | --- | --- |
| **~100건 (초기)** | **평가셋(Benchmark) + 용어집 JSON 축적** | 학습 전 `MiLMMT-46-1B-v1.0`의 오역률·용어 일치율 기준선(baseline) 확보, 빈출 오역 패턴 파악 |
| **300~1,000건** | **Glossary-conditioned LoRA SFT (1차)** | `v1.0`에 용어집 추종 능력 학습 (+ 일반 번역 데이터 10~20% 혼합으로 망각 방지). 이후 패치부터는 용어집 JSON 갱신만으로 신규 고유명사 대응 |
| **1,000건+ (MiLMMT 오역 다수 누적)** | **CPO/DPO 보강 + 필요 시 Linear Merge** | `original_translation(rejected)` vs `suggested_translation(chosen)`으로 직역·말투·용어 누락 교정; 일반 품질 저하 시 원본 `v1.0`과 선형 병합 비율 조절 |

### 초기 부트스트랩 전략
- 사용자 제보만으로 처음 수백~수천 건이 모이기까지는 시간이 걸린다.
- 따라서 **1차 Glossary-SFT**는 기존에 확보 가능한 FFXIV 다국어 텍스트/용어집(또는 일반 병렬 데이터 + 자동 용어 매칭 합성 데이터)으로 먼저 "용어집 추종 능력"을 학습해 두고, 사용자 제보는 **평가셋(hold-out) + 고부가가치 교정(SFT/CPO)**으로 얹는 편이 초기 체감 개선이 훨씬 빠르다.

---

## 4. 상세 설계 선택지 3가지

아래 3가지 축은 서로 독립적으로 착수할 수 있으며, 필요 시 단계적으로 연결한다.

### 선택지 1: D1 → 검수 → SFT / CPO / 용어집 JSONL 추출 파이프라인

#### 1-1. 검수 워크플로 (`pending` → `approved` / `rejected`)
- **안 A (로컬 CLI 스크립트, 최소 구현)**: `wrangler d1 execute ironworks-reports --remote --json`으로 `pending` 레코드를 내려받아 터미널에서 1건씩 확인 후 `approved`/`rejected` SQL을 일괄 반영. 인프라 추가 없음.
- **안 B (경량 관리자 엔드포인트 + Cloudflare Access)**: `ironworks-db-endpoint`에 `/admin/reports` 조회·상태 변경 엔드포인트를 두고 Cloudflare Access(Zero Trust 이메일 인증)로 보호. 브라우저에서 바로 검수 가능.

#### 1-2. 추출 스크립트 출력물 (`export_datasets.py` 등)
- `eval.jsonl`: 최초 검수분 일부(예: 100~200건, 언어 쌍·`translation_kind` 층화 추출)를 고정 평가셋으로 격리. **학습셋과 절대 겹치지 않도록 `id` 또는 정규화 `source_text` 해시로 제외.**
- `sft.jsonl`: `review_status = 'approved' AND suggested_translation IS NOT NULL AND (draft_edited = 1 OR draft_source = 'current')`
- `cpo.jsonl`: `review_status = 'approved' AND engine = 'MiLMMT' AND suggested_translation IS NOT NULL AND suggested_translation != original_translation AND (draft_edited = 1 OR draft_source = 'current')`
- `glossary.json`: `review_status = 'approved'`의 `glossary_terms_json` 집계·정규화 결과.

#### 1-3. 정제 규칙
- 동일 `(source_language, target_language, normalized(source_text))` 중복은 최신 `approved` 1건만 유지.
- 플레이어 닉네임 등 민감 문자열 패턴이 원문에 포함된 경우 검수 단계에서 마스킹 또는 `rejected` 처리.
- D1 무료 티어(5GB) 관리: 추출 시 아카이브(로컬/R2) 백업 후 처리 완료된 오래된 `rejected` 레코드는 주기적으로 정리 가능.

---

### 선택지 2: Glossary-conditioned LoRA SFT 프롬프트 포맷 & LlamaFactory → GGUF 레시피

#### 2-1. 프롬프트 포맷 설계
현재 `MiLMMTTranslator.RenderPrompt` (`src/IronworksTranslator/Utils/Translator/MiLMMTTranslator.cs`)는 아래 고정 형식을 사용한다.

```text
Translate this from {SourceName} to {TargetName}:
{SourceName}: {Text}
{TargetName}:
```

Glossary-SFT에서는 기존 형식을 최대한 보존하면서, **매칭된 용어가 있을 때만** 상단에 최소한의 `Glossary:` 블록을 삽입한다.

```text
Glossary:
暁の血盟 => 새벽의 혈맹
光の戦士 => 빛의 전사

Translate this from Japanese to Korean:
Japanese: 暁の血盟へようこそ、光の戦士。
Korean:
```

- **용어집이 없는 경우**: 기존 `RenderPrompt`와 완전히 동일한 형식을 유지해 하위 호환성과 일반 번역 능력을 지킨다.
- **과적합·과의존 방지 학습 데이터 비율**:
  - 60~70%: 원문에 실제로 등장하는 용어 1~8개를 포함한 게임 병렬 샘플
  - 15~20%: 용어집 블록 없이 학습하는 게임/일반 병렬 샘플 (용어집 없이도 정상 동작)
  - 10~15%: 무관한(distractor) 용어 1~2개를 섞은 샘플 (원문에 없는 용어를 억지로 출력하지 않도록 훈련)
  - 전체 중 10~20%는 일반 도메인 병렬 번역 데이터 혼합 (catastrophic forgetting 방지)

#### 2-2. LlamaFactory 학습 및 변환 레시피 (권장 초안)
- **기반 모델**: `xiaomi-research/MiLMMT-46-1B-v1.0`
- **필수 토크나이저/템플릿 설정**: `template: empty`, `add_special_tokens: false` (업스트림 레시피 유지)
- **LoRA SFT 하이퍼파라미터**:
  - `lora_rank`: `16`~`32`, `lora_alpha`: `32`~`64`, `lora_target`: `all`
  - `learning_rate`: `5e-5`~`1e-4`, `lr_scheduler_type`: `cosine`, `warmup_ratio`: `0.05`
  - `num_train_epochs`: `1`~`3` (eval loss / 용어 일치율 기준 early stop)
  - `bf16: true` (또는 `fp16`), `cutoff_len: 1024` (`ContextSize: 2048` 대비 충분)
- **CPO / DPO 보강 (2단계, 선택)**:
  - SFT 병합 체크포인트 위에 낮은 학습률(`5e-6`~`1e-5`), `beta: 0.1`, `1` epoch 내외로 `cpo.jsonl` 적용.
- **배포 파이프라인**:
  1. LoRA 가중치 병합 (`llamafactory-cli export`) — 일반 품질 저하 시 원본 `v1.0`과 선형 보간(예: 0.7:0.3) 적용.
  2. `llama.cpp` `convert_hf_to_gguf.py` → `llama-quantize` (`Q4_K_M` / `Q8_0` 등 기존 프로필 양자화 규격 맞춤).
  3. `eval.jsonl`로 `v1.0` 대비 **용어 정확도(Term Accuracy)**, **태그/구두점 보존율**, **일반 문장 회귀 여부** 비교.
  4. 통과 시 SHA-256 산출 후 `MiLMMTModelProfiles`에 신규 프로필 추가.

---

### 선택지 3: 앱 내 용어집 동기화·주입 구조

#### 3-1. 서버 배포 (`ironworks-db-endpoint` 확장 또는 정적 JSON)
- `ironworks-db-endpoint`에 `GET /v2/glossary?src=Japanese&dst=Korean` (또는 언어쌍 전체를 담은 단일 `glossary.v1.json`) 추가.
- `ETag` / `Cache-Control` 헤더로 앱 기동 시 변경분이 있을 때만 경량 동기화하고, 로컬 캐시(`%LOCALAPPDATA%\IronworksTranslator\glossary\`)에 저장해 오프라인·서버 장애 시에도 번역이 막히지 않게 한다.
- JSON 스키마 예시:
  ```json
  {
    "version": "2026-11-26.1",
    "pairs": {
      "Japanese->Korean": [
        { "source": "暁の血盟", "target": "새벽의 혈맹" }
      ],
      "English->Korean": [
        { "source": "Scions of the Seventh Dawn", "target": "새벽의 혈맹" }
      ]
    }
  }
  ```

#### 3-2. 클라이언트 매칭 및 주입 (`MiLMMTTranslator`)
- **매칭 방식**: 원문에 부분 일치하는 용어만 최장 일치(longest-match) 우선으로 최대 8~10개 추출 (Aho-Corasick 또는 길이 내림차순 매칭; 용어 수천 건 수준에서도 마이크로초 단위).
- **모델 호환성 게이트**: 원본 `MiLMMT-46-1B-v1.0`은 zero-shot 용어집을 안정적으로 따르지 못하므로, **`MiLMMTModelProfile`에 `SupportsGlossaryPrompt` 플래그를 두고 Glossary-SFT 모델 프로필에서만 주입을 활성화**한다.
- **캐시 키 갱신**: `MiLMMTTranslator.TranslationCacheKey`에 용어집 버전(또는 매칭된 용어 해시)을 포함시켜, 용어집이 갱신되면 이전 캐시가 재사용되지 않도록 보장한다.
- **사용자 커스텀 용어집(선택)**: 원격 공식 용어집 외에 로컬 사용자 용어 파일 오버라이드도 동일 매칭기에 합쳐 넣을 수 있다.

---

## 5. 권장 진행 순서 및 미결 의사결정

1. **단기 (지금~100건 수집 구간)**: **선택지 1**의 로컬 검수/추출 CLI와 고정 평가셋(`eval.jsonl`) 포맷을 먼저 확정.
2. **중기 (부트스트랩 또는 300건+ 도달 시)**: **선택지 2**로 첫 Glossary-conditioned LoRA SFT 실험 → 평가셋으로 `v1.0` 대비 개선 검증 → GGUF 패키징.
3. **배포 연동**: 새 모델 프로필이 검증되면 **선택지 3**의 용어집 동기화·조건부 프롬프트 주입을 앱과 `ironworks-db-endpoint`에 연결.

### 다음 결정 필요 사항
- 검수 도구를 **로컬 CLI(안 A)**로 가볍게 시작할지, **Cloudflare Access 기반 웹 UI(안 B)**로 둘지
- 1차 Glossary-SFT를 **사용자 제보 누적 후** 진행할지, **공개 FFXIV 텍스트/용어집 부트스트랩**으로 먼저 실험할지
- 타 번역기 무수정본(`draft_edited = 0`)을 학습셋에서 **완전 제외**할지, **별도 파일로만 분리**할지
