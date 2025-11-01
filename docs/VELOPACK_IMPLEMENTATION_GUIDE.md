# Velopack 자동 업데이트 구현 가이드

## 목차
1. [Velopack 개요](#velopack-개요)
2. [구현 단계](#구현-단계)
3. [프로젝트 설정](#프로젝트-설정)
4. [코드 구현](#코드-구현)
5. [빌드 및 패키징](#빌드-및-패키징)
6. [GitHub Actions 설정](#github-actions-설정)
7. [배포 및 테스트](#배포-및-테스트)
8. [문제 해결](#문제-해결)

---

## Velopack 개요

### Velopack이란?
Velopack은 크로스 플랫폼 데스크톱 애플리케이션용 설치 및 자동 업데이트 프레임워크입니다.

### 주요 특징
- **Zero Config**: 컴파일러 출력만으로 설치 관리자와 업데이트 패키지를 자동 생성
- **고속 성능**: Rust 기반으로 개발되어 빠른 성능 제공
- **델타 패키지**: 변경된 부분만 다운로드하여 업데이트 시간 단축
- **다중 플랫폼**: Windows, macOS, Linux 지원
- **GitHub Releases 통합**: GitHub Releases를 업데이트 소스로 사용 가능

### IronworksTranslator에 적합한 이유
1. **WPF 완벽 지원**: .NET 8.0 WPF 앱과 호환
2. **GitHub Releases 활용**: 현재 사용 중인 GitHub 기반 배포 방식과 자연스러운 통합
3. **간단한 구현**: 최소한의 코드로 자동 업데이트 기능 구현
4. **사용자 경험**: 앱 시작 시 자동으로 업데이트 확인 및 적용

---

## 구현 단계

### 전체 프로세스
```
1. NuGet 패키지 설치
   ↓
2. App.xaml 및 App.xaml.cs 수정
   ↓
3. 업데이트 체크 로직 구현
   ↓
4. 프로젝트 파일 설정
   ↓
5. GitHub Actions 워크플로우 생성
   ↓
6. 빌드 및 배포
```

---

## 프로젝트 설정

### 1. NuGet 패키지 설치

`IronworksTranslator.csproj` 파일에 Velopack 패키지를 추가합니다:

```xml
<PackageReference Include="Velopack" Version="0.0.1298" />
```

또는 터미널에서 설치:
```bash
cd src/IronworksTranslator
dotnet add package Velopack
```

### 2. App.xaml 수정

Velopack은 업데이트 적용 시 WPF 오버헤드를 피하기 위해 커스텀 Main 메서드를 권장합니다.

`App.xaml`의 Build Action을 **Page**로 변경합니다:

`IronworksTranslator.csproj` 파일에 다음을 추가:

```xml
<ItemGroup>
  <ApplicationDefinition Remove="App.xaml"/>
  <Page Include="App.xaml"/>
</ItemGroup>

<PropertyGroup>
  <StartupObject>IronworksTranslator.App</StartupObject>
</PropertyGroup>
```

### 3. .csproj 파일에 버전 정보 추가

자동 버전 관리를 위해 다음 프로퍼티를 추가합니다:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <AssemblyVersion>$(Version)</AssemblyVersion>
  <FileVersion>$(Version)</FileVersion>
</PropertyGroup>
```

---

## 코드 구현

### 1. App.xaml.cs에 Main 메서드 추가

기존 `App.xaml.cs` 파일을 수정하여 Velopack 초기화 코드를 추가합니다:

```csharp
using Velopack;

namespace IronworksTranslator
{
    public partial class App
    {
        /// <summary>
        /// 애플리케이션 진입점
        /// Velopack 업데이트 처리를 위한 커스텀 Main 메서드
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                // Velopack 초기화 - 업데이트 설치, 언인스톨 등의 작업 처리
                VelopackApp.Build()
                    .WithFirstRun((v) =>
                    {
                        // 첫 실행 시 수행할 작업 (선택사항)
                        Log.Information($"First run of version {v}");
                    })
                    .WithRestarted((v) =>
                    {
                        // 업데이트 후 재시작 시 수행할 작업 (선택사항)
                        Log.Information($"Restarted to version {v} after update");
                    })
                    .Run();

                // WPF 애플리케이션 시작
                App app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                // 초기화 실패 시 로깅
                MessageBox.Show($"Application failed to start: {ex.Message}",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 기존 OnStartup 메서드는 유지
        private void OnStartup(object sender, StartupEventArgs e)
        {
            InitLogger();
            SetupUnhandledExceptionHandlers();

            try
            {
                // 업데이트 체크 (백그라운드에서 실행)
                _ = CheckForUpdatesAsync();

                _host.Start();
                var chatWindow = GetService<ChatWindow>();
                chatWindow.Show();
                var dialogueWindow = GetService<DialogueWindow>();
                BootWatchDog();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start host.");
                return;
            }
        }

        // 나머지 기존 코드...
    }
}
```

### 2. 업데이트 체크 메서드 구현

`App.xaml.cs`에 업데이트 체크 로직을 추가합니다:

```csharp
using Velopack;
using Velopack.Sources;

namespace IronworksTranslator
{
    public partial class App
    {
        /// <summary>
        /// 앱 시작 시 업데이트 확인 및 다운로드
        /// </summary>
        private static async Task CheckForUpdatesAsync()
        {
            try
            {
                // GitHub Releases에서 업데이트 확인
                var mgr = new UpdateManager(
                    new GithubSource("https://github.com/sappho192/IronworksTranslator",
                    null,  // 인증 토큰 (공개 리포지토리는 null)
                    false) // prerelease 포함 여부 (false = stable만)
                );

                Log.Information("Checking for updates...");

                // 새 버전 확인
                var updateInfo = await mgr.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    Log.Information("No updates available.");
                    return;
                }

                Log.Information($"Update available: v{updateInfo.TargetFullRelease.Version}");

                // 업데이트 다운로드
                await mgr.DownloadUpdatesAsync(updateInfo, progress =>
                {
                    // 진행률 로깅 (선택사항)
                    Log.Debug($"Download progress: {progress}%");
                });

                Log.Information("Update downloaded successfully.");

                // 사용자에게 업데이트 알림
                var result = MessageBox.Show(
                    Localizer.GetString("update.available.message") ??
                        $"A new version (v{updateInfo.TargetFullRelease.Version}) is available.\n\nWould you like to restart and update now?",
                    Localizer.GetString("update.available.title") ?? "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information
                );

                if (result == MessageBoxResult.Yes)
                {
                    // 업데이트 적용 및 재시작
                    mgr.ApplyUpdatesAndRestart(updateInfo);
                }
                else
                {
                    Log.Information("User postponed the update.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check for updates.");
            }
        }
    }
}
```

### 3. (선택사항) UpdateManager를 DI 컨테이너에 등록

더 나은 관리를 위해 UpdateManager를 서비스로 등록할 수 있습니다:

```csharp
// App.xaml.cs의 ConfigureServices에 추가
services.AddSingleton<UpdateManager>(sp =>
{
    return new UpdateManager(
        new GithubSource("https://github.com/sappho192/IronworksTranslator", null, false)
    );
});
```

### 4. (선택사항) 설정 페이지에 수동 업데이트 버튼 추가

`SettingsPage`에 수동으로 업데이트를 확인할 수 있는 버튼을 추가할 수 있습니다:

```csharp
// SettingsViewModel.cs
[RelayCommand]
private async Task CheckForUpdates()
{
    try
    {
        var mgr = App.GetService<UpdateManager>();
        var updateInfo = await mgr.CheckForUpdatesAsync();

        if (updateInfo == null)
        {
            // 업데이트 없음 메시지 표시
            MessageBox.Show(
                Localizer.GetString("update.no_updates") ?? "You are using the latest version.",
                Localizer.GetString("update.title") ?? "Update",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        else
        {
            // 업데이트 다운로드 및 적용
            await mgr.DownloadUpdatesAsync(updateInfo);

            var result = MessageBox.Show(
                Localizer.GetString("update.restart_now") ?? "Update downloaded. Restart now?",
                Localizer.GetString("update.title") ?? "Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                mgr.ApplyUpdatesAndRestart(updateInfo);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Manual update check failed.");
        MessageBox.Show(
            Localizer.GetString("update.error") ?? "Failed to check for updates.",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }
}
```

---

## 빌드 및 패키징

### 1. Velopack CLI 도구 설치

Velopack CLI (`vpk`) 도구를 글로벌로 설치합니다:

```bash
dotnet tool install -g vpk
```

설치 확인:
```bash
vpk --version
```

### 2. 애플리케이션 퍼블리시

Self-contained 방식으로 애플리케이션을 빌드합니다:

```bash
cd src/IronworksTranslator
dotnet publish -c Release --self-contained -r win-x64 -o ../../publish
```

주요 옵션:
- `-c Release`: Release 구성으로 빌드
- `--self-contained`: .NET 런타임 포함
- `-r win-x64`: Windows 64비트 타겟
- `-o ../../publish`: 출력 디렉토리

### 3. Velopack 패키지 생성

퍼블리시된 파일로 Velopack 패키지를 생성합니다:

```bash
# 프로젝트 루트 디렉토리에서 실행
vpk pack -u IronworksTranslator -v 1.0.0 -p publish -o releases
```

주요 옵션:
- `-u`: 고유 식별자 (앱 이름)
- `-v`: 버전 번호
- `-p`: 퍼블리시된 파일 경로
- `-o`: 출력 디렉토리 (releases 폴더에 생성됨)

생성되는 파일:
- `IronworksTranslator-1.0.0-win-Setup.exe`: 설치 프로그램
- `IronworksTranslator-1.0.0-win-full.nupkg`: 전체 패키지
- `RELEASES`: 버전 정보 파일

### 4. 델타 패키지 생성 (업데이트용)

버전 업데이트 시 이전 릴리스에서 델타 패키지를 생성합니다:

```bash
# 이전 릴리스 다운로드
vpk download github --repoUrl https://github.com/sappho192/IronworksTranslator -o releases

# 새 버전 패키징 (델타 자동 생성)
vpk pack -u IronworksTranslator -v 1.1.0 -p publish -o releases
```

델타 패키지는 변경된 부분만 포함하여 업데이트 다운로드 크기를 줄입니다.

---

## GitHub Actions 설정

### 1. 워크플로우 파일 생성

`.github/workflows/release.yml` 파일을 생성합니다:

```yaml
name: Build and Release

on:
  push:
    tags:
      - 'v*.*.*'  # v1.0.0 형식의 태그에서 실행

jobs:
  build-and-release:
    runs-on: windows-latest

    steps:
    - name: Checkout code
      uses: actions/checkout@v4
      with:
        fetch-depth: 0  # 전체 히스토리 가져오기

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'

    - name: Get version from tag
      id: get-version
      shell: bash
      run: |
        VERSION=${GITHUB_REF#refs/tags/v}
        echo "version=$VERSION" >> $GITHUB_OUTPUT
        echo "Version: $VERSION"

    - name: Restore dependencies
      run: dotnet restore src/IronworksTranslator/IronworksTranslator.csproj

    - name: Build and Publish
      run: |
        dotnet publish src/IronworksTranslator/IronworksTranslator.csproj `
          -c Release `
          --self-contained `
          -r win-x64 `
          -o publish `
          /p:Version=${{ steps.get-version.outputs.version }}

    - name: Install Velopack
      run: dotnet tool install -g vpk

    - name: Download Previous Releases (for delta packages)
      run: |
        vpk download github `
          --repoUrl https://github.com/${{ github.repository }} `
          --token ${{ secrets.GITHUB_TOKEN }} `
          -o releases
      continue-on-error: true  # 첫 릴리스에서는 이전 버전이 없으므로 에러 무시

    - name: Create Velopack Release
      run: |
        vpk pack `
          -u IronworksTranslator `
          -v ${{ steps.get-version.outputs.version }} `
          -p publish `
          -o releases

    - name: Upload to GitHub Releases
      run: |
        vpk upload github `
          --repoUrl https://github.com/${{ github.repository }} `
          --token ${{ secrets.GITHUB_TOKEN }} `
          --publish `
          --releaseName "IronworksTranslator v${{ steps.get-version.outputs.version }}" `
          --tag ${{ github.ref_name }}

    - name: Upload artifacts
      uses: actions/upload-artifact@v4
      with:
        name: velopack-releases
        path: releases/
```

### 2. 워크플로우 주요 단계 설명

1. **트리거 설정**: `v*.*.*` 형식의 Git 태그 푸시 시 자동 실행
2. **환경 설정**: Windows 환경에서 .NET 8.0 설치
3. **버전 추출**: Git 태그에서 버전 번호 추출 (v1.0.0 → 1.0.0)
4. **빌드**: Self-contained 방식으로 애플리케이션 빌드
5. **이전 릴리스 다운로드**: 델타 패키지 생성을 위한 이전 버전 다운로드
6. **패키지 생성**: Velopack 패키지 및 델타 패키지 생성
7. **업로드**: GitHub Releases에 자동 업로드 및 릴리스 생성

### 3. 릴리스 생성 방법

```bash
# 버전 태그 생성
git tag v1.0.0

# 태그 푸시
git push origin v1.0.0
```

GitHub Actions가 자동으로 실행되어 릴리스를 생성합니다.

---

## 배포 및 테스트

### 1. 첫 릴리스 배포

```bash
# 1. 버전 태그 생성
git tag v1.0.0
git push origin v1.0.0

# 2. GitHub Actions 워크플로우 확인
# https://github.com/sappho192/IronworksTranslator/actions

# 3. GitHub Releases 확인
# https://github.com/sappho192/IronworksTranslator/releases
```

### 2. 사용자 설치

사용자는 GitHub Releases 페이지에서 `IronworksTranslator-1.0.0-win-Setup.exe`를 다운로드하여 설치합니다.

### 3. 업데이트 테스트

새 버전을 릴리스하여 자동 업데이트를 테스트합니다:

```bash
# 1. 코드 수정 및 커밋
git add .
git commit -m "Add new feature"

# 2. 새 버전 태그 생성
git tag v1.1.0
git push origin v1.1.0

# 3. 기존 설치된 앱 실행
# - 앱 시작 시 자동으로 업데이트 확인
# - 업데이트 다운로드 및 설치 제안
# - 재시작 시 새 버전으로 업데이트됨
```

### 4. 로컬 테스트 (개발 환경)

로컬에서 Velopack 패키지를 테스트하려면:

```bash
# 1. 퍼블리시
dotnet publish -c Release --self-contained -r win-x64 -o publish

# 2. 패키지 생성
vpk pack -u IronworksTranslator -v 1.0.0 -p publish -o releases

# 3. 로컬 웹 서버로 테스트
# releases 폴더를 HTTP 서버로 호스팅하고
# UpdateManager의 URL을 로컬 서버로 변경
```

---

## 문제 해결

### 1. 업데이트 체크 실패

**증상**: `CheckForUpdatesAsync()`가 null을 반환하거나 예외 발생

**원인 및 해결방법**:
- **GitHub Releases에 파일이 없음**: `RELEASES` 파일과 `.nupkg` 파일이 릴리스에 업로드되었는지 확인
- **URL 오류**: GithubSource의 URL이 정확한지 확인
- **네트워크 문제**: 방화벽 또는 프록시 설정 확인
- **Private 리포지토리**: 인증 토큰 필요 (GithubSource의 두 번째 파라미터에 토큰 전달)

```csharp
// Private 리포지토리 예시
var mgr = new UpdateManager(
    new GithubSource("https://github.com/user/repo", "ghp_YourToken", false)
);
```

### 2. 업데이트 후 앱이 시작되지 않음

**증상**: 업데이트 적용 후 앱이 재시작되지 않거나 크래시

**원인 및 해결방법**:
- **VelopackApp.Build().Run() 누락**: Main 메서드에 Velopack 초기화 코드가 있는지 확인
- **파일 경로 문제**: 업데이트 후 필수 파일(data 폴더, WatchDogMain.exe 등)이 제대로 복사되었는지 확인
- **.csproj 설정 확인**: `ExcludeFromSingleFile`과 `CopyToOutputDirectory` 설정이 올바른지 확인

```xml
<Content Include="WatchDogMain.exe">
  <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
  <CopyToOutputDirectory>Always</CopyToOutputDirectory>
</Content>
```

### 3. 델타 패키지가 생성되지 않음

**증상**: 업데이트 시 항상 전체 패키지를 다운로드

**원인 및 해결방법**:
- **이전 릴리스 다운로드 누락**: `vpk download github` 명령을 실행하여 이전 버전 다운로드
- **releases 폴더 정리**: 이전 버전의 파일이 releases 폴더에 있어야 델타 생성 가능
- **버전 번호 확인**: 새 버전이 이전 버전보다 높은지 확인 (Semantic Versioning)

### 4. GitHub Actions 빌드 실패

**증상**: 워크플로우 실행 시 에러 발생

**일반적인 원인**:

**A. dotnet 명령 실패**:
- Dependencies 확인: 모든 NuGet 패키지가 복원되는지 확인
- .NET SDK 버전: `dotnet-version: '8.0.x'`가 프로젝트와 일치하는지 확인

**B. vpk 명령 실패**:
- 경로 문제: publish 폴더 경로가 올바른지 확인
- 권한 문제: `secrets.GITHUB_TOKEN` 권한이 충분한지 확인

**C. 업로드 실패**:
- 토큰 권한: Repository의 Settings → Actions → General에서 Workflow permissions를 "Read and write permissions"로 설정
- 릴리스 충돌: 같은 태그의 릴리스가 이미 존재하면 실패 (기존 릴리스 삭제 후 재시도)

### 5. 버전 번호 불일치

**증상**: 앱에 표시되는 버전과 업데이트 버전이 다름

**해결방법**:
- `.csproj` 파일에 `<Version>` 프로퍼티 추가
- GitHub Actions에서 태그 버전을 빌드 시 전달: `/p:Version=${{ steps.get-version.outputs.version }}`
- `AssemblyInfo`에서 버전 정보 확인

### 6. WatchDog 프로세스 충돌

**증상**: 업데이트 후 WatchDog 프로세스가 제대로 동작하지 않음

**원인**: Velopack 업데이트와 WatchDog 재시작 로직 충돌

**해결방법**:
- WatchDog을 Velopack 패키지에 포함: `CopyToOutputDirectory` 설정 확인
- 업데이트 후 WatchDog을 수동으로 재시작하는 로직 추가:

```csharp
VelopackApp.Build()
    .WithRestarted((v) =>
    {
        Log.Information($"Restarted after update to version {v}");
        // WatchDog 재시작
        BootWatchDog();
    })
    .Run();
```

---

## 추가 리소스

### 공식 문서
- [Velopack 공식 사이트](https://velopack.io/)
- [Velopack 문서](https://docs.velopack.io/)
- [Velopack GitHub](https://github.com/velopack/velopack)
- [C# Getting Started Guide](https://docs.velopack.io/getting-started/csharp)

### 예제 프로젝트
- [WpfVelopack 예제](https://github.com/lmaslovs/WpfVelopack)

### NuGet 패키지
- [Velopack on NuGet](https://www.nuget.org/packages/velopack)

### 커뮤니티
- [Velopack Discord](https://discord.gg/CjrCrNzd3F)
- [GitHub Issues](https://github.com/velopack/velopack/issues)

---

## 로드맵

### Phase 1: 기본 구현 (현재 문서)
- [x] Velopack 조사 및 문서 작성
- [ ] NuGet 패키지 설치
- [ ] App.xaml.cs 수정
- [ ] 업데이트 체크 로직 구현
- [ ] GitHub Actions 워크플로우 생성

### Phase 2: 고급 기능
- [ ] 설정 페이지에 수동 업데이트 버튼 추가
- [ ] 업데이트 진행률 UI 표시
- [ ] Pre-release / Stable 채널 선택 기능
- [ ] 업데이트 다운로드 취소 기능

### Phase 3: 사용자 경험 개선
- [ ] 업데이트 알림 토스트 메시지
- [ ] 릴리스 노트 표시
- [ ] 자동 업데이트 On/Off 설정
- [ ] 업데이트 주기 설정 (시작 시 / 매일 / 매주)

### Phase 4: 안정화 및 최적화
- [ ] 델타 패키지 최적화
- [ ] 업데이트 실패 시 롤백 기능
- [ ] 텔레메트리 및 에러 리포팅
- [ ] 다국어 지원 (업데이트 메시지)

---

## 결론

Velopack은 IronworksTranslator에 자동 업데이트 기능을 추가하는 데 이상적인 솔루션입니다. GitHub Releases와의 자연스러운 통합, 간단한 구현, 그리고 강력한 기능을 제공합니다.

이 가이드를 따라 구현하면 사용자는 항상 최신 버전을 사용할 수 있으며, 개발자는 배포 프로세스를 자동화하여 효율적인 릴리스 관리가 가능합니다.

구현 중 문제가 발생하면 "문제 해결" 섹션을 참조하거나, Velopack 커뮤니티에서 도움을 받을 수 있습니다.
