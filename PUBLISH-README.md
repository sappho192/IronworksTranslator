# Publishing IronworksTranslator with Correct Version

## Problem Summary

Visual Studio builds don't properly integrate with GitVersion, causing the assembly version to show as `1.0.0.0` instead of the correct version (e.g., `1.1.3.0`).

**Root Cause**: Visual Studio's MSBuild sets `$(GetVersion)` to `false`, preventing GitVersion from running. This is a known limitation of GitVersion.MsBuild with Visual Studio for WPF projects.

## Solution

Use the provided PowerShell script `publish-release.ps1` which uses `dotnet CLI` to build and publish. The CLI build system works correctly with GitVersion.

## Usage

### Basic Publishing
```powershell
.\publish-release.ps1
```

This will:
1. Clean build artifacts (`bin`, `obj` folders)
2. Restore NuGet packages
3. Build in Release configuration
4. Read version from GitVersion
5. Publish to `publish\IronworksTranslator-v{VERSION}\`
6. Verify the EXE has correct version

### Create Distribution ZIP
```powershell
.\publish-release.ps1 -CreateZip
```

Creates a ZIP file: `publish\IronworksTranslator-v{VERSION}.zip`

### Skip Clean Step
```powershell
.\publish-release.ps1 -SkipClean
```

Useful for faster rebuilds when you know the build state is clean.

### Custom Output Directory
```powershell
.\publish-release.ps1 -OutputDir "releases"
```

Publishes to `releases\IronworksTranslator-v{VERSION}\` instead of the default `publish\` directory.

## Examples

```powershell
# Standard release build with ZIP
.\publish-release.ps1 -CreateZip

# Quick rebuild without cleaning
.\publish-release.ps1 -SkipClean -CreateZip

# Publish to custom directory
.\publish-release.ps1 -OutputDir "D:\Releases" -CreateZip
```

## Expected Output

When the script runs successfully, you should see:

```
========================================
IronworksTranslator Release Publisher
========================================

[1/5] Cleaning previous build artifacts...
  Clean completed.

[2/5] Restoring NuGet packages...
  Restore completed.

[3/5] Building in Release configuration...
  MrAdvice 2.19.1... weaved module 'IronworksTranslator, Version=1.1.3.0, ...'
  Build completed.

[4/5] Checking version information...
  Version: 1.1.3
  Assembly Version: 1.1.3.0
  Full Version: 1.1.3+Branch.master.Sha.8b48d699...

[5/5] Publishing application...
  Published to: publish\IronworksTranslator-v1.1.3

Verification:
  EXE File Version: 1.1.3.0
  EXE Product Version: 1.1.3+Branch.master.Sha...
  ✓ Version is correct!

========================================
✓ Publishing completed successfully!
========================================
```

## Verification

After publishing, verify the version is correct:

```powershell
# Check the published EXE
(Get-Item "publish\IronworksTranslator-v1.1.3\IronworksTranslator.exe").VersionInfo.FileVersion
```

Should output: `1.1.3.0`

## Alternative: Command Line Build

You can also build manually using dotnet CLI:

```powershell
# Clean
Remove-Item src\IronworksTranslator\bin -Recurse -Force
Remove-Item src\IronworksTranslator\obj -Recurse -Force

# Build in Release
dotnet build src\IronworksTranslator\IronworksTranslator.csproj -c Release

# Publish
dotnet publish src\IronworksTranslator\IronworksTranslator.csproj -c Release -o publish\Release
```

## Why Not Use Visual Studio?

Visual Studio's MSBuild has a fundamental incompatibility with GitVersion.MsBuild for WPF projects:

- Visual Studio uses Framework-based MSBuild from `C:\Program Files\Microsoft Visual Studio\2022\...\MSBuild\`
- .NET CLI uses SDK-based MSBuild from `C:\Program Files\dotnet\sdk\...\`
- GitVersion.MsBuild only works correctly with SDK-based MSBuild

The build log shows: `$(GetVersion)="false"(이전 값: "true")` - GitVersion is actively disabled in Visual Studio builds.

## For CI/CD

In your CI/CD pipeline, use the dotnet CLI commands:

```yaml
- dotnet restore
- dotnet build -c Release
- dotnet publish -c Release -o output/
```

GitVersion will work correctly in these environments.
