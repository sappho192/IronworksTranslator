# Hermes v2 / Sharlayan.Lite 9.1.2 → 9.1.4 upstream handoff

작성일: 2026-07-25

## 문서 목적

이 문서는 IronworksTranslator가 Hermes v2와 Sharlayan.Lite `9.1.4`를 소비할 때 필요한
최종 upstream 계약, 검증된 범위 및 남은 consumer 작업을 전달한다. 구현 절차와 체크리스트는
같은 디렉터리의 [`HERMES_V2_MIGRATION_PLAN.md`](HERMES_V2_MIGRATION_PLAN.md)를 따른다.

기준 문서:

- [ffxiv-hermes v2 구현 계획](https://github.com/sappho192/ffxiv-hermes/blob/main/docs/V2_IMPLEMENTATION_PLAN.md)
- [Hermes v2 release 기록](https://github.com/sappho192/ffxiv-hermes/blob/main/docs/2026-07-25/2026-07-25-v2-release-session.md)
- [Sharlayan 9.1.2 runtime 계획](https://github.com/sappho192/Sharlayan.Lite/blob/min-chat/docs/2026-07-22/2026-07-22-06-hermes-v2-runtime-plan.md)
- [Sharlayan 9.1.2 release 기록](https://github.com/sappho192/Sharlayan.Lite/blob/min-chat/docs/2026-07-25/2026-07-25-hermes-v2-and-nuget-release.md)
- [Sharlayan.Lite 9.1.2 on NuGet.org](https://www.nuget.org/packages/Sharlayan.Lite/9.1.2)
- [Sharlayan.Lite 9.1.4 on NuGet.org](https://www.nuget.org/packages/Sharlayan.Lite/9.1.4)
- [Sharlayan.Lite 9.1.4 production Actions run](https://github.com/sappho192/Sharlayan.Lite/actions/runs/30166550163)

## 인계 시점의 upstream 상태

### Hermes

- public v2 base는 `https://hermes.sapphosound.com/v2/`다.
- `v2/latest.json`은 `live-verified` immutable manifest를 가리킨다.
- immutable manifest와 latest pointer는 origin `Cache-Control`을 기준으로 전달된다.
- FCS HEAD 확인과 candidate PR 생성은 자동화되어 있다.
- production 승격과 rollback은 보호된 환경의 수동 승인 및 live-smoke 증거를 요구한다.
- legacy `latest/address.json` endpoint는 기존 IronworksTranslator를 위해 무기한 유지한다.

현재 GitHub 운영 설정을 public 문서에 고정된 값으로 복제하지 않는다. 필요하면 Hermes
workflow가 참조하는 입력과 repository/environment 설정을 읽기 전용으로 대조한다.

### Sharlayan.Lite

- 최종 resource 및 Talk consumer package는 stable `9.1.4`다.
- 지원 target에는 IronworksTranslator가 사용하는 `net10.0`이 포함된다.
- `RemotePreferred`는 remote → verified cache → embedded 순서로 resource를 선택한다.
- 원격 manifest와 cache는 strict validation을 통과해야 하며 실패하면 다음 source로 fallback한다.
- embedded manifest는 handoff 시점의 Hermes live-verified production byte와 동기화되어 있다.
- `9.1.4` package는 선택된 manifest revision에서 CHATLOG와 Talk를 초기화한다.
- `MemoryHandler.ResourceInfo`로 source, revision, FCS commit, validation 및 fallback 정보를
  확인할 수 있다.

### 2026-07-26 consumer compile 정정

NuGet에 발행된 `9.1.2`는 repository commit `1f1bc33`에서 패키징됐다. current Talk API는
그 이후 commit `3e27261`에서 추가됐으므로 공개 `9.1.2` DLL에 포함되지 않는다.

- `Reader`에는 `CanGetLastTalk()` / `GetLastTalk()`만 존재한다.
- `CanGetTalk()`, `GetTalk()`, `GetCurrentTalk()`은 존재하지 않는다.
- `TalkResult`에는 `IsAvailable`, `Name`, `Text`만 있고 `Source`, `IsVisible`이 없다.
- local nupkg SHA-512는 NuGet cache metadata와 일치하므로 cache 손상이 아니다.
- 당시 NuGet V3에는 `8.0.1`과 `9.1.2`만 공개되어 있었다.

따라서 `9.1.2`는 Phase 1 resource configuration에는 사용할 수 있지만 Phase 2 Talk API
전환에는 사용할 수 없었다. 이것이 당시 IronworksTranslator Phase 2를 중단한 역사적
원인이다.

### 2026-07-26 Sharlayan.Lite 9.1.4 해결

current Talk API와 CHATLOG `StdVector` count 감소/reset 처리를 포함한 stable `9.1.4`가
NuGet.org에 발행됐다.

- package/source commit: `78d2eccb2025ef16786811b97aed8bbe30dc9552`
- release record commit: `f00386b5f261932def13fd63f55fb4f7dfa8bdbe`
- public repository-signed nupkg SHA-256:
  `e310c00162d7775f84c66543a67a08410bb771143f58ce78b43251649e2b9d50`
- target frameworks: `net462;net48;net6.0;net7.0;net8.0;net10.0`
- package repository metadata는 위 package/source commit을 가리킨다.
- `net10.0` DLL/PDB가 포함되고 불필요한 FFXIVClientStructs assembly는 없다.
- NuGet.org 단일 source와 완전히 새로운 cache의 net10 consumer build/API contract가
  통과했다.
- upstream live reset 재검사는 420.1초 동안 신규 2,723건, cursor wrap 3회,
  stderr/OnException/`Invalid chat entry size` 0건으로 통과했다.

이에 따라 Phase 2 package blocker는 해결됐다. IronworksTranslator는 local project
reference나 DLL 복사 없이 정식 `9.1.4` package만 사용한다.

## IronworksTranslator가 사용할 API

설정:

```csharp
var configuration = new SharlayanConfiguration {
    ProcessModel = processModel,
    GameLanguage = gameLanguage,
    ResourceMode = ResourceMode.RemotePreferred,
    HermesV2LatestUri = new Uri("https://hermes.sapphosound.com/v2/latest.json"),
    ResourceCacheDirectory = AppPaths.SharlayanCacheDirectory,
};
```

`HermesV2LatestUri`는 현재 같은 값을 기본값으로 제공하지만, consumer 정책을 명확히 하기 위해
IronworksTranslator에서 명시한다. 앱은 Hermes JSON을 직접 다운로드하거나 해석하지 않는다.
`GameRegion`은 Sharlayan.Lite `9.1.2` 이후 obsolete no-op이며 chat resource가
region-independent이므로 IronworksTranslator configuration에 전달하지 않는다.

IronworksTranslator의 consumer 정책은 `RemotePreferred`로 고정한다. `EmbeddedOnly` 사용자
설정과 수동 resource reload UI는 제공하지 않으며, 실행 중 선택된 revision은 handler 수명
동안 유지한다. 사용자가 새 revision을 즉시 확인하려면 앱을 재시작한다.

Talk 읽기 요구 계약이며, 다음 API는 stable `9.1.4` package에서 제공된다.

```csharp
if (handler.Reader.CanGetTalk()) {
    TalkResult talk = handler.Reader.GetTalk();
}
```

`GetTalk()`의 반환 정책:

1. 현재 표준 Talk addon을 읽을 수 있으면 `Source=Current`, `IsVisible=true`
2. current를 읽을 수 없으면 마지막 Talk 값으로 fallback하여 `Source=Last`, `IsVisible=false`
3. 어느 쪽도 읽을 수 없으면 `IsAvailable=false`

`Name`과 `Text`는 pair로 처리한다. `GetCurrentTalk()`과 `GetLastTalk()`은 consumer가 source를
명시적으로 분리해야 할 때만 사용한다.

## 현재 화면 대사와 attach baseline

UX 기준은 현재 화면에 표시되는 대사를 우선 취득하는 것이다.

- attach 직후 `Current/visible` snapshot은 현재 사용자가 보고 있는 대사이므로 처리한다.
- attach 직후 `Last/not visible` snapshot은 이전 대사의 잔존값일 수 있으므로 baseline으로만
  저장하고 신규 번역으로 보내지 않는다.
- 이후 `Current/visible → Last/not visible → Current/visible` 전환을 관찰한 동일 pair는
  새로운 표시 세션으로 처리할 수 있다.
- polling 사이에 닫힘을 관찰하지 못한 동일 pair의 재표시는 구분할 수 없다.
- process reconnect 시 tracker 상태를 초기화하고 같은 정책을 다시 적용한다.

실제 대사 문자열은 일시적인 로컬 진단에서만 화면과 대조한다. public 문서, 테스트 fixture,
artifact 및 공유 로그에는 source, visibility, 길이와 일치 여부만 남긴다.

## upstream에서 확인된 범위

- Hermes generator의 deterministic output 및 canonical revision 검증
- candidate → live smoke → 수동 production 승격
- Sharlayan remote, cache 및 embedded resource 선택
- embedded manifest와 production immutable manifest의 byte 일치
- source checkout에서 CHATLOG와 Talk signature/location 초기화
- source checkout에서 current Talk 화면 일치 및 다음 Talk로의 전환
- source checkout에서 Talk 종료 후 `Last/not visible` fallback
- Sharlayan.Lite `9.1.4` package 생성, 검증, NuGet 배포 및 신규 cache restore
- `net10.0` 소비자 build

공개 `9.1.2` consumer compile은 Talk API 부재로 실패했지만, `9.1.4` 신규 cache consumer
build와 API contract 검증은 통과했다.

Sharlayan.Lite `9.1.2`는 지인 테스트를 시작하기 위한 명시적 일회성 release waiver를
포함한다. 일부 장시간·다중 client evidence가 미충족인 사실은 그대로 유지되며, 이 waiver를
다음 release나 IronworksTranslator의 release readiness로 자동 승계하지 않는다.

## IronworksTranslator 작업 범위

1. `Sharlayan.Lite`를 `9.1.4`로 갱신하고 완전히 새로운 NuGet cache에서 restore/build한다.
2. `ResourceMode.RemotePreferred`와 cache directory를 설정한다.
3. `HermesAddress.GetLatestAddress()`와 앱의 Hermes HTTP 요청을 제거한다.
4. `ALLMESSAGES` custom signature와 raw `GetString(..., 2048)` polling을 제거한다.
5. current-first `CanGetTalk()` / `GetTalk()`으로 전환한다.
6. speaker와 text를 typed pipeline 끝까지 분리해 보존하고 DialogueWindow에는
   `Speaker: Text` 형식으로 표시한다.
7. current-visible 처리, last baseline, 중복 및 reconnect tracker를 단위 테스트한다.
8. `ResourceInfo`를 이용해 선택된 source와 revision을 attach당 한 번 기록한다.
9. remote, cache 및 embedded fallback에서 CHATLOG와 Talk를 검증한다.
10. packaged app과 실제 게임에서 최종 smoke를 수행한다.
11. `UseInternalAddress`와 local `address.json` 지원을 제거하며 legacy 값을 다른 resource
    mode로 mapping하지 않는다.

## 완료 경계

이 handoff가 증명하는 것은 Hermes와 Sharlayan `9.1.4`가 IronworksTranslator Phase 2를
진행할 수 있는 upstream 조건을 충족했다는 점이다. upstream smoke는 IronworksTranslator의
release readiness를 대신하지 않는다. 다음 항목은 앱에서 별도로 완료해야 한다.

- 기존 settings YAML의 `use_internal_address`를 값 mapping 없이 안전하게 제거
- typed dialogue queue
- DialogueWindow의 `Speaker: Text` 표시
- packaged-app fallback
- 실제 게임에서 CHATLOG와 current Talk 동시 동작
- process 종료·재연결 및 앱 shutdown
- IronworksTranslator release와 rollback 검증

수동 resource reload는 consumer 작업 범위에 포함하지 않는다. 새 revision의 사용자 지원
갱신 경로는 앱 재시작이다.

통합 중 upstream API 또는 runtime 결함을 발견하면 consumer workaround를 먼저 고정하지 말고
Hermes 계약 또는 Sharlayan API 책임인지 분리해 해당 저장소에서 수정·검증한 뒤 package
version을 갱신한다.
