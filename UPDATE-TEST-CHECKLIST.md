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
- The app starts without a crash.
- The terms dialog appears on a clean profile.
- Accepting the terms writes:

```powershell
$env:APPDATA\IronworksTranslator\settings.yaml
```

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

Run `IronworksTranslator.exe` from the extracted folder.

Expected result:

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
- `data\model` is copied to `%LOCALAPPDATA%\IronworksTranslator\data\model` if the destination was empty.
- Existing destination files are not overwritten.

Installer note:

- The installer cannot automatically discover an arbitrary old zip folder. If a user installs from `Setup.exe`, they should manually copy old `settings.yaml` or model files into the new user-data locations if automatic migration did not find them.

## 6. Update Check In Development Build

Run the app directly from `bin`, `publish`, or Visual Studio.

Expected result:

- The app starts normally.
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
- Settings under `%APPDATA%\IronworksTranslator` are preserved.
- Logs and model files under `%LOCALAPPDATA%\IronworksTranslator` are preserved.

Stable isolation check:

- Publish a newer Beta GitHub prerelease that contains only `beta` channel assets.
- Launch an older Stable installed app.
- Confirm the Stable app does not offer the Beta update.

Beta channel check:

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
- Settings, logs, and model files are preserved.
- Returning from Beta to Stable is manual: reinstall the latest Stable installer. Automatic Stable downgrade is not supported in this first Beta channel.

## 8. Post-Update Regression Checks

After an update:

- Open the app again and confirm no repeated update prompt appears for the same version.
- Toggle translator on/off.
- Show/hide chat and dialogue windows.
- Reset chat/dialogue window positions.
- Open the log folder from the dashboard and confirm Windows Explorer opens `%LOCALAPPDATA%\IronworksTranslator\logs`.
- Clear logs from the dashboard and confirm only `.iwlog` and legacy `.txt` files in `%LOCALAPPDATA%\IronworksTranslator\logs` are affected.
- Select the MiLLMT translator and confirm model download/reuse works.

## 9. Release Blockers

Do not publish the release if any of these fail:

- `vpk pack` does not verify `VelopackApp.Run()`.
- `releases.win.json` is missing from GitHub release assets.
- Installed app cannot update from GitHub.
- Settings or model files are deleted during update.
- Update failure shows a crash instead of a recoverable error.
- The app writes user settings or logs into the install/current app folder.
