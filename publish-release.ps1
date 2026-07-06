# IronworksTranslator Release Publishing Script
# Builds the app with GitVersion and creates Velopack release assets.

param(
    [switch]$SkipClean,
    [switch]$CreateZip,
    [switch]$SkipVelopack,
    [ValidateSet("Stable", "Beta")]
    [string]$ReleaseChannel = "Stable",
    [string]$PrereleaseLabel = "",
    [string]$OutputDir = "publish",
    [string]$VelopackOutputDir = "Releases"
)

$ErrorActionPreference = "Stop"
$solutionPath = "src\IronworksTranslator\IronworksTranslator.sln"
$projectPath = "src\IronworksTranslator\IronworksTranslator.csproj"
$launcherProjectPath = "src\IronworksTranslator.Launcher\IronworksTranslator.Launcher.csproj"
$iconPath = "src\IronworksTranslator\icon.ico"
$velopackVersion = "1.2.0"
$packId = "Sappho192.IronworksTranslator"
$packTitle = "IronworksTranslator"
$appExe = "IronworksTranslator.exe"
$launcherExe = "IronworksTranslator.Launcher.exe"
$runtime = "win-x64"
$framework = "net10.0-x64-desktop"
$channel = "win"
$packVersion = $null

function Assert-Success($message) {
    if ($LASTEXITCODE -ne 0) {
        Write-Error $message
        exit 1
    }
}

if ($ReleaseChannel -eq "Beta") {
    if ([string]::IsNullOrWhiteSpace($PrereleaseLabel)) {
        Write-Error "Beta releases require -PrereleaseLabel, for example: -PrereleaseLabel beta.1"
        exit 1
    }

    if ($PrereleaseLabel -notmatch '^beta\.[1-9][0-9]*$') {
        Write-Error "Beta prerelease label must use the format beta.N, for example: beta.1"
        exit 1
    }

    $channel = "beta"
    if ((Split-Path -Leaf $VelopackOutputDir) -ne "beta") {
        $VelopackOutputDir = Join-Path $VelopackOutputDir "beta"
    }
} elseif (-not [string]::IsNullOrWhiteSpace($PrereleaseLabel)) {
    Write-Error "-PrereleaseLabel can only be used with -ReleaseChannel Beta"
    exit 1
}

$releaseChannelBuildProperty = "/p:IronworksReleaseChannel=$ReleaseChannel"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "IronworksTranslator Release Publisher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Release channel: $ReleaseChannel ($channel)" -ForegroundColor Cyan
if ($ReleaseChannel -eq "Beta") {
    Write-Host "Prerelease label: $PrereleaseLabel" -ForegroundColor Cyan
}
Write-Host ""

if (-not $SkipClean) {
    Write-Host "[1/6] Cleaning previous build artifacts..." -ForegroundColor Yellow

    if (Test-Path "src\IronworksTranslator\bin") {
        Remove-Item "src\IronworksTranslator\bin" -Recurse -Force
        Write-Host "  - Removed bin folder" -ForegroundColor Gray
    }

    if (Test-Path "src\IronworksTranslator\obj") {
        Remove-Item "src\IronworksTranslator\obj" -Recurse -Force
        Write-Host "  - Removed obj folder" -ForegroundColor Gray
    }

    if (Test-Path "src\IronworksTranslator.Launcher\bin") {
        Remove-Item "src\IronworksTranslator.Launcher\bin" -Recurse -Force
        Write-Host "  - Removed launcher bin folder" -ForegroundColor Gray
    }

    if (Test-Path "src\IronworksTranslator.Launcher\obj") {
        Remove-Item "src\IronworksTranslator.Launcher\obj" -Recurse -Force
        Write-Host "  - Removed launcher obj folder" -ForegroundColor Gray
    }

    Write-Host "  Clean completed." -ForegroundColor Green
} else {
    Write-Host "[1/6] Skipping clean (--SkipClean specified)" -ForegroundColor Gray
}
Write-Host ""

Write-Host "[2/6] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $solutionPath
Assert-Success "Package restore failed!"
Write-Host "  Restore completed." -ForegroundColor Green
Write-Host ""

Write-Host "[3/6] Building in Release configuration..." -ForegroundColor Yellow
dotnet build $projectPath -c Release --no-restore $releaseChannelBuildProperty
Assert-Success "Build failed!"
dotnet build $launcherProjectPath -c Release --no-restore
Assert-Success "Launcher build failed!"
Write-Host "  Build completed." -ForegroundColor Green
Write-Host ""

Write-Host "[4/6] Checking version information..." -ForegroundColor Yellow
$gitversionPath = "src\IronworksTranslator\obj\gitversion.json"
if (Test-Path $gitversionPath) {
    $gitversion = Get-Content $gitversionPath | ConvertFrom-Json
    $version = $gitversion.MajorMinorPatch
    $fullVersion = $gitversion.InformationalVersion
    $assemblyVersion = $gitversion.AssemblySemVer
    $packVersion = $version
    if ($ReleaseChannel -eq "Beta") {
        $packVersion = "$version-$PrereleaseLabel"
    }

    Write-Host "  Version: $version" -ForegroundColor Green
    Write-Host "  Package Version: $packVersion" -ForegroundColor Green
    Write-Host "  Assembly Version: $assemblyVersion" -ForegroundColor Green
    Write-Host "  Full Version: $fullVersion" -ForegroundColor Green
} else {
    Write-Error "gitversion.json not found. Cannot create a versioned Velopack release."
    exit 1
}
Write-Host ""

Write-Host "[5/6] Publishing application and launcher (single-file, framework-dependent)..." -ForegroundColor Yellow
$publishPath = Join-Path $OutputDir "IronworksTranslator ($packVersion)"
dotnet publish $projectPath -c Release -o $publishPath `
    $releaseChannelBuildProperty `
    /p:PublishSingleFile=true `
    /p:RuntimeIdentifier=$runtime `
    /p:SelfContained=false
Assert-Success "Publish failed!"
dotnet publish $launcherProjectPath -c Release -o $publishPath `
    /p:PublishSingleFile=true `
    /p:RuntimeIdentifier=$runtime `
    /p:SelfContained=false
Assert-Success "Launcher publish failed!"
Write-Host "  Published to: $publishPath" -ForegroundColor Green
Write-Host ""

$appExePath = Join-Path $publishPath $appExe
$launcherExePath = Join-Path $publishPath $launcherExe
if ((Test-Path $appExePath) -and (Test-Path $launcherExePath)) {
    $fileVersion = (Get-Item $appExePath).VersionInfo.FileVersion
    $productVersion = (Get-Item $appExePath).VersionInfo.ProductVersion
    $fileSize = [math]::Round((Get-Item $appExePath).Length / 1MB, 2)
    $launcherFileVersion = (Get-Item $launcherExePath).VersionInfo.FileVersion
    $launcherFileSize = [math]::Round((Get-Item $launcherExePath).Length / 1MB, 2)

    Write-Host "Verification:" -ForegroundColor Cyan
    Write-Host "  App EXE File Version: $fileVersion" -ForegroundColor Green
    Write-Host "  App EXE Product Version: $productVersion" -ForegroundColor Green
    Write-Host "  App EXE Size: $fileSize MB" -ForegroundColor Green
    Write-Host "  Launcher EXE File Version: $launcherFileVersion" -ForegroundColor Green
    Write-Host "  Launcher EXE Size: $launcherFileSize MB" -ForegroundColor Green

    if ($fileVersion -like "$version.*") {
        Write-Host "  App version is correct." -ForegroundColor Green
    } else {
        Write-Warning "App version mismatch detected. Expected $version.*, got $fileVersion"
    }

    if ($launcherFileVersion -like "$version.*") {
        Write-Host "  Launcher version is correct." -ForegroundColor Green
    } else {
        Write-Warning "Launcher version mismatch detected. Expected $version.*, got $launcherFileVersion"
    }
} else {
    Write-Error "Could not find published EXEs for verification: $appExePath, $launcherExePath"
    exit 1
}
Write-Host ""

if (-not $SkipVelopack) {
    Write-Host "[6/6] Creating Velopack release assets..." -ForegroundColor Yellow
    $vpkCommand = Get-Command "vpk" -ErrorAction SilentlyContinue
    if ($null -eq $vpkCommand) {
        Write-Error "Velopack CLI 'vpk' was not found. Install it with: dotnet tool install --global vpk --version $velopackVersion"
        exit 1
    }

    vpk pack `
        --packId $packId `
        --packVersion $packVersion `
        --packDir $publishPath `
        --mainExe $launcherExe `
        --packTitle $packTitle `
        --outputDir $VelopackOutputDir `
        --channel $channel `
        --runtime $runtime `
        --framework $framework `
        --icon $iconPath
    Assert-Success "Velopack packaging failed!"
    Write-Host "  Velopack output: $VelopackOutputDir" -ForegroundColor Green
} else {
    Write-Host "[6/6] Skipping Velopack packaging (--SkipVelopack specified)" -ForegroundColor Gray
}
Write-Host ""

if ($CreateZip) {
    Write-Host "Creating legacy distribution ZIP..." -ForegroundColor Yellow
    $zipPath = Join-Path $OutputDir "IronworksTranslator-v$packVersion.zip"

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath
    Write-Host "  Created: $zipPath" -ForegroundColor Green
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Publishing completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Publish output: $publishPath" -ForegroundColor White
if (-not $SkipVelopack) {
    Write-Host "Velopack assets: $VelopackOutputDir" -ForegroundColor White
    Write-Host "Velopack channel: $channel" -ForegroundColor White
}
if ($CreateZip) {
    Write-Host "Legacy ZIP file: $zipPath" -ForegroundColor White
}
Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Gray
Write-Host "  .\publish-release.ps1" -ForegroundColor Gray
Write-Host "  .\publish-release.ps1 -ReleaseChannel Beta -PrereleaseLabel beta.1" -ForegroundColor Gray
Write-Host "  .\publish-release.ps1 -SkipClean" -ForegroundColor Gray
Write-Host "  .\publish-release.ps1 -CreateZip" -ForegroundColor Gray
Write-Host "  .\publish-release.ps1 -SkipVelopack" -ForegroundColor Gray
