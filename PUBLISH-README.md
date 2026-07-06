# Publishing IronworksTranslator

IronworksTranslator releases are built with the .NET CLI so GitVersion can stamp the executable correctly, then packaged with Velopack for installable releases and in-app updates.

## Prerequisites

Install the Velopack CLI version used by the app:

```powershell
dotnet tool install --global vpk --version 1.2.0
```

If it is already installed, keep it aligned with the `Velopack` NuGet package:

```powershell
dotnet tool update --global vpk --version 1.2.0
```

## Build A Release

Stable releases are the default:

```powershell
.\publish-release.ps1
```

Beta releases must use a SemVer prerelease label:

```powershell
.\publish-release.ps1 -ReleaseChannel Beta -PrereleaseLabel beta.1
```

The script:

1. Cleans `bin` and `obj` unless `-SkipClean` is used.
2. Restores packages.
3. Builds Release with GitVersion and the selected release channel.
4. Reads `src\IronworksTranslator\obj\gitversion.json`.
5. Publishes the framework-dependent `win-x64` app and launcher to `publish\IronworksTranslator ({PACKAGE_VERSION})`.
6. Runs `vpk pack` and writes Stable Velopack assets to `Releases` or Beta assets to `Releases\beta`.

Velopack settings used by the script:

- `packId`: `Sappho192.IronworksTranslator`
- `packTitle`: `IronworksTranslator`
- `mainExe`: `IronworksTranslator.Launcher.exe`
- Stable `channel`: `win`
- Beta `channel`: `beta`
- `runtime`: `win-x64`
- `framework`: `net10.0-x64-desktop`

## Launcher And Administrator Rights

Velopack launches `IronworksTranslator.Launcher.exe`, which runs as the invoking user and handles Velopack startup hooks. On normal startup, the launcher starts `IronworksTranslator.exe` with the Windows `runas` verb, so the real WPF translator app still runs with administrator rights.

This keeps per-user Velopack install/update flows separate from the app's FFXIV memory access requirement. Installer first-run and update restart should show a UAC prompt from the launcher. If the user cancels the UAC prompt, installation should still be considered complete; only the app launch is cancelled.

## Release Assets

Upload these Stable Velopack files from `Releases` to a normal GitHub release:

- `Sappho192.IronworksTranslator-win-Setup.exe`
- `Sappho192.IronworksTranslator-{VERSION}-full.nupkg`
- `Sappho192.IronworksTranslator-{VERSION}-delta.nupkg` when generated
- `releases.win.json`

Upload these Beta Velopack files from `Releases\beta` to a GitHub prerelease:

- `Sappho192.IronworksTranslator-beta-Setup.exe`
- `Sappho192.IronworksTranslator-{VERSION}-beta.N-full.nupkg`
- `Sappho192.IronworksTranslator-{VERSION}-beta.N-delta.nupkg` when generated
- `releases.beta.json`

Optional generated files such as `Sappho192.IronworksTranslator-win-Portable.zip`, `assets.win.json`, and `RELEASES` are not required for the in-app updater.

The model files are not part of the default app update package. The app stores downloaded model/tokenizer data under `%LocalAppData%\IronworksTranslator\data`.

## Stable And Beta Rules

- Stable builds use GitHub non-prerelease releases and the Velopack `win` channel.
- Beta builds use GitHub prereleases and the Velopack `beta` channel.
- Do not upload `releases.beta.json` or Beta `.nupkg` files to a Stable GitHub release.
- Do not upload `releases.win.json` or Stable `.nupkg` files to a Beta GitHub prerelease.
- Beta versions must use labels such as `1.1.5-beta.1` and `1.1.5-beta.2`.
- When promoting a tested Beta to Stable, publish the Stable version without the prerelease label, such as `1.1.5`.
- The first Beta channel does not provide automatic downgrade back to Stable. Beta testers should reinstall the latest Stable installer to return to Stable.
- The `1.2.0-beta.*` packages were reset before public use. Start the launcher-based Beta line at `1.2.1-beta.1`; do not rely on automatic updates from `1.2.0-beta.*`.

## Useful Options

```powershell
.\publish-release.ps1 -SkipClean
.\publish-release.ps1 -CreateZip
.\publish-release.ps1 -SkipVelopack
.\publish-release.ps1 -ReleaseChannel Beta -PrereleaseLabel beta.1
.\publish-release.ps1 -OutputDir "publish" -VelopackOutputDir "Releases"
```

`-CreateZip` is only for a legacy zip artifact. The installer and in-app updater use Velopack assets.

## Verification

After publishing, verify the executable version:

```powershell
(Get-Item "publish\IronworksTranslator (1.2.1-beta.1)\IronworksTranslator.exe").VersionInfo.FileVersion
(Get-Item "publish\IronworksTranslator (1.2.1-beta.1)\IronworksTranslator.Launcher.exe").VersionInfo.FileVersion
```

For local update testing, build two different versions into the same `Releases` folder, install the older `Setup.exe`, then launch it and confirm it updates to the newer release.

For local Beta update testing, build two different Beta versions into `Releases\beta`, install the older `Sappho192.IronworksTranslator-beta-Setup.exe`, then launch it and confirm it updates to the newer Beta release.

## Why Not Visual Studio Publish?

Visual Studio builds may not run GitVersion correctly for this WPF project. Use `publish-release.ps1` or equivalent `dotnet restore`, `dotnet build -c Release`, and `dotnet publish` commands for release builds.
