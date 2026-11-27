# MiLMMT Glossary Builder

`ffxiv-datamining` CSV(`ja`/`en`/`de`/`fr`/`ko`)와 Google Drive 실전 채팅 용어 자료를 병합해 계층형 FFXIV 번역 용어집을 생성하는 프로토타입 스크립트입니다. (Python 3.10+ 표준 라이브러리만 사용)

## 출력물

- `glossary.runtime.json`: 앱 실시간 프롬프트 주입용 경량 핵심 용어집 (Tier 0~3, 동일 시트 다의어 자동 제외)
- `glossary.full.json`: SFT/평가 부트스트랩용 확장 용어집 (Tier 0~4, 메타데이터 포함)
- `community_phrases.jsonl`: 코알라 노트 `🟡소통` 등에서 추출한 실전 채팅 병렬 문장(축 2 SFT용)
- `glossary.report.json`: 소스별 추출 수량, 필터 탈락 통계, Tier 간 충돌 목록

> 생성 산출물은 `.gitignore` 대상인 `artifacts/glossary/` 같은 로컬 경로로 출력하고 커밋하지 마세요.

## 실행 예시

권장 빌드 (공식 `ffxiv-datamining` + 검수 완료된 `community_curated.json`):

```powershell
python tools\milmmt-glossary-builder\build_glossary.py `
  --datamining-dir D:\REPO\ffxiv-datamining `
  --community-curated-json tools\milmmt-glossary-builder\community_curated.json `
  --out-dir artifacts\glossary
```

원본 Drive 시트에서 초안/대화 문장(`community_phrases.jsonl`) 추출 시:

```powershell
python tools\milmmt-glossary-builder\build_glossary.py `
  --datamining-dir D:\REPO\ffxiv-datamining `
  --koala-xlsx path\to\koala.xlsx `
  --jclient-xlsx path\to\jclient_terms.xlsx `
  --ff14-terms-md path\to\ff14_terms.md `
  --out-dir artifacts\glossary
```

## 테스트

```powershell
python -m unittest discover -s tools\milmmt-glossary-builder -v
```
