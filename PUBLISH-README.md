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

```powershell
.\publish-release.ps1
```

The script:

1. Cleans `bin` and `obj` unless `-SkipClean` is used.
2. Restores packages.
3. Builds Release with GitVersion.
4. Reads `src\IronworksTranslator\obj\gitversion.json`.
5. Publishes a framework-dependent `win-x64` app to `publish\IronworksTranslator ({VERSION})`.
6. Runs `vpk pack` and writes Velopack assets to `Releases`.

Velopack settings used by the script:

- `packId`: `Sappho192.IronworksTranslator`
- `packTitle`: `IronworksTranslator`
- `mainExe`: `IronworksTranslator.exe`
- `channel`: `win`
- `runtime`: `win-x64`
- `framework`: `net8.0-x64-desktop`

## Release Assets

Upload these Velopack files from `Releases` to the GitHub release:

- `Sappho192.IronworksTranslator-win-Setup.exe`
- `Sappho192.IronworksTranslator-{VERSION}-full.nupkg`
- `Sappho192.IronworksTranslator-{VERSION}-delta.nupkg` when generated
- `releases.win.json`

Optional generated files such as `Sappho192.IronworksTranslator-win-Portable.zip`, `assets.win.json`, and `RELEASES` are not required for the in-app updater.

The model files are not part of the default app update package. The app stores downloaded model/tokenizer data under `%LocalAppData%\IronworksTranslator\data`.

## Useful Options

```powershell
.\publish-release.ps1 -SkipClean
.\publish-release.ps1 -CreateZip
.\publish-release.ps1 -SkipVelopack
.\publish-release.ps1 -OutputDir "publish" -VelopackOutputDir "Releases"
```

`-CreateZip` is only for a legacy zip artifact. The installer and in-app updater use Velopack assets.

## Verification

After publishing, verify the executable version:

```powershell
(Get-Item "publish\IronworksTranslator (1.1.5)\IronworksTranslator.exe").VersionInfo.FileVersion
```

For local update testing, build two different versions into the same `Releases` folder, install the older `Setup.exe`, then launch it and confirm it updates to the newer release.

## Why Not Visual Studio Publish?

Visual Studio builds may not run GitVersion correctly for this WPF project. Use `publish-release.ps1` or equivalent `dotnet restore`, `dotnet build -c Release`, and `dotnet publish` commands for release builds.
