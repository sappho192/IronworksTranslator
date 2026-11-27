# MiLMMT 학습 데이터 구축 및 사후학습 실행 청사진

- **작성일**: 2026-11-27
- **선행 문서**: [2026-11-26-milmmt-post-training-and-data-utilization-plan.md](../2026-11-26/2026-11-26-milmmt-post-training-and-data-utilization-plan.md)
- **대상 모델**:
  - 1차 검증: `xiaomi-research/MiLMMT-46-1B-v1.0` (`Q8_0`, `Q4_K_M`)
  - 2차 확장: `xiaomi-research/MiLMMT-46-4B-v1.0` (1차 검증 통과 후 동일 데이터·프롬프트 레시피 적용)
- **연계 저장소**:
  - 클라이언트: `sappho192/IronworksTranslator`
  - 백엔드/DB: `sappho192/ironworks-db-endpoint` (Cloudflare D1 `ironworks-reports`)

---

## 1. 초기 제보(18건) 분석 결과 및 확정된 의사결정

### 1-1. 1일차 수집 데이터(18건) 핵심 진단
`2026-11-26` 배포 직후 `translation_reports`에 수집된 18건(`review_status = pending`)을 분석한 결과, 기존 계획에서 예상했던 "고유명사/용어집 오역" 외에 **실전 채팅 특유의 실패 패턴**이 분명하게 드러났다.

| 지표 / 유형 | 관측 결과 | 설계 반영 사항 |
| --- | --- | --- |
| **언어·유형 분포** | `JA→KO` 채팅 16건(`v2.3.0`), `EN→KO` 대사 2건(개발 테스트 `4B-v0.1`) | `JA→KO` + `EN→KO`를 1차 핵심 언어 쌍으로 집중 강화하되 5개 언어(`JA/EN/DE/FR/KO`) 유지 |
| **FFXIV 고유명사·UI 용어** | 11건 (유니크 9문장, 약 56%) — 지명, 직업명, UI/시스템 용어, 아이템 카타카나 오역 | Glossary-conditioned SFT + 공식 용어집 동기화가 가장 큰 체감 개선 축 |
| **태그/약어 혼재·언어 이탈** | 영문 약어(`G.A.T.E.`)·특수 괄호(`『』`) 혼재 시 일본어 원문 잔존 + 영어 혼출 발생 | 용어집 데이터에 영문 약어·수량/등급 태그(`[HQ]×1`)·게임 괄호 포함 |
| **구어체·초단문 채팅 오역** | 6건 (약 33%) — 1~5자 감탄사, 구어 축약/오타, 일상 카타카나, 화자 방향 반전 | **용어집만으로는 해결 불가** → 구어체·초단문 채팅 축을 SFT 데이터에 필수 편입 |
| **초단문 반복 폭주** | 2건 — 짧은 감탄사 입력에서 `ChatMaxTokens=64` 끝까지 동일 문자 무한 반복 | 학습과 별개로 **Step 0 추론기 디코딩 파라미터(`RepeatPenalty`)**로 즉시 완화 검증 |
| **용어 힌트·메모 입력률** | `glossary_terms_json` 0/18건(0%), `comment` 0/18건(0%) | 검수 CLI에서 `(source, original, suggested)` **diff 기반 용어 후보 반자동 추출** 필수 |
| **타 번역기 무수정본** | `draft_source=papago, draft_edited=0` 1건 실측 | 기본 학습셋 제외 + `reference_only.jsonl` 별도 분리 정책 유효성 확인 |
| **엔진별 중복 원문** | 동일 시스템 문장이 `Papago`와 `MiLMMT` 양쪽에서 각각 제보됨 | **SFT와 CPO의 중복 제거 키를 분리**해 `MiLMMT`의 `rejected` 신호 보존 |

### 1-2. 확정된 4가지 방향성
1. **데이터셋 전략**: **하이브리드 부트스트랩** — ① FFXIV 용어·시스템/대사 + ② 일본어 구어체/초단문 채팅 + ③ 일반 병렬 데이터의 3축 혼합으로 1차 학습을 진행하고, 사용자 제보는 고정 평가셋(`eval.jsonl`)과 고부가 교정(SFT/CPO)에 우선 투입한다.
2. **검수·추출 도구**: **로컬 CLI 스크립트**부터 시작한다(D1 조회 → diff 기반 용어 후보 추출 → `curated_translation` 검수 → JSONL/용어집 추출).
3. **타 번역기 무수정본(`draft_edited = 0`)**: 기본 SFT/CPO 학습셋에서는 제외하고 `reference_only.jsonl`로 별도 분리 보관한다(용어 후보 추출 및 평가 참고용).
4. **모델 범위**: **`MiLMMT-46-1B-v1.0`에서 먼저 전체 파이프라인과 평가 게이트를 검증**한 뒤, 검증된 동일 데이터·프롬프트 레시피로 **`MiLMMT-46-4B-v1.0`에 확장**한다.

---

## 2. Step 0 — 추론기 단기 개선 (학습 전 독립 검증)

학습 데이터만으로는 추론 시 순수 Greedy 디코딩의 반복 루프를 완전히 차단하기 어렵고, 반대로 추론기 수정만으로 해결되는 문제를 학습 효과와 섞어 측정하면 안 된다.

### 현재 원인
- `src/IronworksTranslator/Utils/Translator/MiLMMTTranslator.cs`의 `CreateInferenceParams`는 `Temperature = 0.0f, TopK = 1, TopP = 1.0f, MinP = 0.0f`만 설정하며, LLamaSharp `DefaultSamplingPipeline`의 반복 페널티(`RepeatPenalty`, `PenaltyCount` 등)를 지정하지 않는다.
- 이로 인해 짧은 감탄사 입력에서 `ChatMaxTokens = 64` 상한까지 동일 토큰이 반복 출력된다.

### 실험 및 확정 결과 (2026-11-27 실측 반영 완료)
`MiLMMT-46-1B-v1.0.Q8_0.gguf` 실모델에서 64토큰 폭주 사례(초단문 감탄사 반복 4종)와 일반 문장·숫자 포함 시스템 로그·긴 영문 대사를 대상으로 `RepeatPenalty`와 `FrequencyPenalty` 조합을 실측 비교한 결과:
- **`RepeatPenalty > 1.0` (1.10~1.15) 부작용 확인**: 프롬프트 토큰까지 페널티 창에 포함되어 시스템 메시지의 숫자(`20` → `#19`)가 변조되고 문장 끝 느낌표(`!`)가 탈락하는 회귀가 발생함.
- **최적 설정 확정 (`RepeatPenalty = 1.0f`, `FrequencyPenalty = 0.30f`, `PresencePenalty = 0.0f`, `PenaltyCount = 16`)**:
  - 모든 64토큰 반복 폭주가 5~6토큰(`아아아아아`, `아아아아!`)에서 정상 종료됨.
  - 숫자(`20`, `100,000`), 문장부호(`!`, `,`), 의도적 반복(`テスト、テスト`), 긴 영문 NPC 대사 모두 회귀 없이 보존됨.
- `MiLMMTTranslator.CreateInferenceParams`에 반영 및 단위 테스트 추가 완료.

---

## 3. Step 1 — 로컬 검수·데이터셋 추출 CLI 설계

### 3-1. 데이터 격리 및 프라이버시 원칙
- 원격 D1에서 내려받은 원본 제보, 플레이어 채팅 텍스트, 검수 작업 파일은 **절대 Git 저장소에 커밋하지 않는다.**
- 작업 디렉터리 예시는 `.gitignore`에 포함되는 로컬 전용 경로(예: `local-data/milmmt-pipeline/` 또는 `%LOCALAPPDATA%\IronworksTranslator-Training\`)로 고정한다.
- 공개 저장소/문서에는 집계 통계, 실패 유형, 일반화된 시스템/합성 예시만 기록한다.

### 3-2. CLI 하위 명령 구조 (Python 3.11+ 기준, `tools/milmmt-curation/` 예정)

| 명령 | 역할 | 입출력 |
| --- | --- | --- |
| `pull` | `wrangler d1 execute ironworks-reports --remote --json`으로 리포트 스냅샷 조회 | `raw/reports_YYYYMMDD.json` |
| `review` | 미검수(`pending`) 레코드를 1건씩 대화형으로 검수 | `curated/curation_log.jsonl` |
| `sync-status` | 확정된 `approved`/`rejected` 상태만 원격 D1 `review_status`에 일괄 반영 | D1 `UPDATE` SQL 실행 |
| `export` | 검수 로그 + 원본 스냅샷을 결합해 학습/평가/용어집 아티팩트 생성 | `exports/<version>/*.jsonl`, `glossary.json` |

### 3-3. `review` 단계 상세 동작
1. **필드 표시**: `id`, `app_version`, `translation_kind`, `npc_speaker`, `source_language -> target_language`, `engine/model_id`, `draft_source/draft_edited`, `source_text`, `original_translation`, `suggested_translation`, `glossary_terms_json`, `comment`.
2. **자동 경고 표시**:
   - `draft_source != 'current' AND draft_edited = 0` → `[REFERENCE_ONLY 후보]` 배지 표시
   - 동일 `(source_language, target_language, normalized(source_text))` 기존 검수본 존재 시 비교 출력
   - 닉네임·고유 식별자 등 민감 문자열 의심 시 마스킹/반려 확인 프롬프트
3. **검수 결정 저장 (`curation_log.jsonl`)**:
   - `review_status`: `approved` | `rejected` | `skip`
   - `curated_translation`: 비워두면 `suggested_translation` 사용, 문체/공식 용어 미세 수정 시 검수자 확정본 기록(원본 D1 `suggested_translation`은 훼손하지 않음)
   - `split`: `eval` | `train` | `reference_only`
   - `failure_tags`: `["glossary", "language_drift", "register", "colloquial", "short_repetition", "tag_preservation"]`
4. **diff 기반 반자동 용어 후보 추출**:
   - `glossary_terms_json`이 비어 있어도 `original_translation`과 `curated_translation`의 치환 구간(token/character diff)을 계산하고, 원문의 대응 후보(카타카나/고유명사/괄호 내부 문자열)와 묶어 용어쌍 후보를 제안한다.
   - 검수자는 `[Y/수정/건너뛰기]`로 `confirmed_glossary_terms`를 확정한다.

### 3-4. `export` 산출물 및 중복 제거 규칙

| 파일 | 포함 조건 | 중복 제거 키 | 비고 |
| --- | --- | --- | --- |
| `eval.jsonl` | `split == 'eval'` AND `review_status == 'approved'` | `(src_lang, dst_lang, norm(source_text))` | **최우선 격리**. 여기에 포함된 `id`와 `norm(source_text)` 해시는 모든 학습 파일에서 영구 제외 |
| `sft_user.jsonl` | `split == 'train'` AND `review_status == 'approved'` AND `final_translation != null` AND `(draft_edited == 1 OR draft_source == 'current')` | `(src_lang, dst_lang, norm(source_text))` 최신 1건 | `final_translation = curated_translation ?? suggested_translation` |
| `cpo_user.jsonl` | `split == 'train'` AND `review_status == 'approved'` AND `engine == 'MiLMMT'` AND `final_translation != original_translation` AND `(draft_edited == 1 OR draft_source == 'current')` | `(model_sha256, src_lang, dst_lang, norm(source_text))` | `Papago` 레코드와 충돌하지 않도록 SFT와 별도 키 사용; `model_sha256` 메타 보존 |
| `reference_only.jsonl` | `review_status == 'approved'` AND `draft_source IN ('papago', 'deepl')` AND `draft_edited == 0` | `(src_lang, dst_lang, norm(source_text))` | 기본 SFT/CPO 제외; 용어 후보 추출 및 오역 진단 참고용 |
| `glossary.json` | `review_status == 'approved'`의 `confirmed_glossary_terms` (+ 외부 확정 FFXIV 용어집 병합) | `(src_lang, dst_lang, source)` | 동일 `source`에 다른 `target` 충돌 시 빌드 실패 후 수동 확정 강제 |

---

## 4. Step 2 — 하이브리드 부트스트랩 학습 데이터셋 설계

### 4-0. 확보된 핵심 원천 자산 (2026-11-27 실측 완료)

두 원천 자산이 **공식 게임 데이터**와 **실전 유저 채팅·은어**를 상호 보완하는 구조로 확보되어 있다.

1. **로컬 `D:\REPO\ffxiv-datamining` (`xivapi/ffxiv-datamining` + `csv/ko` 서브모듈 `Ra-Workspace/ffxiv-datamining-ko`)**
   - `ja`, `en`, `de`, `fr`(`csv/<lang>/`) 및 `ko`(`csv/ko/csv/`) 5개 언어가 Row ID 기준 1:1 정렬된 표준 1-헤더 CSV(`xiv-data-oxidizer` 포맷).
   - **공식 용어집 원천**: `ClassJob`(42), `PlaceName`(5,283), `ContentFinderCondition`(856), `Status`(4,625), `Trait`(667), `Action`(44,376), `CraftAction`(432), `Item`(52,799), `ENpcResident`(30,618), `BNpcName`(14,560), `Completion`(1,535), `Addon`(14,368), `Mount`/`Companion`/`TripleTriadCard`/`Title`/`Race`/`Tribe`/`GuardianDeity`/`GrandCompany` 등.
   - **공식 게임 텍스트/대사 원천**: `LogMessage`(8,491), `Balloon`(10,456), `NpcYell`(19,393), `InstanceContentTextData`(10,110), `DefaultTalk`(11,651), `quest/`(261,973), `custom/`(27,861), `cut_scene/`(26,516).
   - **필수 정제 규칙**: 시트별 명시적 컬럼 매핑(`Name`/`Singular`/`Text`), 더미/미번역 행(`ja == ko`, `テスト用...`, EN 열 일본어 잔존) 필터링, FFXIV SE-String 태그(`<If(...)>`, `<Sheet(...)>`, `<Highlight>`) 제거/치환.
2. **Google Drive `파판14` 폴더 (실전 유저 채팅·레이드·공팟 약어 자산)**
   - **`📑코알라의 FF14 日本語 노트`** *(원작자 사용 허가 확보)*: `🟠모집`(77행), `🟡소통`(176행), `🟢기믹리딩`(75행), `🔵용어`(150행) — 실제 일본 파티 인사·응답·공팟 약어·레이드 콜아웃 핵심 원천.
   - **`파판 용어` (Google Docs)**: 한섭↔일섭 레이드/파티 은어 핵심 대응표 (`どまい`, `レディチェ`, `消化`, `フリロ`, `取り抜け`, `クリ目`, `~滅解散`, `被り`, `強攻撃` 등).
   - **`FF14 J클라 게임 용어 정리`**: 글섭 실전 줄임말·1글자 직업 약어·예문(93행).
   - *(구버전 추출본인 `상용구`/`ffac_full.csv`/`ff_item_*.txt`/레이드 대사 시트는 로컬 `ffxiv-datamining` 최신 CSV로 상위 호환.)*

#### 계층형 용어집(`glossary.json`) 우선순위 체계
- **Tier 1 (최우선)**: 검수 확정 사용자 제보 용어 + Drive 실전 채팅/레이드 약어(`파판 용어`, `J클라 용어`, `코알라 노트`)
- **Tier 2 (공식 핵심 용어)**: `ClassJob`, `PlaceName`, `ContentFinderCondition`, `Status`, `Trait`, `GrandCompany`, `Race`, `Tribe`, `GuardianDeity`, `Completion`, `MainCommand`, `GeneralAction`, 핵심 `Addon`
- **Tier 3 (확장 공식 용어)**: `Action`, `CraftAction`, `Item`, `ENpcResident`, `BNpcName`, `Mount`, `Companion`, `TripleTriadCard`, `Title` (일반 명사 오치환 방지를 위해 짧은 1~2자 일반어·고빈도 모호어 필터 적용)
- 충돌 규칙: 상위 Tier가 기본 우선하되, 공식 지명/직업명(예: `PlaceName`/`ClassJob`)과 커뮤니티 음차 표기가 충돌하면 충돌 리포트(`glossary_conflicts.json`)로 분리해 공식 한섭 표기를 우선 검증한다.

### 4-1. 데이터 라이선스 및 배포 위생 원칙
- 원격 D1 원본 제보와 `ffxiv-datamining` 기반 대사 추출 데이터셋 자체는 **로컬 비공개 유지(Git/HF 미커밋)**를 원칙으로 한다.
- 추후 Fine-tuned GGUF 가중치를 공개 배포(Hugging Face 등)할 가능성을 고려해, **1차 부트스트랩은 공식 용어집 + Drive 실전 채팅 표현(허가 확보) + 공개 병렬 코퍼스 + 도메인 포맷 합성/정선 샘플**로 구성한다.
- 특히 `quest/` 26만 행을 통째로 학습하면 1B 모델이 일반 채팅까지 중세 판타지 어투로 과적합되므로, 게임 텍스트를 보조 활용할 때도 `LogMessage`(슬롯 치환)·`Balloon`/`NpcYell`·소량 층화 샘플로 비중을 엄격히 통제한다.
- 이 방식은 신규 패치에서 새 고유명사가 추가돼도 **용어집 JSON만 갱신하면 모델 재학습 없이 즉시 대응**되는 일반화 성능을 확보한다.

### 4-2. 1차 LoRA SFT 3축 데이터 믹스 (목표: 약 20,000~25,000 샘플)

| 축 | 비중 | 목표 샘플 수 | 핵심 구성 및 해결 대상 |
| --- | :---: | :---: | --- |
| **축 1. Glossary-Conditioned 번역** | **50~55%** | 11,000~13,000 | **(1-A) 범용 용어집 추종 (약 8,000~9,000)**<br>• 공개 병렬 코퍼스(OPUS/Tatoeba/WMT/CCMatrix 정제본 등)에서 명사·고유명사·카타카나 대응쌍을 자동 정렬해 `Glossary:` 블록 생성<br>• 언어 비중: `JA→KO` 45%, `EN→KO` 40%, `DE/FR→KO` 및 역방향(`KO→JA/EN`) 15%<br>**(1-B) FFXIV 포맷·용어 특화 (약 3,000~4,000)**<br>• FFXIV 용어집(지명·직업·UI·콘텐츠·아이템명)을 삽입한 합성 시스템 메시지·짧은 안내문·NPC 대화 템플릿 + 검수된 사용자 제보(`sft_user.jsonl`)<br>• **의도적 포함 패턴**: 영문 약어(`G.A.T.E.`, `HQ`, `MGP`), 수량·기호(`[HQ]×1`, `「」`, `『』`), 명령형/평서체 시스템 톤 (`#9`, `#11`, `#12` 유형 집중 대응) |
| **축 2. 일본어 구어체·초단문·일상 채팅** | **25~30%** | 5,500~6,500 | • 언어 비중: `JA→KO` 80%, `EN→KO` 20%<br>• **세부 하위 구성**:<br>  1. **초단문·감탄사·게임 채팅 약어 (25%)**: 1~8자 짧은 발화(인사, 감탄, 수고, 축약어)를 과생성 없이 1~10자로 간결하게 번역<br>  2. **구어 축약·오타 내성 (20%)**: 모음 탈락/장음/말줄임표(`。。`, `…`, `ー`)가 포함된 자연스러운 구어체<br>  3. **일상 카타카나 외래어 (20%)**: 음식·일상·취미 외래어 정확한 음차/번역<br>  4. **화자 방향·문체 보존 (35%)**: `〜てきますね`(내가 ~할게요) vs `〜てください`(~해 주세요), 반말/존댓말 일관성<br>• 출처: 공개 자막/대화 병렬 데이터 정제본 + 검수된 소규모 합성 게임 채팅 쌍 + 사용자 제보 |
| **축 3. 일반 도메인 병렬 (망각 방지)** | **15~20%** | 3,500~4,500 | • **`Glossary:` 블록 없이** 기존 `RenderPrompt` 형식 100% 그대로 구성<br>• `JA`, `EN`, `DE`, `FR`, `KO` 5개 언어 쌍 균형 샘플링<br>• 원본 `v1.0`의 일반 번역 유창성과 다국어 능력 유지 |

### 4-3. 축 1 내부의 용어집 프롬프트 조건 비율
축 1(Glossary-Conditioned) 내부에서는 과의존과 환각을 막기 위해 아래 3가지 형태를 혼합한다.
- **65% — 정합 용어 주입 (`1~8`개)**: 원문에 실제 등장하는 용어만 최장 일치 순으로 제시
- **20% — 빈 용어집 (`Glossary:` 생략)**: 동일 도메인 문장을 용어집 없이 번역(용어집 미매칭 시 fallback 성능 유지)
- **15% — Distractor 혼합 (`정합 0~3개 + 무관 용어 1~2개`)**: 원문에 없는 용어가 함께 들어와도 절대 억지로 출력하지 않도록 훈련

### 4-4. 확정 프롬프트 포맷 및 토크나이저 정합 검증

```text
Glossary:
Limsa Lominsa => 림사 로민사
Mizzenmast => 미젠마스트

Translate this from English to Korean:
English: Welcome to the heart of Limsa Lominsa─the Mizzenmast!
Korean:
```

- 매칭 용어가 0개이면 `Glossary:` 블록과 빈 줄을 완전히 생략해 현재 `MiLMMTTranslator.RenderPrompt`와 바이트 단위로 동일하게 유지한다.
- **학습 착수 전 토크나이저 정합 실측 완료 (`MiLMMT-46-1B-v1.0.Q8_0.gguf`, 2026-11-27)**:
  - `add_bos_token = false` (`<bos>` 미추가 — 업스트림 `add_special_tokens: false`와 일치)
  - 프롬프트 끝 `{TargetName}:` 직후 **선행 공백 없음** (`JA→KO`, `EN→KO`, `KO→EN`, `KO→JA` 전 방향 확인)
  - 종료 토큰은 `<eos>` (`id = 1`)
  - 따라서 LlamaFactory `template: empty`, `add_special_tokens: false`, `instruction` 끝 `{TargetName}:`, `output` 선행 공백 없음으로 직렬화한다.
- **확정된 1차 데이터 정책 (2026-11-27)**:
  - **학습 환경**: Colab Pro+ (A100/L4), 데이터셋은 로컬/개인 Drive 비공개 유지, 모델 가중치(GGUF)만 배포
  - **무합성 우선**: LLM 합성 대사 없이 `ffxiv-datamining` 인간 번역 + `코알라 노트 🟡소통` + On-policy Replay(축 3)로 1차 검증
  - **비대칭 다국어 비율**: `JA→KO` 50%, `EN→KO` 30%, `DE→KO` 5%, `FR→KO` 5%, `KO→JA` 5%, `KO→EN` 5%
  - **Counterfactual Glossary Swapping**: 축 1 정합 샘플의 20%에 글섭 음차↔한섭 공식(또는 동종 고유명사) 반사실 치환을 적용해 용어집 추종 일반화 확보
  - **사전 베이스라인 실측 (`eval.jsonl` 15건, Step 0 `fp=0.30/n=16` 적용 상태)**:
    - 용어집 미주입: `Term Accuracy = 1/9 (11.1%)`, `drift = 0`, `loops = 0`
    - 용어집 Zero-shot 주입: `Term Accuracy = 4/9 (44.4%)`, `drift = 1`, `loops = 0` (원본 `v1.0`의 zero-shot 용어집 한계 정량 확인)

### 4-5. 데이터 품질 필터 (자동 스크립트 게이트)
모든 학습 샘플은 병합 전 아래 정제 필터를 통과해야 한다.
- `eval.jsonl`과 정규화 텍스트 해시 및 높은 n-gram 유사도(예: 문자 5-gram Jaccard ≥ 0.85) 중복 제거(data leakage 차단)
- 타겟 언어 스크립트 검증(`target_language == Korean`일 때 한글 비율 하한 체크, 단 순수 기호/약어 예외 허용)
- 원문 대비 비정상 길이 비율 제거(예: 초단문 원문에 40자 이상 과생성된 노이즈 샘플)
- 동일 문자/어절 4회 이상 비정상 반복 샘플 제거
- `Glossary:`에 포함된 정합 용어의 `target`이 실제 정답 번역에 반영되었는지 일관성 검증(Distractor 제외)

---

## 5. Step 3 — LlamaFactory 학습 · 1B → 4B 확장 · 평가 게이트

### 5-1. 1차 검증 레시피 (`MiLMMT-46-1B-v1.0`)

```yaml
# LlamaFactory LoRA SFT 기준 초안 (1B)
model_name_or_path: xiaomi-research/MiLMMT-46-1B-v1.0
stage: sft
do_train: true
finetuning_type: lora
lora_rank: 16
lora_alpha: 32
lora_dropout: 0.05
lora_target: all

template: empty
add_special_tokens: false
cutoff_len: 1024
train_on_prompt: false

per_device_train_batch_size: 4   # GPU VRAM에 따라 조절 (effective batch = 32~64 유지)
gradient_accumulation_steps: 8
learning_rate: 5.0e-5
lr_scheduler_type: cosine
warmup_ratio: 0.05
num_train_epochs: 2.0
bf16: true
eval_strategy: steps
eval_steps: 100
save_steps: 100
```

- **체크포인트 선정**: validation loss만으로 고르지 않고, 매 100~200 step 체크포인트(또는 epoch 1.0 / 1.5 / 2.0)에 대해 아래 **4분할 평가 게이트**를 돌려 최적 체크포인트를 선택한다.
- **일반 품질 저하 시 안전장치**: LoRA 병합 후 일반 도메인 점수가 기준치 이상 떨어지면 원본 `v1.0`과 선형 보간(`0.8:0.2` 또는 `0.7:0.3`)을 비교 평가한다.

### 5-2. 2차 확장 레시피 (`MiLMMT-46-4B-v1.0`)
- 1B에서 프롬프트 포맷·데이터 비율·평가 게이트가 모두 검증된 뒤, **동일한 데이터셋과 평가셋**을 그대로 `xiaomi-research/MiLMMT-46-4B-v1.0`에 적용한다.
- 4B 조정 파라미터:
  - `learning_rate`: `3.0e-5` (1B 대비 보수적으로 설정해 4B의 강한 일반 번역 능력 보존)
  - `lora_rank`: `16` (필요 시 `32`), `lora_alpha`: `32` (`64`)
  - 가능하면 QLoRA(4-bit)보다 `bf16` LoRA(+ gradient checkpointing)를 우선해 병합·GGUF 양자화 시 누적 오차를 줄인다.

### 5-3. GGUF 변환 및 양자화 파이프라인
1. `llamafactory-cli export`로 LoRA 병합 HF 체크포인트 생성
2. `llama.cpp` `convert_hf_to_gguf.py --outtype bf16` (또는 `f16`) 변환
3. `llama-quantize`로 기존 프로필 규격에 맞춰 `Q8_0` 및 `Q4_K_M` 생성 (`Q4_K_M`은 다국어+한국어+FFXIV 보정용 calibration 텍스트 기반 `--imatrix` 적용 권장)
4. **반드시 GGUF 양자화 이후 모델로 `eval.jsonl` 최종 게이트를 재실행**한다(HF bf16 통과만으로 배포하지 않는다).
5. 통과 시 SHA-256·파일 크기·메모리 추정치를 산출해 `MiLMMTModelProfiles`에 신규 프로필로 등록한다.

### 5-4. 4분할 정량 평가 게이트 (배포 통과 기준)

고정 `eval.jsonl`(초기 18건 유니크 시드 포함, 목표 100~200건) + 공개 일반 벤치마크(FLORES-200)로 원본 `v1.0`과 1:1 비교한다.

| 평가 버킷 | 측정 지표 | 합격 기준 (GGUF 기준) |
| --- | --- | --- |
| **1. FFXIV 용어/고유명사** | Term Accuracy (용어집 주입 시) | **≥ 90%** 및 원본 `v1.0` 대비 뚜렷한 상승 |
| **2. Distractor 저항성** | 무관 용어 오주입률 (False Insertion Rate) | **≤ 3%** |
| **3. 시스템 태그·언어 보존** | 타겟 언어 이탈률 / `[HQ]`·`『』`·약어 보존율 | 언어 이탈 **0%**, 태그 보존 **≥ 98%** |
| **4. 초단문·구어체 안정성** | 64토큰 반복 폭주율 / 초단문 과생성률 / 사람 검수 승률 | 반복 폭주 **0%**, 원본 대비 승률 우세 |
| **5. 일반 도메인 회귀** | FLORES-200 (`JA→KO`, `EN→KO`, `DE→KO`, `FR→KO`) chrF++ / COMET | 원본 `v1.0` 대비 하락 **≤ 0.5pt** |

---

## 6. Step 4~5 — 앱 용어집 동기화 연동 및 CPO/DPO 보강

### 6-1. 앱·서버 용어집 연동 (1B 1차 모델 검증 통과 후)
- **`ironworks-db-endpoint`**:
  - `GET /v2/glossary` 추가 (버전 문자열 + 언어쌍별 `[{source, target}]` JSON, `ETag` / `Cache-Control` 지원).
- **`IronworksTranslator`**:
  - 기동 시 경량 동기화 후 `%LOCALAPPDATA%\IronworksTranslator\glossary\glossary.json`에 로컬 캐시(오프라인/서버 장애 시 캐시 또는 비활성 폴백).
  - `MiLMMTModelProfile`에 `SupportsGlossaryPrompt` 플래그를 추가해 **Glossary-SFT 모델 프로필에서만** `Glossary:` 블록을 활성화하고, 원본 `v1.0` 프로필은 기존 프롬프트를 그대로 유지.
  - 최장 일치(longest-match) 우선으로 최대 8개 용어 추출 후 `RenderPrompt`에 주입.
  - **경계 체크 필수**: 라틴 문자 용어는 영숫자 단어 경계 체크, 일본어 카타카나 용어는 인접 문자가 카타카나가 아닐 때만 매칭(긴 카타카나 단어 내부의 짧은 스킬명 부분 오매칭 방지).
  - `MiLMMTTranslator.TranslationCacheKey`에 용어집 버전(또는 매칭된 용어 해시)을 포함해 용어집 갱신 시 캐시 무효화 보장.

### 6-2. 2단계 CPO/DPO 보강 (누적 오역 300~1,000건 이후)
- Glossary-SFT 모델 프로필을 배포한 뒤, 해당 `model_sha256`에서 새로 수집된 `cpo_user.jsonl`이 충분히 쌓이면 2단계 CPO/DPO를 적용한다.
- 권장 초안: `stage: dpo` (`pref_loss: cpo` 또는 `sigmoid`), `learning_rate: 5e-6 ~ 1e-5`, `pref_beta: 0.1`, `num_train_epochs: 1.0`.
- `eval.jsonl` + 일반 벤치마크 회귀를 통과할 때만 신규 리비전으로 승격한다.

---

## 7. 바로 착수 가능한 실행 순서

1. **[Step 0]** `MiLMMTTranslator` 반복 페널티 실험 및 단위 테스트 추가 (초단문 감탄사 반복 폭주 차단 검증)
2. **[Step 1]** 로컬 검수·추출 CLI(`pull` / `review` / `sync-status` / `export`) 구현 후 현재 `pending` 18건 전수 검수 → 초기 `eval.jsonl` 시드 + 초기 `glossary.json` 생성
3. **[Step 2]** 토크나이저 정합 체크 + 3축 하이브리드 부트스트랩 데이터셋 생성 파이프라인 구축 (20k~25k 샘플)
4. **[Step 3]** `MiLMMT-46-1B-v1.0` LoRA SFT → GGUF 변환(`Q8_0`/`Q4_K_M`) → 4분할 평가 게이트 검증
5. **[Step 4]** 통과 시 `ironworks-db-endpoint` `/v2/glossary` 및 앱 내 조건부 용어집 주입 연동 → 동일 레시피로 `MiLMMT-46-4B-v1.0` 확장
