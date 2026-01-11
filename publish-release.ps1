# IronworksTranslator Release Publishing Script
# This script builds and publishes the application with proper GitVersion versioning

param(
    [switch]$SkipClean,
    [switch]$CreateZip,
    [string]$OutputDir = "publish"
)

$ErrorActionPreference = "Stop"
$projectPath = "src\IronworksTranslator\IronworksTranslator.csproj"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "IronworksTranslator Release Publisher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean build artifacts
if (-not $SkipClean) {
    Write-Host "[1/5] Cleaning previous build artifacts..." -ForegroundColor Yellow

    if (Test-Path "src\IronworksTranslator\bin") {
        Remove-Item "src\IronworksTranslator\bin" -Recurse -Force
        Write-Host "  - Removed bin folder" -ForegroundColor Gray
    }

    if (Test-Path "src\IronworksTranslator\obj") {
        Remove-Item "src\IronworksTranslator\obj" -Recurse -Force
        Write-Host "  - Removed obj folder" -ForegroundColor Gray
    }

    Write-Host "  Clean completed." -ForegroundColor Green
} else {
    Write-Host "[1/5] Skipping clean (--SkipClean specified)" -ForegroundColor Gray
}
Write-Host ""

# Restore packages
Write-Host "[2/5] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Package restore failed!"
    exit 1
}
Write-Host "  Restore completed." -ForegroundColor Green
Write-Host ""

# Build in Release configuration
Write-Host "[3/5] Building in Release configuration..." -ForegroundColor Yellow
dotnet build $projectPath -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}
Write-Host "  Build completed." -ForegroundColor Green
Write-Host ""

# Get version from gitversion.json
Write-Host "[4/5] Checking version information..." -ForegroundColor Yellow
$gitversionPath = "src\IronworksTranslator\obj\gitversion.json"
if (Test-Path $gitversionPath) {
    $gitversion = Get-Content $gitversionPath | ConvertFrom-Json
    $version = $gitversion.MajorMinorPatch
    $fullVersion = $gitversion.InformationalVersion
    $assemblyVersion = $gitversion.AssemblySemVer

    Write-Host "  Version: $version" -ForegroundColor Green
    Write-Host "  Assembly Version: $assemblyVersion" -ForegroundColor Green
    Write-Host "  Full Version: $fullVersion" -ForegroundColor Green
} else {
    Write-Warning "  gitversion.json not found. Version information unavailable."
    $version = "unknown"
}
Write-Host ""

# Publish
Write-Host "[5/5] Publishing application (single-file)..." -ForegroundColor Yellow
$publishPath = Join-Path $OutputDir "IronworksTranslator ($version)"
dotnet publish $projectPath -c Release -o $publishPath `
    /p:PublishSingleFile=true `
    /p:RuntimeIdentifier=win-x64 `
    /p:SelfContained=false
if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed!"
    exit 1
}
Write-Host "  Published to: $publishPath" -ForegroundColor Green
Write-Host ""

# Verify published exe version
$exePath = Join-Path $publishPath "IronworksTranslator.exe"
if (Test-Path $exePath) {
    $fileVersion = (Get-Item $exePath).VersionInfo.FileVersion
    $productVersion = (Get-Item $exePath).VersionInfo.ProductVersion

    Write-Host "Verification:" -ForegroundColor Cyan
    Write-Host "  EXE File Version: $fileVersion" -ForegroundColor Green
    Write-Host "  EXE Product Version: $productVersion" -ForegroundColor Green
    $fileSize = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
    Write-Host "  EXE Size: $fileSize MB (single-file, framework-dependent)" -ForegroundColor Green

    # Check if version matches
    if ($fileVersion -like "$version.*") {
        Write-Host "  ✓ Version is correct!" -ForegroundColor Green
    } else {
        Write-Warning "  ⚠ Version mismatch detected!"
        Write-Warning "    Expected: $version.*"
        Write-Warning "    Got: $fileVersion"
    }
} else {
    Write-Warning "Could not find published EXE for verification."
}
Write-Host ""

# Create zip if requested
if ($CreateZip) {
    Write-Host "Creating distribution ZIP..." -ForegroundColor Yellow
    $zipPath = Join-Path $OutputDir "IronworksTranslator-v$version.zip"

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath
    Write-Host "  Created: $zipPath" -ForegroundColor Green
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ Publishing completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output location: $publishPath" -ForegroundColor White
if ($CreateZip) {
    Write-Host "ZIP file: $zipPath" -ForegroundColor White
}
Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Gray
Write-Host "  .\publish-release.ps1                 # Standard publish" -ForegroundColor Gray
Write-Host "  .\publish-release.ps1 -CreateZip      # Publish and create ZIP" -ForegroundColor Gray
Write-Host "  .\publish-release.ps1 -SkipClean      # Skip cleaning step" -ForegroundColor Gray
