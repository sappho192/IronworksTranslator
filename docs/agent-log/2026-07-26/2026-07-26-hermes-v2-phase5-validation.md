# Hermes v2 Phase 5 검증 기록

## 1. 범위

- 검증 일자: 2026-07-26
- 검증 기준 commit:
  `4e4869c0d18acf4d4bb5221246ad12255f42939e`
- branch: `feat/sharlayan912`
- app version: `1.3.0`
- package dependency: `Sharlayan.Lite 9.1.4`
- .NET SDK: `10.0.301`
- Velopack CLI: `1.2.0`

이 기록은 Phase 5에서 완료한 자동 검증, package 구조 검증 및 제한된 packaged-app
smoke의 증거다. Sharlayan upstream smoke는 이 앱의 release readiness를 대신하지 않으며,
아래의 미완료 live gate가 모두 끝나기 전에는 production release-ready로 간주하지 않는다.

## 2. Clean restore, build and test

새 NuGet cache를 사용했다.

```text
C:\Users\tikim\AppData\Local\Temp\ironworks-nuget-914-f49615fe0c45486c9be4c9f65a813e47
```

실행 명령:

```powershell
$env:NUGET_PACKAGES = 'C:\Users\tikim\AppData\Local\Temp\ironworks-nuget-914-f49615fe0c45486c9be4c9f65a813e47'
dotnet restore src\IronworksTranslator\IronworksTranslator.sln
dotnet build src\IronworksTranslator\IronworksTranslator.sln -c Release --no-restore
dotnet test tests\IronworksTranslator.Tests\IronworksTranslator.Tests.csproj -c Release --no-build --no-restore
```

결과:

- restore PASS
- Release build PASS, warning 0, error 0
- Release tests PASS, 141/141
- project assets가 fresh cache의 `Sharlayan.Lite/9.1.4`를 선택함

## 3. Publish 검증

실행 명령:

```powershell
.\publish-release.ps1 -SkipVelopack `
  -OutputDir 'publish\phase5-hermes-v2' `
  -VelopackOutputDir 'Releases\phase5-hermes-v2'
```

결과:

- PASS
- publish path:
  `publish\phase5-hermes-v2\IronworksTranslator (1.3.0)`
- product version:
  `1.3.0+4e4869c0d18acf4d4bb5221246ad12255f42939e`
- 42 files, 103,905,480 bytes
- main executable, launcher, native probe, watchdog, llama.cpp license 및 CUDA/Vulkan
  runtime files 존재
- `FFXIVClientStructs`, `HermesAddress`, `latest/address.json`, local
  `address.json`, `UseInternalAddress`, `ALLMESSAGES` consumer 부재

## 4. Velopack package 검증

기존 publish 산출물에 실제 Velopack packaging을 실행했다.

```powershell
.\publish-release.ps1 -SkipClean `
  -OutputDir 'publish\phase5-hermes-v2' `
  -VelopackOutputDir 'Releases\phase5-hermes-v2'
```

결과:

- PASS
- `vpk pack`이 launcher의 `VelopackApp.Run()`을 확인함
- full nupkg 46 entries, portable archive 43 entries
- 양쪽 산출물에 필수 executable, watchdog, license 및 CUDA/Vulkan runtime 존재
- 양쪽 산출물에 `FFXIVClientStructs`, `HermesAddress`, `address.json` 부재
- `releases.win.json`의 version과 full nupkg hash가 실제 산출물과 일치

산출물:

| Artifact | Size | SHA-256 |
| --- | ---: | --- |
| `Sappho192.IronworksTranslator-1.3.0-full.nupkg` | 55,772,757 | `E5C5939CD5F1B3C2224F1B03879D9E76602453B86B1FC4730D4846D607AF10BC` |
| portable zip | 55,681,699 | `C6A356B02EA6AF1DFF8516A3D25254C7645B8C84E51B60586E5A627CC0B107C4` |
| Setup executable | 60,376,661 | `91D5F79468CF7E865B9B4E7DA8255854AA732CC15C09DC9076B1C107285B12A1` |
| `releases.win.json` | - | `C0E2DAA2534D7AC2B15D266BE0A37EDAE4C77FF53F9049F1D4358C94CD6D8719` |

이 로컬 package는 code-signing parameter 없이 생성됐다. 구조와 실행 smoke에는 사용할 수
있지만, production signing·installer·update gate를 통과한 산출물은 아니다.

## 5. Packaged-app remote startup smoke

publish 디렉터리의 `IronworksTranslator.exe`를 실행했다. UAC 승격 후 앱 process가
응답 상태로 유지됐고 메인 창, ChatWindow 및 DialogueWindow가 열렸다. 실행 중인 FFXIV
process도 유지됐다.

앱은 다음 위치에 새로운 Hermes v2 cache를 기록했다.

```text
%LOCALAPPDATA%\IronworksTranslator\data\sharlayan\hermes-v2
```

확인 결과:

- `latest.etag`, `latest.json`, `manifest` 생성
- validation status: `live-verified`
- selected revision:
  `sha256:419248bf2ef93aa64e72723ea9e97d5503163178dab63e90a8155b359ebcf96d`
- cached manifest SHA-256:
  `419248BF2EF93AA64E72723EA9E97D5503163178DAB63E90A8155B359EBCF96D`
- revision과 manifest SHA-256 일치
- 기존 Hermes v1 사용자 파일은 삭제하거나 변경하지 않음

최초 startup smoke 동안에는 DialogueWindow에 실제 대사가 표시되지 않았다. 이후 사용자가
제공한 live screenshot에서 overlay에 `Krile: ...`와 `Koana: ...`가 순서대로 남고, 현재
게임 Talk가 Koana의 같은 대사를 표시하는 것을 확인했다.

Screenshot evidence:

- source file: `codex-clipboard-15135126-053d-4b53-8a9b-f458e257eec3.png`
- dimensions: 760 x 356
- SHA-256:
  `589BF80FF3457129320687A3C05FCBF507F4DE3041AE2082590ACF5D4B19BA14`
- PASS: speaker와 text가 분리되지 않고 `Speaker: Text` 형식으로 표시됨
- PASS: 이전 Krile entry 다음에 현재 Koana entry가 순서대로 표시됨

이 screenshot은 표준 Talk의 화면 표시 경로와 entry 순서를 검증한다. CHATLOG와 Talk의 동시
동작, 번역 의미 정확성, attach baseline 또는 reconnect 동작까지 증명하지는 않는다.

## 6. 정상 종료

메인 창의 정상 close 동작으로 앱을 종료했다.

결과:

- 앱 process가 약 1초 안에 종료됨
- 남은 IronworksTranslator process 없음
- 새 암호화 로그 파일이 정상적으로 flush됨

이 결과는 앱의 정상 종료 경로만 검증한다. FFXIV process 종료에 따른 handler 정리와 게임
재실행 후 reconnect는 검증하지 않았다.

## 7. 남은 release blockers

- packaged app에서 실제 CHATLOG와 표준 Talk 동시 수집·번역
- attach baseline과 같은 name/text Talk 재등장 live 검증
- verified cache fallback
- clean-cache embedded fallback
- 글로벌 client와 한국 client Talk smoke
- FFXIV process 종료, handler/poller 정리 및 게임 재실행 후 reconnect
- production signing이 적용된 installer의 최초 실행과 update/restart
- release note의 `UseInternalAddress` 제거 및 앱 재시작 기반 resource revision 갱신 안내

위 항목은 자동 테스트, Sharlayan upstream smoke 또는 이번 unsigned local package 검증으로
대체되지 않는다.
