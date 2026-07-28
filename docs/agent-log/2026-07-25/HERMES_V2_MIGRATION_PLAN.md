# Hermes v2 마이그레이션 계획

## 1. 문서 역할

이 문서는 IronworksTranslator가 기존 Hermes 주소 JSON과 raw Sharlayan memory 접근을 제거하고 Hermes v2를 지원하는 Sharlayan.Lite의 고수준 CHATLOG 및 Talk API로 전환하기 위한 저장소별 구현 계획이다.

기준 문서:

- [ffxiv-hermes v2 구현 계획](https://github.com/sappho192/ffxiv-hermes/blob/main/docs/V2_IMPLEMENTATION_PLAN.md)
- [Sharlayan Hermes v2 런타임 계획](https://github.com/sappho192/Sharlayan.Lite/blob/min-chat/docs/2026-07-22/2026-07-22-06-hermes-v2-runtime-plan.md)
- [IronworksTranslator용 upstream handoff](2026-07-25-hermes-v2-upstream-handoff.md)

Hermes v2 schema는 ffxiv-hermes 문서가, runtime API와 fallback 동작은 Sharlayan 문서가 기준이다. IronworksTranslator는 Hermes manifest를 직접 해석하지 않는다.

## 2. 목표

- 기존 `latest/address.json` 다운로드와 `HermesAddress` 사용을 제거한다.
- `ALLMESSAGES` custom `Signature`와 raw `GetString(..., 2048)` 호출을 제거한다.
- Hermes v2를 지원하는 Sharlayan.Lite package로 갱신한다.
- Sharlayan을 `RemotePreferred`로 구성하여 주소 데이터만 바뀐 경우 앱 재배포 없이 대응한다.
- 네트워크, cache 또는 remote manifest 오류가 앱 시작과 CHATLOG 번역을 중단하지 않게 한다.
- 표준 NPC Talk의 이름과 대사를 typed data로 처리한다.
- DialogueWindow에는 NPC 이름과 번역문을 `Speaker: Text` 형식으로 표시한다.
- attach 직후 메모리에 남아 있는 이전 Talk를 신규 대사로 번역하지 않는다.
- 동일 대사 반복과 게임 process 재연결을 명시적으로 처리한다.
- 사용 중인 Hermes revision과 fallback source를 진단 로그에 기록한다.

## 3. 비목표

- IronworksTranslator에서 Hermes v2 JSON DTO와 validator를 구현하지 않는다.
- 앱에서 FCS repository나 GitHub HEAD를 직접 확인하지 않는다.
- 앱에서 signature pattern이나 pointer path를 구성하지 않는다.
- BattleTalk, TalkSubtitle, NpcYell 및 말풍선을 이번 전환에 포함하지 않는다.
- `LastTalkText`만으로 현재 대화창의 open 및 closed 상태를 추측하지 않는다.
- 번역 엔진, CHATLOG channel filtering 또는 Velopack update 구조를 변경하지 않는다.

## 4. 마이그레이션 전 상태

마이그레이션 전 NPC 대화 경로:

1. `HermesAddress.GetLatestAddress()`가 동기 HTTP로 legacy JSON을 다운로드한다.
2. `ChatLookupService.AttachGame()`이 `ALLMESSAGES` signature를 직접 추가한다.
3. signature에는 pattern 없이 module-relative pointer path만 들어간다.
4. 200ms timer가 `GetString(location, 0, 2048)`을 호출한다.
5. text가 `lastMessage`와 다르면 `ChatQueue.EnqueueDialogueIfNew`에 전달한다.
6. `DialogueWindow`가 string queue에서 text를 꺼내 정제하고 번역한다.

관련 파일:

```text
src/IronworksTranslator/Utils/HermesAddress.cs
src/IronworksTranslator/Services/FFXIV/ChatLookupService.cs
src/IronworksTranslator/Models/ChatQueue.cs
src/IronworksTranslator/Views/Windows/DialogueWindow.xaml.cs
src/IronworksTranslator/Models/Settings/TranslatorSettings.cs
src/IronworksTranslator/Models/Settings/IronworksSettings.cs
src/IronworksTranslator/Utils/AppPaths.cs
tests/IronworksTranslator.Tests/Models/ChatQueueTests.cs
```

마이그레이션 전 위험:

- app attach가 동기 네트워크 요청에 의존한다.
- HTTP status, timeout, response 크기 및 schema를 검증하지 않는다.
- `HttpClient`, response 및 reader 생명주기가 정리되지 않는다.
- module-relative 주소가 게임 patch마다 바뀔 수 있다.
- 고정 2048바이트 read가 실제 `Utf8String` 길이를 사용하지 않는다.
- `lastMessage`와 `ChatQueue.LastMsg`가 중복 상태를 관리한다.
- attach 시 `"Dialogue window"` sentinel을 실제 queue에 넣는다.
- 이름을 읽지 않고 text 문자열만 처리한다.

## 5. 확정된 upstream 기준

2026-07-25 기준으로 Phase 1의 선행 조건이 충족됐다. 2026-07-26 consumer compile에서
공개 `9.1.2`의 Talk API 누락이 확인돼 Phase 2가 일시 중단됐으나, 같은 날
Sharlayan.Lite `9.1.4`가 발행되어 선행 조건이 해결됐다.

- Hermes v2 production latest는 `live-verified` immutable manifest를 가리킨다.
- 최종 consumer package는 Sharlayan.Lite `9.1.4`다.
- `RemotePreferred`는 remote → verified cache → embedded 순서로 fallback한다.
- upstream source에서 CHATLOG와 Talk는 선택된 같은 manifest revision에서 초기화된다.
- Phase 2에 필요한 source API는 `Reader.CanGetTalk()` / `Reader.GetTalk()`이다.
- 필요하면 `Reader.GetCurrentTalk()`과 `Reader.GetLastTalk()`으로 source를 명시적으로 구분할 수 있다.
- `TalkResult`는 `Name`, `Text`, `Source`, `IsVisible`, `IsAvailable`을 제공한다.
- 선택된 resource source, revision, FCS commit 및 fallback reason은
  `MemoryHandler.ResourceInfo`에서 확인한다.

역사적 원인으로, NuGet의 Sharlayan.Lite `9.1.2`는 repository commit `1f1bc33`에서
패키징됐고 current Talk API가 추가된 `3e27261`보다 이전이다. 실제 `9.1.2` DLL은
`CanGetLastTalk()` / `GetLastTalk()`만 제공하고 `TalkResult.Source` 및
`TalkResult.IsVisible`을 제공하지 않았다. package SHA-512는 NuGet cache metadata와
일치했으므로 local cache 손상이 아니었다.

해결된 stable `9.1.4`의 기준:

- package/source commit: `78d2eccb2025ef16786811b97aed8bbe30dc9552`
- release record commit: `f00386b5f261932def13fd63f55fb4f7dfa8bdbe`
- public repository-signed nupkg SHA-256:
  `e310c00162d7775f84c66543a67a08410bb771143f58ce78b43251649e2b9d50`
- public API: `CanGetTalk()`, `GetTalk()`, `GetCurrentTalk()`, `GetLastTalk()` 및
  `TalkResult.Source`, `IsVisible`, `IsAvailable`, `Name`, `Text`
- `GetTalk()`은 current-first이며 current가 없거나 일시적으로 unreadable하면 LastTalk로
  fallback한다.

## 6. 대상 구조

마이그레이션 후 데이터 흐름:

```text
Hermes v2
  -> Sharlayan remote/cache/embedded provider
  -> Sharlayan MemoryHandler + Reader
  -> ChatLookupService
  -> typed dialogue queue
  -> DialogueWindow translation/rendering
```

IronworksTranslator가 알아야 하는 정보:

- resource mode
- Hermes cache directory
- 선택된 resource source와 revision의 진단 정보
- Talk의 name, text 및 availability

IronworksTranslator가 몰라야 하는 정보:

- FCS type 및 field offset
- signature pattern
- relative follow offset
- pointer path
- `Utf8String` layout
- CHATLOG vector offset
- Hermes manifest JSON shape

## 7. Sharlayan package 갱신

Phase 1에서는 `src/IronworksTranslator/IronworksTranslator.csproj`의 `Sharlayan.Lite`
dependency를 `9.1.2`로 올렸다. Phase 2에서는 current Talk API와 CHATLOG reset 수정이
포함된 stable `9.1.4`로 갱신한다.

검증 항목:

- NuGet lock 또는 restore 결과가 의도한 package를 사용한다.
- `net10.0-windows`에서 Sharlayan의 compatible target asset이 선택된다.
- publish 산출물에 불필요한 FCS assembly가 포함되지 않는다.
- package update가 기존 `ChatLogItem`, `ChatLogResult` 및 Reader 호출과 호환된다.
- GitVersion과 Velopack versioning에는 별도 영향을 주지 않는다.

Phase 1 commit은 NuGet.org의 stable `9.1.2`를 사용했다. 전체 migration의 최종 dependency는
NuGet.org의 stable `9.1.4`다. local project reference, 임의 DLL 복사 및 reflection 기반
consumer workaround는 사용하지 않는다.

## 8. Sharlayan configuration

`AttachGame()`에서 obsolete JSON configuration을 제거하고 Hermes v2용 configuration을 명시한다.

개념적 설정:

```csharp
var configuration = new SharlayanConfiguration {
    ProcessModel = processModel,
    GameLanguage = gameLanguage,
    ResourceMode = ResourceMode.RemotePreferred,
    HermesV2LatestUri = new Uri("https://hermes.sapphosound.com/v2/latest.json"),
    ResourceCacheDirectory = AppPaths.SharlayanCacheDirectory,
};
```

제거 대상:

- `PatchVersion`
- `UseLocalCache`
- `JSONCacheDirectory`
- 해당 property를 설명하는 오래된 comment

정책:

- production URL은 한 곳의 상수 또는 configuration helper에 둔다.
- app 자체에서 Hermes HTTP 요청을 수행하지 않는다.
- Sharlayan initialization이 비동기로 진행되는 동안 UI thread를 막지 않는다.
- cache 경로는 기존 `%LOCALAPPDATA%\IronworksTranslator\data\sharlayan`을 재사용한다.
- 기본값과 production 동작은 `RemotePreferred`로 고정한다.
- `EmbeddedOnly`를 선택하는 사용자 설정이나 UI는 추가하지 않는다.
- 선택된 resource revision은 handler 수명 동안 고정한다.
- 수동 resource reload 기능은 추가하지 않는다. 사용자가 새 revision을 즉시 확인하려면 앱을
  재시작해야 한다.
- `GameRegion`은 Sharlayan.Lite `9.1.2` 이후 obsolete no-op이며 chat resource가
  region-independent이므로 configuration에서 제거한다.

## 9. 기존 Hermes 코드 제거

최종 제거 대상:

```text
src/IronworksTranslator/Utils/HermesAddress.cs
```

`ChatLookupService`에서 제거할 코드:

- `using Sharlayan.Models`가 custom `Signature` 때문에만 필요하면 해당 using
- `new List<Signature>()`
- `Key = "ALLMESSAGES"`
- `HermesAddress.GetLatestAddress().Address`
- `Scanner.LoadOffsets`의 custom dialogue signature 호출
- `ALLMESSAGES` memory location 검사와 로그
- `handler.GetString(..., 2048)` 호출
- `lastMessage` 필드
- `ChatQueue.EnqueueDialogue("Dialogue window")` sentinel

기존 `latest/address.json` endpoint는 구버전 app을 위해 Hermes에서 유지하지만 새 IronworksTranslator는 요청하지 않는다.

## 10. `UseInternalAddress` 설정 제거

현재 persisted YAML에는 `use_internal_address`가 존재할 수 있지만 Hermes v2 전환 후에는 더
이상 사용하지 않는다. `true`와 `false` 모두 `ResourceMode.RemotePreferred`를 사용하며
`EmbeddedOnly` 또는 다른 동작으로 mapping하지 않는다.

제거 정책:

1. `TranslatorSettings.UseInternalAddress`와 해당 runtime 분기를 제거한다.
2. 기존 YAML의 `use_internal_address`는 역직렬화 전에 제거하거나 unknown property로 안전하게
   무시한다.
3. 값의 존재 여부와 값 자체는 resource mode 선택에 영향을 주지 않는다.
4. 설정을 다시 저장하면 `use_internal_address`가 출력되지 않는지 검증한다.
5. 기존 local `address.json`은 읽지 않으며 production fallback으로 유지하지 않는다.
6. 사용자 설정이나 UI에 `EmbeddedOnly` 선택지를 새로 추가하지 않는다.

사용자가 직접 만든 legacy `address.json`을 계속 지원하는 호환 기능은 제공하지 않는다.

## 11. Talk polling

`ChatLookupService`는 raw memory location 대신 Sharlayan Reader를 호출한다.

개념적 흐름:

```csharp
private void UpdateDialogue(object? state) {
    var handler = CurrentMemoryHandler;
    if (handler?.Reader.CanGetTalk() != true) {
        LogDialogueNotReady();
        return;
    }

    var talk = handler.Reader.GetTalk();
    if (!talk.IsAvailable) {
        return;
    }

    ObserveTalk(talk);
}
```

규칙:

- handler initialization이 끝나기 전에는 Talk를 읽지 않는다.
- attach 직후 `Source=Last`인 잔존 snapshot은 baseline으로 저장하고 queue에 넣지 않는다.
- attach 직후에도 `Source=Current`, `IsVisible=true`인 snapshot은 현재 화면 대사이므로 처리한다.
- 이후 name 또는 text가 바뀌면 신규 observation으로 처리한다.
- 빈 text는 queue에 넣지 않되 baseline 상태에는 반영한다.
- unavailable read는 이전 baseline을 지우지 않는다.
- process 재연결 시 baseline을 초기화한다.
- polling interval은 우선 현재 200ms를 유지하고 실제 부하 측정 후 조정한다.
- `Source=Current`와 `IsVisible`은 현재 표시 여부 판단에 사용한다.
- `Source=Last` 값만으로 새로운 open event를 만들지 않는다.

## 12. 대화 상태 추적

현재 `lastMessage`와 `ChatQueue.LastMsg`가 같은 목적을 중복 수행한다. 하나의 테스트 가능한 상태 추적기로 통합한다.

제안 helper:

```text
src/IronworksTranslator/Services/FFXIV/TalkObservationTracker.cs
```

책임:

- 최초 `Last` snapshot baseline 처리와 현재 visible snapshot 통과
- name과 text 쌍 비교
- source와 visibility 전환 처리
- 빈 text 처리
- unavailable snapshot 무시
- process 재연결 reset
- 신규 observation만 반환

개념적 모델:

```csharp
public sealed record DialogueEntry(string Speaker, string Text);
```

프로젝트의 기존 block-scoped namespace 스타일을 따른다. record 사용 여부는 queue와 serialization 요구가 없으므로 기존 모델 스타일에 맞춰 class로 바꿀 수 있다.

비교 key는 `(Speaker, Text)`를 사용한다. text가 같고 speaker가 달라진 경우를 서로 다른 대사로 처리한다.

동일한 `(Speaker, Text)`가 `Current/visible → Last/not visible → Current/visible`로 전환되면
새로운 표시 세션으로 처리할 수 있다. polling 사이에 닫힘 상태를 관찰하지 못한 동일 대사의
재표시는 구분할 수 없으므로 중복으로 억제한다.

## 13. Queue 모델

이름과 text를 보존하기 위해 dialogue queue를 `ConcurrentQueue<string>`에서 typed entry queue로 변경하는 것을 권장한다.

개념적 구조:

```csharp
public sealed class DialogueEntry {
    public string Speaker { get; }
    public string Text { get; }
}
```

`ChatQueue` 변경:

- dialogue queue의 bounded 100개 정책을 유지한다.
- thread-safe enqueue를 유지한다.
- 중복 판정은 `TalkObservationTracker` 한 곳에서 수행한다.
- `LastMsg` global state는 제거한다.
- CHATLOG의 bounded `BlockingCollection`은 변경하지 않는다.

`DialogueWindow` 변경:

- typed entry에서 `Text`만 번역기에 전달한다.
- `Speaker`는 번역하지 않고 원본 이름을 유지한다.
- 이번 단계에서는 기존 TextBox append 구조를 유지하고 각 항목을
  `Speaker: {translated text}` 형식으로 표시한다.
- speaker가 비어 있으면 구분자 없이 번역문만 표시한다.
- `DialogueTranslationMethod.ChatMessage` 경로도 기존 author를 speaker로 전달한다.
- speaker별 별도 스타일이나 목록형 UI는 이번 단계에 포함하지 않는다.
- 기존 control payload 정제 순서를 유지한다.
- 정제 결과가 빈 문자열이면 표시하지 않는다.

이름 표시 자체는 게임에서 읽은 이름과 구분자만 사용하므로 별도 localized label을 추가하지
않는다. 그 외 사용자-visible 문자열이나 layout을 변경하면 한국어 및 영어 resource를 함께
갱신한다.

## 14. Timer와 초기화

현재 dialogue timer는 host start 직후 시작되어 `ALLMESSAGES` location을 반복 확인한다.

권장 변경:

- Sharlayan initialization 완료 전 timer를 시작하지 않는다.
- `OnMemoryLocationsFound` 또는 `InitializationTask` continuation에서 CHATLOG와 Talk readiness를 각각 확인한다.
- CHATLOG가 준비되면 chat timer를 시작한다.
- Talk가 준비되면 baseline을 먼저 읽고 dialogue timer를 시작한다.
- Talk가 unavailable이어도 CHATLOG timer는 정상 시작한다.
- timer callback 중 UI 객체에 직접 접근하지 않는다.
- `Destruct`, process exit 및 dispose에서 timer를 확실히 중지한다.
- 이전 handler의 늦은 event가 새 handler 상태를 변경하지 않게 instance 또는 generation을 확인한다.

`OnMemoryLocationsFound` 로그에서 `ALLMESSAGES`를 제거하고 다음 정보를 기록한다.

```text
CHATLOG readiness
Talk readiness
Hermes resource source
Resource revision
FCS commit
Initialization processing time
```

NPC 이름과 대사 본문은 initialization 및 진단 로그에 기록하지 않는다.

## 15. 오류 및 fallback UX

Sharlayan이 remote, cache 또는 embedded 중 하나로 성공하면 사용자에게 오류 dialog를 띄우지 않는다.

정책:

| 상황 | IronworksTranslator 동작 |
| --- | --- |
| remote 실패, cache 성공 | 정상 실행, source를 debug/info 로그에 기록 |
| remote 및 cache 실패, embedded 성공 | 정상 실행, fallback warning 한 번 기록 |
| Talk unavailable, CHATLOG 정상 | 채팅 번역 유지, 대화 번역만 대기 또는 비활성 |
| CHATLOG unavailable, Talk 정상 | Talk 기능 유지 여부를 명시적으로 결정하고 오류 중복 표시 방지 |
| handler initialization 실패 | 기존 attach 실패 UX 사용, 상세 원인은 로그에 기록 |
| game process 종료 | timer 중지, handler dispose, 재연결 가능 상태로 복귀 |

Hermes endpoint 장애를 MessageBox로 직접 표시하지 않는다. 자동 fallback이 모두 실패한 경우에만 기존 process attach 오류와 구분되는 사용자 안내를 후속 검토한다.

## 16. Logging

구조화 로그 필드:

```text
ResourceSource
ResourceRevision
FcsCommit
ChatLogReady
TalkReady
FallbackReason
```

규칙:

- attach당 resource 선택 결과를 한 번 기록한다.
- timer마다 같은 unavailable 로그를 반복하지 않는다.
- readiness가 false에서 true로 바뀔 때 상태를 갱신한다.
- NPC 이름, 원문 및 번역문을 새 진단 로그에 추가하지 않는다.
- 기존 메시지 본문 logging이 있다면 이번 작업에서 개인정보 정책에 맞는지 별도 검토한다.

## 17. 테스트 가능성 개선

현재 `ChatLookupService`는 static handler, timers, `App.GetService` 및 실제 process 접근이 결합되어 직접 단위 테스트하기 어렵다.

이번 migration에서 최소한 다음 순수 로직을 분리한다.

- Talk snapshot에서 `DialogueEntry`를 만드는 변환
- 최초 baseline 및 변경 감지
- resource diagnostics log projection
- dialogue queue bounded enqueue

실제 Sharlayan handler와 FFXIV process를 단위 테스트에 사용하지 않는다. Sharlayan 통합은 manual smoke 또는 별도 opt-in integration test로 검증한다.

## 18. 단위 테스트 계획

### TalkObservationTracker

- 첫 `Last/not visible` snapshot은 baseline이고 신규 entry가 아님
- 첫 `Current/visible` snapshot은 현재 대사로 신규 entry
- 두 번째 다른 text는 신규 entry
- 같은 text와 같은 speaker는 중복
- 같은 text와 다른 speaker는 신규 entry
- `Current/visible → Last/not visible → Current/visible` 동일 pair는 신규 표시
- 빈 text는 queue에 넣지 않음
- unavailable snapshot은 baseline 유지
- reset 후 첫 snapshot도 source와 visibility 정책을 적용
- 한국어, 일본어 및 emoji UTF-8 string 비교

### ChatQueue

- typed dialogue entry enqueue
- 최대 100개 유지
- 초과 시 가장 오래된 entry 제거
- CHATLOG queue capacity 회귀 없음
- 여러 producer에서 thread-safe 동작

### ChatLookupService helper

- Talk unavailable에서 one-time 로그
- CHATLOG와 Talk readiness 독립 처리
- process reconnect에서 tracker reset
- 이전 handler event 무시
- embedded fallback source에서도 timer 시작

### Settings migration

- 기존 `use_internal_address: false` YAML을 안전하게 읽고 RemotePreferred 사용
- 기존 `use_internal_address: true` YAML을 안전하게 읽고 동일하게 RemotePreferred 사용
- legacy 값이 resource mode 선택에 영향을 주지 않음
- 설정 재저장 후 `use_internal_address`가 출력되지 않음
- 설정 재저장 후 다른 translator 설정 보존

### UI helper

- DialogueEntry text 정제
- control payload 제거 후 빈 text 처리
- speaker를 번역 text와 분리하여 보존
- speaker가 있으면 `Speaker: Text` 형식으로 표시
- speaker가 비어 있으면 `Text`만 표시
- ChatMessage 경로의 author도 동일한 speaker 형식으로 표시
- 기존 `DialogueTranslationMethod.MemorySearch` 동작 유지

## 19. 통합 및 수동 테스트

### Network matrix

- 정상 remote v2
- remote 차단, 정상 cache
- remote와 cache 없음, embedded fallback
- 손상된 remote latest
- hash가 다른 immutable manifest
- 오래된 cache와 새 remote revision
- 앱 재시작 후 새 remote revision 선택

### Game matrix

- 게임 실행 전 앱 시작
- 게임 실행 후 attach
- 게임 종료와 재실행
- 일반 NPC Talk 연속 진행
- speaker가 바뀌고 text가 같은 경우
- text가 바뀌고 speaker가 같은 경우
- 대화창 종료 후 잔존 text
- 같은 대사 닫기 및 재열기
- 긴 한국어 및 일본어 대사
- CHATLOG와 Talk 동시 유입

### 기능 회귀

- CHATLOG channel filtering
- BossQuotes 처리
- dialogue 번역 method 전환
- 번역 queue backpressure
- WPF UI responsiveness
- 앱 종료 시 timer와 handler dispose
- Velopack packaged build에서 embedded fallback

## 20. 구현 단계

### Phase 1: Sharlayan 9.1.2 통합

- [x] Hermes v2 지원 Sharlayan package version `9.1.2` 확정
- [x] package reference 갱신
- [x] 새 public API compile 확인
- [x] RemotePreferred configuration 적용
- [x] 기존 obsolete Sharlayan JSON configuration 제거
- [x] resource diagnostics logging 추가

구현 검증:

- `dotnet restore src\IronworksTranslator\IronworksTranslator.sln` 통과
- `dotnet build src\IronworksTranslator\IronworksTranslator.sln -c Debug --no-restore`
  경고 0개, 오류 0개
- `dotnet test tests\IronworksTranslator.Tests\IronworksTranslator.Tests.csproj -c Debug
  --no-build --no-restore` 120개 통과
- resolved package `Sharlayan.Lite 9.1.2` 확인
- Debug output에 `Sharlayan.dll`만 포함되고 FCS assembly는 포함되지 않음

남은 runtime 완료 조건:

- [x] Phase 2 runtime 경로에서 앱이 Hermes JSON을 직접 다운로드하지 않는다.
  사용되지 않는 `HermesAddress` 파일 자체는 Phase 4에서 제거한다.
- [ ] remote, cache 및 embedded 각 source에서 handler가 초기화된다.
- [ ] 실제 게임에서 기존 CHATLOG polling이 유지된다.

### Phase 2: Talk API 전환

상태: **코드 구현 및 자동 검증 완료, 앱 runtime gate 대기**

역사적 blocker:

- NuGet `9.1.2` repository commit: `1f1bc33`
- current Talk API source commit: `3e27261`
- 당시 NuGet V3에 공개된 버전: `8.0.1`, `9.1.2`
- consumer compile 결과: `Reader.CanGetTalk()`, `Reader.GetTalk()`,
  `TalkResult.Source`, `TalkResult.IsVisible` 없음
- 금지: local project reference, 임의 DLL 복사 또는 reflection 기반 consumer workaround

`9.1.4` 해결 및 consumer 검증:

- 완전히 새로운 NuGet cache에서 NuGet.org package restore 통과
- repository metadata commit
  `78d2eccb2025ef16786811b97aed8bbe30dc9552` 일치
- nupkg SHA-256
  `e310c00162d7775f84c66543a67a08410bb771143f58ce78b43251649e2b9d50` 일치
- `net10.0` Talk API contract reflection 확인
- Debug build 경고 0개, 오류 0개
- unit test 130개 통과
- Debug output에 `Sharlayan.dll`만 있고 `FFXIVClientStructs` assembly 없음

- [x] `ALLMESSAGES` custom signature 제거
- [x] `GetString(..., 2048)` 제거
- [x] Sharlayan current-first `GetTalk()` 사용
- [x] attach baseline 처리
- [x] process reconnect reset
- [x] readiness와 one-time logging 갱신
- [x] `"Dialogue window"` sentinel 제거

완료 조건:

- [x] 코드 경로에서 NPC Talk는 raw pointer 없이 읽힌다.
- [x] tracker 단위 테스트에서 attach 시 기존 LastTalk가 신규 번역으로 들어오지 않는다.
- [x] CHATLOG와 Talk readiness 및 timer 시작은 서로 독립적이다.
- [ ] packaged app과 실제 게임에서 CHATLOG/Talk 동시 동작, process 종료 및 재연결을
  검증한다. 이 항목은 Phase 5 release gate다.

### Phase 3: Typed dialogue pipeline

상태: **코드 구현 및 자동 검증 완료, UI/live gate 대기**

- [x] `DialogueEntry` 모델 추가
- [x] `TalkObservationTracker` 추가 및 Phase 2 baseline/reconnect 단위 테스트
- [x] dialogue queue를 typed entry로 변경
- [x] `LastMsg` 중복 상태 제거
- [x] DialogueWindow가 entry text만 번역하도록 변경
- [x] DialogueWindow에 `Speaker: Text` 형식으로 표시
- [x] ChatMessage 경로의 author를 speaker로 전달
- [x] queue, tracker 및 표시 formatter 단위 테스트 추가

구현 검증:

- Debug build 경고 0개, 오류 0개
- unit test 138개 통과
- typed queue의 100개 capacity와 FIFO trim 유지 확인
- 동일 text의 다른 speaker가 별도 entry로 보존됨을 확인
- speaker가 없으면 번역문만, 있으면 `Speaker: Text`가 됨을 확인
- ChatMessage `TranslationText.Author`가 `DialogueEntry.Speaker`로 전달됨을 확인

완료 조건:

- [x] 이름과 text가 pipeline 끝까지 분리되어 보존된다.
- [x] 중복 판정이 `TalkObservationTracker` 한 곳에서 수행된다.
- [x] queue capacity와 처리 순서가 유지된다.
- [x] 실제 WPF DialogueWindow와 게임에서 speaker 표시 및 entry 순서를 확인했다.
  2026-07-26 live screenshot에서 `Krile: ...`, `Koana: ...` 형식과 순차 표시를 확인했다.

### Phase 4: Legacy 정리

상태: **완료**

- [x] `HermesAddress.cs` 삭제
- [x] `UseInternalAddress` property와 runtime 분기 제거
- [x] 기존 YAML의 `use_internal_address`를 값 mapping 없이 안전하게 무시
- [x] local `address.json` 경로 제거
- [x] legacy cache migration에서 불필요한 address 파일 처리 검토
- [x] 사용되지 않는 localization key, using 및 legacy cache migration 제거
- [x] 관련 로그와 문서 갱신

legacy cache migration 검토 결과:

- 기존 migration은 `address.json`을 복사하지 않았고 Hermes v1의 `actions-*`,
  `signatures-*`, `statuses-*`, `structures-*`, `zones-*` JSON만 복사했다.
- Hermes v2는 해당 파일을 사용하지 않으므로 migration 호출과 구현을 제거했다.
- 사용자 디렉터리의 legacy JSON 또는 local `address.json`을 삭제하지는 않는다.

구현 검증:

- Debug build 경고 0개, 오류 0개
- unit test 141개 통과
- `use_internal_address: true`와 `false` settings YAML 모두 역직렬화 통과
- 두 legacy 값 모두 다른 resource mode로 mapping되지 않으며 재직렬화 결과에서
  `use_internal_address`가 사라짐
- production source와 resource에서 `HermesAddress`, `UseInternalAddress`,
  `latest/address.json`, local `address.json` 참조가 없음

완료 조건:

- [x] source에 legacy Hermes consumer가 없다.
- [x] publish 산출물에 legacy Hermes consumer가 없는지 Phase 5에서 확인한다.
- [x] 기존 settings.yaml을 안전하게 읽을 수 있다.
- [x] `use_internal_address` 값이 resource mode 선택에 영향을 주지 않는다.
- [x] 앱 runtime source는 `latest/address.json`을 요청하지 않는다.

### Phase 5: Release 검증

상태: **자동·패키지·remote startup 검증 완료, live/release gate 진행 중**

- [x] 전체 unit test 실행
- [x] Release build 실행
- [x] `publish-release.ps1 -SkipVelopack` 검증
- [x] 실제 Velopack package 구조와 legacy consumer 부재 확인
- [x] packaged app에서 remote `live-verified` revision 초기화 확인
- [ ] packaged app에서 cache/embedded fallback 확인
- [ ] 글로벌 및 한국 client Talk smoke test
- [ ] packaged app에서 CHATLOG/Talk 동시 동작 확인
- [x] packaged app에서 `Speaker: Text` 표시와 entry 순서 확인
- [ ] FFXIV process 종료 및 재연결 확인
- [x] packaged app 정상 종료와 process 정리 확인
- [x] UPDATE 또는 release checklist에 Hermes v2 항목 추가
- [ ] release note에 legacy setting 변경 기록

2026-07-26 검증 결과:

- clean NuGet cache에서 Release build 경고 0개, 오류 0개
- Release unit test 141개 통과
- `publish-release.ps1 -SkipVelopack`와 실제 `vpk pack` 통과
- `vpk pack`이 launcher의 `VelopackApp.Run()`을 확인함
- publish 및 Velopack package에 `FFXIVClientStructs`, `HermesAddress`,
  `latest/address.json`, local `address.json`, `UseInternalAddress` consumer가 없음
- packaged app이 실제 실행 중인 FFXIV에 attach되어 remote `live-verified` revision
  `sha256:419248bf2ef93aa64e72723ea9e97d5503163178dab63e90a8155b359ebcf96d`을
  새 `hermes-v2` cache에 기록함
- 메인 창 close를 통한 정상 종료 후 process가 남지 않았고 암호화 로그가 정상 flush됨
- 최초 smoke에서는 대화 창에 실제 Talk가 나타나지 않았으나, 후속 live screenshot에서
  `Krile: ...`, `Koana: ...` 형식과 entry 순서를 확인함
- CHATLOG/Talk 동시 동작은 아직 확인하지 못함
- local Velopack package는 code signing parameter 없이 생성됐으므로 production
  installer 검증을 대신하지 않음

상세 증거와 남은 blocker는
[Phase 5 validation log](../2026-07-26/2026-07-26-hermes-v2-phase5-validation.md)에 기록한다.

완료 조건:

- packaged app에서 CHATLOG와 Talk가 동작한다.
- Hermes 장애와 네트워크 차단에서 app이 계속 실행된다.
- process reconnect와 app shutdown에서 background callback이 남지 않는다.

## 21. 검증 명령

repository root에서 실행한다.

```powershell
dotnet restore src\IronworksTranslator\IronworksTranslator.sln
dotnet build src\IronworksTranslator\IronworksTranslator.sln -c Debug
dotnet test tests\IronworksTranslator.Tests\IronworksTranslator.Tests.csproj -c Debug
```

release 산출물 검증:

```powershell
.\publish-release.ps1 -SkipVelopack
```

최종 release 전에는 실제 Velopack package와 update path도 기존 checklist에 따라 검증한다.

## 22. 배포 순서

1. 완료된 upstream 기준인 Hermes v2 production과 Sharlayan.Lite `9.1.4`를 확인한다.
2. IronworksTranslator feature branch에서 stable `9.1.4`로 통합한다.
3. unit test, Debug/Release build 및 packaged-app fallback 검증을 수행한다.
4. 실제 게임에서 current Talk, CHATLOG 및 process reconnect를 smoke test한다.
5. IronworksTranslator release를 배포한다.
6. 구버전 app을 위해 legacy `latest/address.json` endpoint는 무기한 유지한다.

Hermes candidate 생성과 production 승격은 이후에도 별도의 수동 live-smoke gate를 따른다.
IronworksTranslator 배포가 해당 gate를 대신하지 않는다.

## 23. Rollback

### 데이터 rollback

- Hermes가 `v2/latest.json`을 이전 immutable revision으로 되돌린다.
- IronworksTranslator는 다음 앱 시작 시 ETag를 확인하고 이전 revision을 사용한다.
- 현재 handler는 실행 중 revision을 바꾸지 않으며 수동 reload 기능을 제공하지 않는다.
- 즉시 rollback revision을 적용해야 하면 사용자에게 앱 재시작을 안내한다.

### 앱 rollback

- 새 Sharlayan package 또는 Talk pipeline 회귀가 있으면 이전 정상 dependency로 새 IronworksTranslator release를 만든다.
- 구버전 app은 유지된 `latest/address.json`을 계속 사용할 수 있다.
- `UseInternalAddress` 제거 후에도 기존 settings.yaml의 다른 설정이 손상되지 않아야 한다.

### 긴급 EmbeddedOnly

`RemotePreferred`는 remote와 verified cache가 모두 실패하면 package embedded manifest로 자동
fallback한다. 일반 사용자에게 `EmbeddedOnly` 설정이나 UI를 제공하지 않는다. 특정 release를
강제로 EmbeddedOnly로 배포해야 하는 상황은 별도의 code/release 결정으로 처리한다.

## 24. 완료 기준

- IronworksTranslator가 Hermes endpoint를 직접 호출하지 않는다.
- `HermesAddress`, `ALLMESSAGES` 및 raw 2048-byte read가 제거된다.
- Sharlayan `RemotePreferred`를 명시적으로 사용한다.
- CHATLOG와 Talk를 Sharlayan 고수준 API로만 읽는다.
- Talk 이름과 text가 typed pipeline에서 보존되고 `Speaker: Text` 형식으로 표시된다.
- source-aware attach baseline, 중복 및 reconnect 동작이 테스트된다.
- remote, cache 및 embedded fallback이 packaged app에서 검증된다.
- 기존 settings.yaml의 `use_internal_address`가 값 mapping 없이 안전하게 제거된다.
- unit test, Debug build 및 release package 검증이 통과한다.
- legacy `latest/address.json` endpoint는 구버전 app을 위해 무기한 유지된다.

## 25. 구현 전 확정 사항

- [확정] Phase 1 Sharlayan.Lite package version: `9.1.2`
- [확정] Phase 2 이상 Sharlayan.Lite package version: `9.1.4`
- [확정] resource configuration: `ResourceMode.RemotePreferred`,
  `HermesV2LatestUri`, `ResourceCacheDirectory`
- [확정] Talk API: `CanGetTalk()` / current-first `GetTalk()`
- [확정] unavailable 표현: `TalkResult.IsAvailable == false`
- [확정] legacy `latest/address.json` endpoint 지원 종료 조건: 무기한 유지
- [확정] speaker 표시: 기존 TextBox에 `Speaker: Text` 형식
- [확정] `UseInternalAddress`: 제거하며 값 mapping 없음
- [확정] user-selectable `EmbeddedOnly`: 제공하지 않음
- [확정] resource revision 갱신: 수동 reload 없이 앱 재시작
- [확정] `GameRegion`: Sharlayan configuration에서 제거
