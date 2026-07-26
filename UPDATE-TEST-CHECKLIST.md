# Auto Update Test Checklist

This checklist is for manually verifying the Velopack-based update flow before publishing a public IronworksTranslator release.

## 1. Preflight

Use a Windows 10/11 test machine or VM if possible.

Install or update the Velopack CLI:

```powershell
dotnet tool update --global vpk --version 1.2.0
```

Back up existing user data if this is your main PC:

```powershell
Copy-Item "$env:APPDATA\IronworksTranslator" "$env:APPDATA\IronworksTranslator.backup" -Recurse -ErrorAction SilentlyContinue
Copy-Item "$env:LOCALAPPDATA\IronworksTranslator" "$env:LOCALAPPDATA\IronworksTranslator.backup" -Recurse -ErrorAction SilentlyContinue
```

For a clean first-run test, remove test data after backing it up:

```powershell
Remove-Item "$env:APPDATA\IronworksTranslator" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$env:LOCALAPPDATA\IronworksTranslator" -Recurse -Force -ErrorAction SilentlyContinue
```

## 2. Build And Package

Run the Stable package:

```powershell
.\publish-release.ps1 -SkipClean
```

Expected result:

- Script completes without errors.
- `publish\IronworksTranslator ({VERSION})\IronworksTranslator.exe` exists.
- `publish\IronworksTranslator ({VERSION})\IronworksTranslator.Launcher.exe` exists.
- `publish\IronworksTranslator ({VERSION})\IronworksMiLMMTNativeProbe.exe` exists.
- The publish directory contains `LICENSES\llama.cpp-MIT.txt` and separate `milmmt-cuda` and `milmmt-vulkan` runtime-pack directories.
- `Releases\releases.win.json` exists.
- `Releases\Sappho192.IronworksTranslator-{VERSION}-full.nupkg` exists.
- `Releases\Sappho192.IronworksTranslator-win-Setup.exe` exists.
- `Releases\Sappho192.IronworksTranslator-win-Portable.zip` exists.

Run the Beta package:

```powershell
.\publish-release.ps1 -ReleaseChannel Beta -PrereleaseLabel beta.1 -SkipClean
```

Expected result:

- Script completes without errors.
- `publish\IronworksTranslator ({VERSION}-beta.1)\IronworksTranslator.exe` exists.
- `publish\IronworksTranslator ({VERSION}-beta.1)\IronworksTranslator.Launcher.exe` exists.
- `publish\IronworksTranslator ({VERSION}-beta.1)\IronworksMiLMMTNativeProbe.exe` exists.
- `Releases\beta\releases.beta.json` exists.
- `Releases\beta\Sappho192.IronworksTranslator-{VERSION}-beta.1-full.nupkg` exists.
- `Releases\beta\Sappho192.IronworksTranslator-beta-Setup.exe` exists.

## 3. Installer First-Run Smoke Test

Run:

```powershell
.\Releases\Sappho192.IronworksTranslator-win-Setup.exe
```

Expected result:

- Installer completes and launches IronworksTranslator.
- The launcher shows a UAC prompt for the real app.
- Accepting the UAC prompt starts `IronworksTranslator.exe` as administrator.
- Cancelling the UAC prompt cancels only app startup; the installer should not report a failed install.
- The app starts without a crash.
- The terms dialog appears on a clean profile.
- Accepting the terms writes:

```powershell
$env:APPDATA\IronworksTranslator\settings.v2.yaml
```

- Fresh installs do not need to create the unversioned legacy `settings.yaml`.
- An encrypted `.iwlog` log file is created under:

```powershell
$env:LOCALAPPDATA\IronworksTranslator\logs
```

- No new `settings.yaml` or `logs` folder is created next to the installed executable.

## 4. Portable First-Run Smoke Test

Extract:

```powershell
.\Releases\Sappho192.IronworksTranslator-win-Portable.zip
```

Run `IronworksTranslator.Launcher.exe` from the extracted folder.

Expected result:

- The launcher shows a UAC prompt for the real app.
- The portable app starts without a crash.
- If the user data folders are clean, the terms dialog appears.
- Settings and logs are still written to `%APPDATA%` and `%LOCALAPPDATA%`, not inside the portable folder.
- Update checks do not show an error dialog when no update is available.

## 5. Legacy Zip Data Migration

This test verifies the v1 migration rule: only files discoverable in the current execution folder are copied automatically.

For portable migration:

1. Remove existing test user data:

   ```powershell
   Remove-Item "$env:APPDATA\IronworksTranslator" -Recurse -Force -ErrorAction SilentlyContinue
   Remove-Item "$env:LOCALAPPDATA\IronworksTranslator" -Recurse -Force -ErrorAction SilentlyContinue
   ```

2. Place a legacy `settings.yaml` next to the portable `IronworksTranslator.exe`.
3. Optionally place legacy model files under `data\model` in the same portable folder.
4. Launch the portable app.

Expected result:

- `settings.yaml` is copied to `%APPDATA%\IronworksTranslator\settings.yaml`.
- The app imports that legacy file into `%APPDATA%\IronworksTranslator\settings.v2.yaml`.
- The imported legacy `settings.yaml` remains available as a downgrade snapshot for older builds.
- `data\model` is copied to `%LOCALAPPDATA%\IronworksTranslator\data\model` if the destination was empty.
- Existing destination files are not overwritten.

Installer note:

- The installer cannot automatically discover an arbitrary old zip folder. If a user installs from `Setup.exe`, they should manually copy old `settings.yaml` or model files into the new user-data locations if automatic migration did not find them.

## 6. Update Check In Development Build

Run the app directly from `bin`, `publish`, or Visual Studio.

Expected result:

- Running `IronworksTranslator.exe` directly starts normally when administrator permission is granted.
- Running `IronworksTranslator.Launcher.exe` starts the real app via UAC.
- Update checks are skipped silently when the app is not a real Velopack install.
- No user-facing update error is shown for `NotInstalledException`.
- Logs mention that the process is not a Velopack install.

## 7. Real Update Test With GitHub Releases

Stable builds use GitHub non-prerelease releases and the Velopack `win` channel. Beta builds use GitHub prereleases and the Velopack `beta` channel. The real in-app update flow must be tested against GitHub release assets.

1. Publish an older Stable Velopack release to a normal GitHub release with these files:
   - `Sappho192.IronworksTranslator-win-Setup.exe`
   - `Sappho192.IronworksTranslator-{OLD_VERSION}-full.nupkg`
   - `releases.win.json`
2. Install the older Stable version using `Sappho192.IronworksTranslator-win-Setup.exe`.
3. Publish a newer normal GitHub release with:
   - `Sappho192.IronworksTranslator-{NEW_VERSION}-full.nupkg`
   - `Sappho192.IronworksTranslator-{NEW_VERSION}-delta.nupkg` if generated
   - updated `releases.win.json`
4. Launch the older Stable installed app.

Expected result:

- The update dialog appears after startup prompts.
- The dialog shows current and new versions.
- Release notes render as readable markdown.
- Choosing `Download and restart` downloads the update.
- The app exits, applies the update, restarts, and reports the new version.
- The restart target is `IronworksTranslator.Launcher.exe`, which prompts for UAC and starts the real app.
- Settings under `%APPDATA%\IronworksTranslator` are preserved.
- Logs and model files under `%LOCALAPPDATA%\IronworksTranslator` are preserved.

Stable isolation check:

- Publish a newer Beta GitHub prerelease that contains only `beta` channel assets.
- Launch an older Stable installed app.
- Confirm the Stable app does not offer the Beta update.

Beta channel check:

The `1.2.0-beta.*` packages were reset before public use. Start launcher-based Beta testing at `1.2.1-beta.1`. Do not treat automatic updates from `1.2.0-beta.*` as a release requirement.

1. Publish an older Beta GitHub prerelease with:
   - `Sappho192.IronworksTranslator-beta-Setup.exe`
   - `Sappho192.IronworksTranslator-{OLD_VERSION}-beta.1-full.nupkg`
   - `releases.beta.json`
2. Install the older Beta version using `Sappho192.IronworksTranslator-beta-Setup.exe`.
3. Publish a newer Beta GitHub prerelease with:
   - `Sappho192.IronworksTranslator-{NEW_VERSION}-beta.2-full.nupkg`
   - `Sappho192.IronworksTranslator-{NEW_VERSION}-beta.2-delta.nupkg` if generated
   - updated `releases.beta.json`
4. Launch the older Beta installed app.

Expected result:

- The Beta app offers the newer Beta update.
- The app exits, applies the update, restarts, and reports the new Beta version.
- The restarted launcher prompts for UAC and starts the real app as administrator.
- Settings, logs, and model files are preserved.
- Returning from Beta to Stable is manual: reinstall the latest Stable installer. Automatic Stable downgrade is not supported in this first Beta channel.

Settings schema isolation check:

1. Start with an older package and a populated unversioned `settings.yaml`.
2. Launch the newer package and confirm it imports the settings into `settings.v2.yaml`.
3. Confirm the legacy `settings.yaml` remains readable by the older package.
4. Change settings in the newer package and confirm only `settings.v2.yaml` changes.
5. Reinstall and launch the older package. Confirm it reaches the main window and update prompt using its preserved `settings.yaml`.
6. Change settings in the older package and confirm `settings.v2.yaml` is unchanged.
7. For every future incompatible settings change, increment `AppPaths.SettingsSchemaVersion`; never repurpose or overwrite an existing versioned settings file.

## 8. Post-Update Regression Checks

After an update:

- Open the app again and confirm no repeated update prompt appears for the same version.
- Toggle translator on/off.
- Show/hide chat and dialogue windows.
- Reset chat/dialogue window positions.
- Open the log folder from the dashboard and confirm Windows Explorer opens `%LOCALAPPDATA%\IronworksTranslator\logs`.
- Clear logs from the dashboard and confirm only `.iwlog` and legacy `.txt` files in `%LOCALAPPDATA%\IronworksTranslator\logs` are affected.
- Select the MiLMMT translator and confirm model download/reuse works.

## 9. Hermes v2 and Sharlayan Checks

Sharlayan upstream smoke는 IronworksTranslator의 release readiness를 대신하지 않는다.
릴리스할 정확한 IronworksTranslator package에서 다음 항목을 별도로 확인한다.

Package and resource checks:

- [ ] Restore 결과와 publish 산출물이 `Sharlayan.Lite 9.1.4`를 사용한다.
- [ ] Publish 및 Velopack package에 `FFXIVClientStructs`, `HermesAddress`,
  `latest/address.json`, local `address.json` 또는 `UseInternalAddress` consumer가 없다.
- [ ] 새 Hermes cache에서 앱을 시작하면 `RemotePreferred`가 remote
  `live-verified` revision을 선택하고 source와 revision을 진단 로그에 남긴다.
- [ ] 기존 verified cache를 보존한 상태로 remote를 사용할 수 없게 만들면 cache fallback으로
  앱이 시작되고 CHATLOG와 Talk가 계속 동작한다.
- [ ] 별도의 깨끗한 cache에서 remote를 사용할 수 없게 만들면 embedded fallback으로 앱이
  시작되고 CHATLOG와 Talk가 계속 동작한다.
- [ ] fallback 검증 전 사용자 cache를 백업하고 검증 후 원상 복구한다. 사용자 cache나 legacy
  파일을 검증 과정에서 삭제하지 않는다.
- [ ] 실행 중 resource revision을 수동 갱신하는 기능은 제공하지 않는다. 새 revision 적용에는
  앱 재시작이 필요함을 확인한다.

Packaged live checks:

- [ ] 실제 게임에서 CHATLOG와 표준 Talk를 동시에 수집하고 번역한다.
- [x] DialogueWindow가 이름과 대사를 `Speaker: Text` 형식으로 표시한다.
- [ ] attach 시 기존 LastTalk를 신규 대사로 처리하지 않으며, 같은 name/text 쌍을 닫았다 다시
  열면 신규 Talk로 처리한다.
- [ ] 글로벌 client와 한국 client에서 Talk 읽기와 CHATLOG 회귀 여부를 각각 확인한다.
- [ ] FFXIV process 종료 시 poller와 handler가 정리되고, 게임 재실행 후 재연결되어 다시
  CHATLOG와 Talk를 처리한다.
- [ ] 앱 정상 종료 후 background callback, 종료 예외 또는 남은 IronworksTranslator process가
  없다.
- [ ] production signing을 적용한 installer에서 최초 설치, 실행, 업데이트 및 재시작을 확인한다.

## 10. Release Blockers

Do not publish the release if any of these fail:

- `vpk pack` does not verify `VelopackApp.Run()`.
- Velopack shortcuts or update restarts do not target `IronworksTranslator.Launcher.exe`.
- Installer first-run fails when the user cancels the UAC prompt.
- `releases.win.json` is missing from GitHub release assets.
- Installed app cannot update from GitHub.
- Settings or model files are deleted during update.
- Update failure shows a crash instead of a recoverable error.
- Fatal startup failure does not show an error dialog and terminate without leaving background processes.
- The app writes user settings or logs into the install/current app folder.
- Any required Hermes v2 or Sharlayan check in section 9 is incomplete or fails.

Fatal startup check:

1. Back up the test profile and introduce a deterministic invalid settings value.
2. Launch the packaged app through `IronworksTranslator.Launcher.exe`.
3. Confirm a startup failure dialog appears before the application exits.
4. Confirm the dialog does not depend on successfully loaded settings or localization.
5. Dismiss the dialog and confirm `IronworksTranslator.exe` exits with no watchdog or child window process left behind.
6. Confirm the encrypted log contains the original startup exception and ends with complete readable frames.
