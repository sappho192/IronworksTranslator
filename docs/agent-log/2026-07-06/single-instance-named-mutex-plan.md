# Named Mutex 기반 단일 실행 차단 구현 계획

## 1. 배경

현재 IronworksTranslator는 앱 전체의 중복 실행을 막지 않는다.

- `App.Main()`은 `VelopackApp.Build().Run()` 이후 바로 WPF 앱을 시작한다.
- 전역 `Mutex`, single-instance guard, 기존 `IronworksTranslator` 프로세스 검사 로직이 없다.
- Setup 설치본, Portable 실행본, Stable 빌드, Beta 빌드가 같은 사용자 데이터 경로를 공유한다.

공유 경로는 다음과 같다.

```text
%APPDATA%\IronworksTranslator\settings.yaml
%LOCALAPPDATA%\IronworksTranslator\logs
%LOCALAPPDATA%\IronworksTranslator\data
```

따라서 여러 인스턴스가 동시에 실행되면 설정 저장 경합, 로그 파일 경합, FFXIV 메모리 polling 중복, 번역 요청 중복이 발생할 수 있다.

---

## 2. 목표

- IronworksTranslator가 이미 실행 중이면 추가 실행을 차단한다.
- Setup, Portable, Stable, Beta를 모두 같은 앱 인스턴스로 취급한다.
- 추가 실행 시 사용자에게 경고 메시지를 띄운 뒤 조용히 종료한다.
- 기존 Velopack 업데이트 흐름과 WPF 앱 시작 흐름을 깨지 않게 한다.
- 단일 실행 차단 로직을 작고 테스트 가능한 helper로 분리한다.

## 3. 비목표

- 이미 실행 중인 창을 foreground로 가져오는 기능은 이번 범위에 넣지 않는다.
- Stable과 Beta를 동시에 실행할 수 있게 분리하지 않는다.
- 앱 데이터 경로를 Stable/Beta/Portable별로 분리하지 않는다.
- 프로세스 이름 기반 검사로 대체하지 않는다.

---

## 4. 구현 방침

`App.Main()` 시작 직후 named mutex를 획득한다.

권장 mutex 이름:

```text
Global\Sappho192.IronworksTranslator
```

이 이름은 Stable, Beta, Setup, Portable이 모두 공유한다. 앱이 관리자 권한으로 실행되는 구조이므로 `Global\` namespace를 사용해 세션 경계에서 의도를 명확히 한다.

단, `Global\` namespace 생성 권한 문제가 실제 사용자 환경에서 확인되면 `Local\Sappho192.IronworksTranslator`로 fallback하는 방식을 검토한다.

---

## 5. 코드 구조

새 helper를 추가한다.

```text
src/IronworksTranslator/Utils/SingleInstanceGuard.cs
```

역할:

- named mutex 생성
- 최초 인스턴스 여부 반환
- 앱 종료 시 mutex release/dispose

예상 형태:

```csharp
namespace IronworksTranslator.Utils
{
    public sealed class SingleInstanceGuard : IDisposable
    {
        private const string MutexName = @"Global\Sappho192.IronworksTranslator";
        private readonly Mutex _mutex;
        private bool _ownsMutex;

        public SingleInstanceGuard()
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out _ownsMutex);
        }

        public bool IsFirstInstance => _ownsMutex;

        public void Dispose()
        {
            if (_ownsMutex)
            {
                _mutex.ReleaseMutex();
                _ownsMutex = false;
            }

            _mutex.Dispose();
        }
    }
}
```

`App.Main()`에는 guard를 가장 앞쪽에 둔다.

```csharp
[STAThread]
private static void Main(string[] args)
{
    using var singleInstanceGuard = new SingleInstanceGuard();
    if (!singleInstanceGuard.IsFirstInstance)
    {
        MessageBox.Show(
            Localizer.GetString("app.single_instance.already_running"),
            Localizer.GetString("app.single_instance.title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        return;
    }

    VelopackApp.Build().Run();

    var app = new App();
    app.InitializeComponent();
    app.Run();
}
```

주의: 이 시점은 아직 Generic Host와 앱 설정이 로드되기 전이다. `Localizer.GetString(...)` 사용이 안전하지 않다면 이 메시지는 최소한의 하드코딩 문자열로 처리하거나, `Localizer.ChangeLanguage("ko-KR")`가 호출되기 전에도 동작하는지 확인한 뒤 적용한다.

---

## 6. UI 문자열

사용자에게 보이는 문자열을 추가한다면 `ko-KR.yaml`과 `en-US.yaml`을 함께 수정한다.

권장 키:

```yaml
app.single_instance.title
app.single_instance.already_running
```

권장 문구:

```text
ko-KR:
app.single_instance.title: "IronworksTranslator 실행 중"
app.single_instance.already_running: "IronworksTranslator가 이미 실행 중입니다."

en-US:
app.single_instance.title: "IronworksTranslator is running"
app.single_instance.already_running: "IronworksTranslator is already running."
```

단, `Main()` 초기에 localization 초기화가 보장되지 않으면 hardcoded fallback을 사용한다.

---

## 7. 예외 처리 정책

named mutex 생성에서 예외가 발생하면 앱을 계속 실행하지 않는 쪽이 안전하다.

권장 정책:

- mutex 생성 성공, 최초 인스턴스: 정상 실행
- mutex 생성 성공, 중복 인스턴스: 안내 후 종료
- mutex 생성 실패: 안내 후 종료

초기 로거가 아직 없기 때문에 mutex 생성 실패는 `MessageBox.Show(...)`로 알리고 종료한다. 로깅은 앱 초기화 이후에만 가능하므로 이 단계에서는 필수로 보지 않는다.

---

## 8. 테스트 계획

## 8.1 단위 테스트

`SingleInstanceGuard`에 mutex 이름을 주입할 수 있게 만들면 테스트가 쉽다.

테스트 전용 이름:

```text
Local\Sappho192.IronworksTranslator.Tests.{Guid}
```

테스트 케이스:

1. 첫 guard는 `IsFirstInstance == true`
2. 첫 guard가 살아 있는 동안 두 번째 guard는 `IsFirstInstance == false`
3. 첫 guard dispose 후 새 guard는 다시 `IsFirstInstance == true`

테스트 helper constructor 예시:

```csharp
internal SingleInstanceGuard(string mutexName)
{
    _mutex = new Mutex(initiallyOwned: true, mutexName, out _ownsMutex);
}
```

프로덕션 constructor는 기본 mutex 이름을 사용한다.

## 8.2 수동 테스트

1. Setup 설치본을 실행한다.
2. 같은 Setup 바로가기를 다시 실행한다.
3. 두 번째 실행에서 "이미 실행 중" 메시지가 뜨고 종료되는지 확인한다.
4. Setup 설치본이 실행 중인 상태에서 Portable `IronworksTranslator.exe`를 실행한다.
5. Portable도 같은 메시지를 띄우고 종료되는지 확인한다.
6. Beta 설치본 또는 Beta Portable이 있다면 같은 방식으로 확인한다.
7. 첫 번째 실행본을 종료한 뒤 다시 실행하면 정상 시작되는지 확인한다.

---

## 9. 리스크와 대응

| 리스크 | 설명 | 대응 |
|---|---|---|
| Localization 초기화 전 메시지 | `Main()` 초기에 i18n 서비스가 아직 구성되지 않았을 수 있음 | hardcoded fallback 또는 localization 초기화 가능 여부 확인 |
| `Global\` mutex 권한 | 일부 환경에서 global mutex 생성 권한 문제가 생길 수 있음 | 실패 시 `Local\` fallback 검토 |
| Velopack update hook | `VelopackApp.Build().Run()`보다 앞에서 차단하면 update helper 실행에 영향 가능성 | Velopack hook 인자가 있는지 확인하고, 필요하면 Velopack 처리 후 일반 실행 경로에서 guard 획득 |
| 창 foreground 미지원 | 사용자는 두 번째 실행 후 기존 창이 앞으로 오길 기대할 수 있음 | v1에서는 안내 후 종료, 필요 시 후속 작업으로 IPC/Win32 foreground 처리 |

---

## 10. 권장 작업 순서

1. `SingleInstanceGuard` helper 추가
2. 테스트 가능한 internal constructor 추가
3. `App.Main()` 초기에 guard 적용
4. localization 초기화 전 메시지 정책 결정
5. `ko-KR.yaml`, `en-US.yaml` 문자열 추가 또는 hardcoded fallback 확정
6. `SingleInstanceGuardTests` 추가
7. `dotnet test tests\IronworksTranslator.Tests\IronworksTranslator.Tests.csproj -c Debug`
8. Setup + Portable 동시 실행 수동 테스트
9. Beta + Stable 동시 실행 수동 테스트

---

## 11. 완료 기준

- 앱이 이미 실행 중이면 추가 실행이 차단된다.
- Setup, Portable, Stable, Beta가 같은 mutex를 공유한다.
- 중복 실행 시 사용자에게 명확한 안내가 표시된다.
- 첫 실행본 종료 후 다시 실행할 수 있다.
- 단위 테스트가 mutex 획득/해제 동작을 검증한다.
- 기존 업데이트 확인, 설정 로드, 로그 생성 흐름에 회귀가 없다.
