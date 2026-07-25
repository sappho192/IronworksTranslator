# BattleTalk / TalkSubtitle 지원 범위 조사

작성일: 2026-07-26

## 1. 조사 목적

이 문서는 IronworksTranslator의 Hermes v2 마이그레이션 이후 `BattleTalk`와
`TalkSubtitle`까지 번역 대상으로 확장할 때 필요한 변경 범위와 선행 조건을 정리한다.

기존 표준 Talk 전환 계획은 다음 문서를 기준으로 한다.

- [Hermes v2 마이그레이션 계획](../2026-07-25/HERMES_V2_MIGRATION_PLAN.md)
- [Hermes v2 / Sharlayan.Lite 9.1.2 upstream handoff](../2026-07-25/2026-07-25-hermes-v2-upstream-handoff.md)

이번 조사는 구현이나 release readiness를 증명하지 않는다. 현재 source 계약을 기준으로
변경 범위를 식별한 결과다.

## 2. 결론

`BattleTalk`와 `TalkSubtitle`은 IronworksTranslator만 변경해서 지원할 수 없다. 현재
Sharlayan.Lite `9.1.2`와 Hermes v2 production 계약에는 두 resource가 없으므로 다음 세
저장소를 순서대로 변경해야 한다.

```text
ffxiv-hermes
  -> Hermes v2 schema / generator / manifest
  -> Sharlayan.Lite parser / mapper / reader / public API
  -> IronworksTranslator polling / observation / queue / rendering
```

상대적인 변경 규모는 다음과 같다.

| 대상 | 예상 규모 | 이유 |
| --- | --- | --- |
| TalkSubtitle | 중간 | FFXIVClientStructs에 현재 text field가 존재하지만 Hermes와 Sharlayan 계약이 없음 |
| BattleTalk | 중간 이상~큼 | 현재 화면과 name/text를 연결하는 안정적인 semantic contract와 live evidence가 없음 |

권장 순서는 현재 표준 Talk 마이그레이션을 먼저 완료한 뒤 `TalkSubtitle`을 별도 기능으로
추가하고, `BattleTalk`는 live memory contract 조사 이후 진행하는 것이다.

## 3. 조사 기준

확인한 upstream snapshot:

- `ffxiv-hermes`: `df4aff864037724847f01e3f3a100a4b0fdb0e04`
- `Sharlayan.Lite`: `02065aecbf3770b64e8938f459829dbc4ffae145`
- Sharlayan package version: `9.1.2`

관련 upstream 문서:

- [ffxiv-hermes v2 구현 계획](https://github.com/sappho192/ffxiv-hermes/blob/main/docs/V2_IMPLEMENTATION_PLAN.md)
- [Sharlayan Hermes v2 runtime 계획](https://github.com/sappho192/Sharlayan.Lite/blob/min-chat/docs/2026-07-22/2026-07-22-06-hermes-v2-runtime-plan.md)

## 4. 현재 지원 경계

### Hermes v2

현재 schema와 generator의 필수 runtime resource는 다음 세 가지다.

```text
chatLog
talk
currentTalk
```

`BattleTalk`의 현재 표시 문자열과 `TalkSubtitle`은 초기 범위에서 명시적으로 제외됐다.
upstream 계획에서도 다음 항목이 미완료 상태다.

- `TalkSubtitle` resource 검토
- BattleTalk의 누락된 FCS semantic metadata 추적

따라서 production manifest는 두 UI의 layout이나 의미를 전달하지 않는다.

### Sharlayan.Lite 9.1.2

`HermesV2ManifestParser`는 `resources` 아래에 `chatLog`, `talk`, `currentTalk`만 허용한다.
알 수 없는 resource를 strict validation에서 거부하며 다음 public API만 제공한다.

```csharp
Reader.CanGetTalk()
Reader.GetTalk()
Reader.GetCurrentTalk()
Reader.GetLastTalk()
```

BattleTalk 또는 TalkSubtitle용 layout, reader, result type 및 public API는 없다.

### IronworksTranslator

현재 Hermes v2 계획은 표준 `Talk`를 `DialogueEntry(Speaker, Text)`로 전달하고
DialogueWindow에 `Speaker: Text` 형식으로 표시하는 범위다. BattleTalk와 TalkSubtitle은
비목표로 명시되어 있다.

upstream API가 추가된 뒤에도 다음 consumer 변경이 필요하다.

- source 종류 식별
- source별 visibility와 baseline 추적
- 여러 source의 polling 및 queue 병합
- CHATLOG와의 중복 여부 확인
- speaker가 없는 source의 표시 규칙

## 5. TalkSubtitle 조사

FFXIVClientStructs에는 다음 현재 구조가 존재한다.

```text
Client::UI::AddonTalkSubtitle
  Addon name: TalkSubtitle
  SubtitleText: Utf8String at offset 0x238
```

이는 text를 읽기 위한 출발점이 명확하다는 뜻이다. 다만 production reader로 사용하려면
다음 계약을 추가로 확정해야 한다.

- addon 탐색 root와 traversal layout
- addon visibility와 readiness 조건
- `SubtitleText`를 안정적으로 읽기 위한 snapshot 절차
- `Utf8String` pointer, length 및 최대 길이 검증
- 닫힘 이후 잔존 text 처리
- 같은 subtitle의 재표시 구분

현재 확인된 구조에는 speaker field가 없다. IronworksTranslator 표시 정책은 다음이
적절하다.

```text
speaker 있음   -> Speaker: Text
speaker 없음   -> Text
```

### TalkSubtitle에 필요한 upstream 변경

Hermes:

- optional `talkSubtitle` resource schema
- FCS metadata extractor
- deterministic generator 및 semantic validator
- candidate manifest와 live-verified production 승격

Sharlayan:

- manifest DTO/parser/mapper
- `TalkSubtitleMemoryLayout`
- bounded UTF-8 memory reader
- `CanGetTalkSubtitle()` / `GetTalkSubtitle()` API
- visibility, unavailable, corrupt-memory 단위 테스트
- live smoke 옵션과 package release

IronworksTranslator:

- source-aware observation 모델
- speaker 없는 entry 처리
- 표준 Talk와 동일한 queue/backpressure 유지
- 표시 종료와 재표시 중복 테스트

## 6. BattleTalk 조사

FFXIVClientStructs에는 `AgentHUD`의 BattleTalk 대기열 구조가 존재한다.

```text
HudQueuedBattleTalk
  IsPending
  Style
  Name: Utf8String
  Text: Utf8String
  Image
  Sound
  EntityId
```

그러나 이 queue가 현재 화면에 표시된 BattleTalk와 정확히 어떻게 대응하는지는 현재
Hermes 계약과 Sharlayan live evidence로 확인되지 않았다. `_BattleTalk` addon 자체도
현재 snapshot에서 stable name/text field semantic metadata가 제공되지 않는다.

따라서 구현 전에 다음을 실제 게임에서 조사해야 한다.

- queue entry의 생성, pending, 표시 및 제거 전환
- 현재 표시 entry를 식별할 수 있는 index 또는 state
- `_BattleTalk` addon visibility와 queue state의 대응
- name/text가 화면과 byte 단위로 일치하는지
- 동일 text 연속 표시와 여러 queued entry의 순서
- 이미지·음성형 BattleTalk에서 text availability
- process reconnect와 UI 닫힘 이후 잔존 데이터

queue의 첫 항목이나 `IsPending`만으로 현재 표시 text를 추측해서는 안 된다. 화면과의
live correlation으로 의미를 확정한 뒤 Hermes semantic contract를 작성해야 한다.

### BattleTalk에 필요한 upstream 변경

TalkSubtitle 변경 항목에 더해 다음 작업이 필요하다.

- 현재 표시 BattleTalk를 나타내는 canonical source 결정
- FCS에 부족한 semantic metadata 보완 또는 검증 가능한 manifest contract 설계
- queue와 current-visible 상태의 atomic snapshot reader
- 빠른 연속 표시와 queue 순서 단위 테스트
- 실제 전투 및 퀘스트 상황의 extended live smoke

speaker와 text가 신뢰성 있게 제공되면 IronworksTranslator에서는 `Speaker: Text` 형식을
그대로 사용할 수 있다.

## 7. IronworksTranslator 설계 영향

### Observation 모델

여러 대화 source를 지원하려면 최소한 source kind를 보존해야 한다.

```csharp
public enum DialogueKind {
    StandardTalk,
    BattleTalk,
    TalkSubtitle,
}

public sealed class DialogueEntry {
    public DialogueKind Kind { get; }
    public string Speaker { get; }
    public string Text { get; }
}
```

`Speaker`와 `Text`만으로 source를 합치면 서로 다른 UI에서 같은 문장이 표시된 경우를
구분할 수 없다.

### Polling

- handler initialization 완료 후 준비된 source만 읽는다.
- 가능하면 기존 dialogue polling callback 한 곳에서 source들을 순서대로 관찰한다.
- source마다 최초 baseline, visibility 전환 및 reconnect reset 상태를 유지한다.
- 한 source의 unavailable 상태가 CHATLOG나 다른 대화 source를 중단하지 않게 한다.
- source별 unavailable 로그는 attach당 한 번만 기록한다.

### 중복

BattleTalk 또는 TalkSubtitle의 같은 발화가 CHATLOG의 `NPCDialog`나 `BossQuotes`에도
들어오는지는 live test로 확인해야 한다.

검증 전에는 text만 같은 항목을 source 간에 무조건 제거하지 않는다. 검증 결과 동일 event가
두 경로로 들어오는 것이 확인되면 짧은 correlation window와 source priority를 별도 정책으로
정의한다.

### 표시

- Standard Talk: `Speaker: translated text`
- BattleTalk: speaker가 있으면 `Speaker: translated text`
- TalkSubtitle: `translated text`
- speaker text 자체는 번역 prompt에 포함하지 않는다.
- 기존 DialogueWindow의 bounded history와 UI thread 규칙을 유지한다.

## 8. 권장 구현 단계

### Phase A: 현재 표준 Talk 마이그레이션 완료

- Sharlayan.Lite `9.1.2` 통합
- current-first Talk reader
- typed queue와 observation tracker
- `Speaker: Text` 표시
- remote/cache/embedded 및 실제 게임 검증

이 단계에 BattleTalk나 TalkSubtitle을 포함하지 않는다.

### Phase B: TalkSubtitle upstream spike

- live memory에서 text, visibility 및 close transition 관찰
- Hermes optional resource 계약 작성
- Sharlayan prototype reader와 synthetic memory test

spike 결과가 안정적일 때만 production schema와 package 변경을 진행한다.

### Phase C: TalkSubtitle release와 consumer 통합

- Hermes candidate 생성
- live smoke 및 수동 production 승격
- Sharlayan package release
- IronworksTranslator 통합과 packaged-app smoke

### Phase D: BattleTalk semantic 조사

- queue와 addon의 live correlation
- 현재 표시 entry의 canonical source 확정
- 빠른 연속 발화와 중복 경로 evidence 수집

semantic contract가 확정되기 전에는 production reader를 구현하지 않는다.

### Phase E: BattleTalk release와 consumer 통합

TalkSubtitle과 같은 candidate → live smoke → manual publish → package → consumer 순서를
독립적으로 적용한다.

## 9. 검증 계획

### Static 및 단위 테스트

- manifest deterministic output
- unknown/missing/invalid resource 거부
- pointer overflow와 invalid offset 거부
- unreadable pointer, invalid UTF-8, 비정상 길이 처리
- hidden/unready addon은 unavailable
- stable snapshot만 entry로 반환
- 동일 source의 baseline, close, reopen 및 reconnect
- typed queue capacity와 순서 유지

### 실제 게임

- BattleTalk와 TalkSubtitle 각각의 실제 화면 text 일치
- 한국 및 글로벌 client에서 가능한 범위의 smoke
- 표시 시작, text 변경, 닫힘 및 동일 text 재표시
- 여러 대사의 빠른 연속 표시
- Standard Talk와의 동시 또는 인접 표시
- CHATLOG 중복 여부
- process 종료와 재연결
- remote, verified cache 및 embedded manifest
- Velopack packaged app

### Release gate

build와 synthetic unit test 통과만으로 production support를 선언하지 않는다.

PASS가 증명하는 것:

- 검증한 game build와 client에서 해당 UI의 화면 값과 reader 값이 일치함
- candidate manifest와 Sharlayan reader가 해당 시나리오에서 안전하게 동작함
- package와 IronworksTranslator 통합 경로가 검증 범위에서 동작함

PASS가 증명하지 않는 것:

- 모든 퀘스트, 전투, UI style 및 client에서의 완전한 coverage
- 장시간 실행과 모든 발화 순서에서의 무누락
- 실제로 관찰하지 않은 이미지·음성·특수 presentation variant 지원
- 다음 FFXIV patch에서의 자동 호환성

## 10. 권고

현재 Hermes v2 마이그레이션의 release 범위에는 두 기능을 추가하지 않는다.

1. 표준 Talk 마이그레이션을 먼저 완료한다.
2. `TalkSubtitle`을 다음 독립 기능으로 조사·구현한다.
3. `BattleTalk`는 current-visible semantic evidence를 확보한 뒤 별도 기능으로 진행한다.
4. 두 기능 모두 upstream release와 실제 게임 gate를 통과한 후 IronworksTranslator에서
   활성화한다.
