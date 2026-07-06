# AGENTS.md

이 문서는 IronworksTranslator 저장소에서 작업하는 에이전트를 위한 프로젝트 지침입니다.
루트에서 하위 전체 저장소에 적용됩니다.

## 프로젝트 개요

- IronworksTranslator는 Final Fantasy XIV 채팅과 NPC 대사를 실시간 번역하는 Windows WPF 데스크톱 앱입니다.
- 주 앱은 `src/IronworksTranslator/IronworksTranslator.csproj`이고 `net8.0-windows`, `UseWPF`, nullable, implicit usings를 사용합니다.
- 솔루션은 `src/IronworksTranslator/IronworksTranslator.sln`에 있으며 앱, `tests/IronworksTranslator.Tests`, `tools/IronworksLogDecryptor`를 포함합니다.
- .NET SDK는 `global.json` 기준 `8.0.100` 이상 최신 minor roll-forward를 사용합니다.
- 앱 릴리스와 업데이트는 GitVersion + Velopack 기반입니다. Visual Studio Publish 대신 `publish-release.ps1`를 사용합니다.

## 주요 명령

PowerShell 기준:

```powershell
dotnet restore src\IronworksTranslator\IronworksTranslator.sln
dotnet build src\IronworksTranslator\IronworksTranslator.sln -c Debug
dotnet test tests\IronworksTranslator.Tests\IronworksTranslator.Tests.csproj -c Debug
```

릴리스 패키징:

```powershell
.\publish-release.ps1
.\publish-release.ps1 -SkipVelopack
```

Velopack 릴리스 생성에는 `vpk` CLI가 필요합니다.

```powershell
dotnet tool update --global vpk --version 1.2.0
```

## 구조와 책임

- `App.xaml.cs`: Generic Host와 DI 구성, localization 등록, Serilog 초기화, 앱 시작/종료, WatchDog 실행.
- `Services/ApplicationHostService.cs`: 설정 로드, 앱 언어 적용, 메인 윈도우 활성화.
- `Services/FFXIV/`: Sharlayan 기반 FFXIV 프로세스/메모리 조회와 채팅/대사 폴링.
- `Models/ChatQueue.cs`: 채팅과 대사 큐. 큐 용량과 중복 방지 로직을 보존하세요.
- `Models/Settings/`: YAML로 저장되는 사용자 설정 모델. `IronworksSettings.Instance`가 런타임 전역 설정입니다.
- `ViewModels/`: CommunityToolkit.Mvvm 기반 WPF ViewModel. `[ObservableProperty]`, `[RelayCommand]`, Messenger, Dispatcher 패턴을 따릅니다.
- `Views/`: WPF XAML과 code-behind. WPF-UI 컨트롤과 기존 리소스 구조를 유지합니다.
- `Utils/Translator/`: Papago, DeepL API, 내부 Ja-Ko, MiLMMT 번역기 구현. 추상 기반 클래스는 `Utils/Translator/TranslatorBase.cs` 파일에 있지만 namespace는 `IronworksTranslator.Utils.Translators`입니다.
- `Resources/Strings/*.yaml`: UI 문자열 리소스. 사용자에게 보이는 문자열을 추가하거나 바꾸면 `ko-KR.yaml`과 `en-US.yaml`을 함께 갱신하세요.
- `Utils/AppPaths.cs`: 사용자 데이터 경로. 설정은 `%APPDATA%\IronworksTranslator`, 로그/모델/데이터는 `%LOCALAPPDATA%\IronworksTranslator` 아래에 둡니다.

## 코딩 규칙

- 기존 C# 스타일을 우선합니다. file-scoped namespace가 아니라 block-scoped namespace를 주로 사용합니다.
- nullable warning을 새로 늘리지 마세요. 기존 `#pragma warning disable` 범위는 필요한 경우에만 좁게 유지합니다.
- CommunityToolkit.Mvvm source generator 패턴을 따릅니다. 설정/화면 상태는 기존처럼 private backing field에 `[ObservableProperty]`를 붙이고 필요한 partial change hook을 추가합니다.
- 설정 모델에서 값 변경 후 즉시 저장되어야 하는 속성은 기존처럼 partial `On...Changed` 메서드에 `[SaveSettingsOnChange]`를 사용합니다.
- 컬렉션 설정을 추가할 때는 파일 저장 트리거가 누락되지 않도록 `TranslatorSettings.InitializeCollectionListeners()` 같은 기존 listener 패턴을 확인하세요.
- WPF UI 객체는 UI thread에서만 변경합니다. 백그라운드 작업, timer, queue 처리 중 UI 접근이 필요하면 `Application.Current.Dispatcher`를 사용합니다.
- 장시간 작업과 번역 호출은 UI thread를 막지 않게 유지합니다. 기존 semaphore/queue backpressure를 우회하지 마세요.
- 번역기는 실패하거나 지원하지 않는 언어를 받으면 로그를 남기고 원문을 반환하는 기존 동작을 유지합니다.
- 새 번역 엔진을 추가할 때는 enum, converter, 설정 UI, DI 등록, 재번역 메뉴, 테스트를 함께 검토합니다.
- 로컬 모델, tokenizer, 로그, 사용자 설정, API 키, `secrets/`, `bin/`, `obj/`, `publish/`, `Releases/` 산출물을 커밋하지 마세요.

## 테스트 지침

- 테스트는 xUnit v3를 사용하며 `tests/IronworksTranslator.Tests` 아래에 기능 영역별로 둡니다.
- FFXIV 프로세스, 실제 네트워크, 브라우저 렌더링, 대형 모델 파일이 필요한 테스트는 기본 단위 테스트에 넣지 마세요.
- 번역기나 외부 서비스 작업은 parsing, prompt rendering, fallback, error handling처럼 순수하거나 격리 가능한 로직을 우선 테스트합니다.
- 채팅 처리 변경은 `ChatWindowViewModelHelperTests`, `ChatQueueTests` 같은 기존 helper 중심 테스트를 확장하는 것을 선호합니다.
- 설정 변경은 기본값, YAML 호환성, 정규화, listener 저장 동작을 함께 고려합니다.

## 릴리스와 업데이트 주의사항

- GitVersion 속성은 `Directory.Build.props`와 앱 csproj에 설정되어 있습니다. 버전 스탬프를 우회하지 마세요.
- 릴리스 빌드는 `publish-release.ps1`가 `dotnet restore`, `dotnet build -c Release`, `dotnet publish`, `vpk pack` 순서로 처리합니다.
- Velopack 업데이트 흐름을 바꾸는 경우 `UPDATE-TEST-CHECKLIST.md`와 `PUBLISH-README.md`를 함께 확인하고 갱신하세요.
- `WatchDogMain.exe`, bundled unidic data, fonts, embedded string resources, encrypted log public key는 앱 실행/배포에 필요한 자산입니다. csproj의 copy/resource 설정을 변경할 때 실제 publish 산출물을 확인하세요.

## 작업 전 체크리스트

- 시작할 때 `git status --short --branch`로 사용자 변경을 확인하고, 관련 없는 변경은 되돌리지 마세요.
- 파일 검색은 `rg`와 `rg --files`를 우선 사용합니다.
- 변경 범위가 UI 문자열, 설정, 번역 엔진, 릴리스, 사용자 데이터 위치 중 하나에 닿으면 관련 문서와 테스트를 함께 확인합니다.
- 작업 후 가능한 최소 검증 명령을 실행하고, 실행하지 못한 검증은 이유를 남깁니다.
