<#
.SYNOPSIS
Verifies Sharlayan.Lite official NuGet provenance against a dedicated package cache and project.assets.json.

.DESCRIPTION
Implements the Sharlayan.Lite provenance gate required before packaging or publishing an IronworksTranslator release:
- Resolves the expected Sharlayan.Lite version from IronworksTranslator.csproj (unless -ExpectedVersion is specified).
- Inspects enabled NuGet sources and reports any non-official sources.
- Verifies that <CacheDir>\sharlayan.lite\<version>\.nupkg.metadata records the official nuget.org v3 index URL.
- Verifies that metadata contentHash matches project.assets.json sha512 for Sharlayan.Lite/<version>.
- Computes SHA-256 and version metadata for the official cached Sharlayan.dll.
- Optionally verifies that built Release / win-x64 Sharlayan.dll outputs match the official cached DLL SHA-256.
#>
param(
    [string]$CacheDir = $env:NUGET_PACKAGES,
    [string]$ProjectPath = "src\IronworksTranslator\IronworksTranslator.csproj",
    [string]$AssetsPath = "src\IronworksTranslator\obj\project.assets.json",
    [string]$ExpectedVersion = "",
    [string]$ExpectedOfficialSource = "https://api.nuget.org/v3/index.json",
    [string]$TargetFramework = "net10.0",
    [string[]]$BuiltDllPaths = @(
        "src\IronworksTranslator\bin\Release\net10.0-windows\Sharlayan.dll",
        "src\IronworksTranslator\bin\Release\net10.0-windows\win-x64\Sharlayan.dll"
    ),
    [switch]$CheckBuiltDlls,
    [switch]$FailOnNonOfficialNuGetSource,
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $parent = Split-Path -Parent $fullPath
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($fullPath, $Content, $utf8NoBom)
}

function Resolve-SharlayanVersion([string]$CsprojPath, [string]$OverrideVersion) {
    if (-not [string]::IsNullOrWhiteSpace($OverrideVersion)) {
        return $OverrideVersion.Trim()
    }
    if (-not (Test-Path -LiteralPath $CsprojPath -PathType Leaf)) {
        throw "Project file not found: $CsprojPath"
    }
    [xml]$csproj = Get-Content -LiteralPath $CsprojPath -Raw -Encoding UTF8
    $pkgNode = $csproj.SelectSingleNode("//PackageReference[@Include='Sharlayan.Lite']")
    if ($null -eq $pkgNode -or [string]::IsNullOrWhiteSpace($pkgNode.Version)) {
        throw "Could not find PackageReference Include='Sharlayan.Lite' in $CsprojPath"
    }
    return ([string]$pkgNode.Version).Trim()
}

function Get-EnabledNuGetSources {
    $sources = @()
    $output = & dotnet nuget list source --format Short 2>&1
    if ($LASTEXITCODE -ne 0) {
        return [ordered]@{
            checked = $false
            error   = ($output -join "`n").Trim()
            enabled = @()
        }
    }
    foreach ($line in $output) {
        $text = ([string]$line).Trim()
        if ($text -match '^E\s+(.+)$') {
            $sources += $Matches[1].Trim()
        }
    }
    return [ordered]@{
        checked = $true
        error   = $null
        enabled = $sources
    }
}

if ([string]::IsNullOrWhiteSpace($CacheDir)) {
    throw "CacheDir is required (pass -CacheDir or set NUGET_PACKAGES to a dedicated release cache directory)."
}
if (-not (Test-Path -LiteralPath $CacheDir -PathType Container)) {
    throw "NuGet cache directory does not exist: $CacheDir"
}

$version = Resolve-SharlayanVersion -CsprojPath $ProjectPath -OverrideVersion $ExpectedVersion
$packageKey = "Sharlayan.Lite/$version"
$pkgDir = Join-Path $CacheDir ("sharlayan.lite\" + $version.ToLowerInvariant())
$metaPath = Join-Path $pkgDir ".nupkg.metadata"
$dllPath = Join-Path $pkgDir ("lib\" + $TargetFramework + "\Sharlayan.dll")

if (-not (Test-Path -LiteralPath $metaPath -PathType Leaf)) {
    throw "Sharlayan.Lite metadata file not found in cache: $metaPath"
}
if (-not (Test-Path -LiteralPath $dllPath -PathType Leaf)) {
    throw "Sharlayan.Lite DLL not found in cache: $dllPath"
}
if (-not (Test-Path -LiteralPath $AssetsPath -PathType Leaf)) {
    throw "project.assets.json not found: $AssetsPath"
}

$nugetSources = Get-EnabledNuGetSources
$nonOfficialSources = @($nugetSources.enabled | Where-Object { $_ -ne $ExpectedOfficialSource })

$meta = Get-Content -LiteralPath $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json
$assets = Get-Content -LiteralPath $AssetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$assetEntry = $assets.libraries.$packageKey
if ($null -eq $assetEntry) {
    throw "Library entry '$packageKey' not found in $AssetsPath"
}

$assetsSha512 = [string]$assetEntry.sha512
$dllSha256 = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
$dllInfo = (Get-Item -LiteralPath $dllPath).VersionInfo

$builtChecks = @()
$allBuiltDllsMatch = $true
if ($CheckBuiltDlls) {
    foreach ($builtPath in $BuiltDllPaths) {
        if (-not (Test-Path -LiteralPath $builtPath -PathType Leaf)) {
            $allBuiltDllsMatch = $false
            $builtChecks += [ordered]@{
                path           = $builtPath
                exists         = $false
                sha256         = $null
                productVersion = $null
                matchesCache   = $false
            }
            continue
        }
        $builtHash = (Get-FileHash -LiteralPath $builtPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $builtVi = (Get-Item -LiteralPath $builtPath).VersionInfo
        $matches = ($builtHash -eq $dllSha256) -and ($builtVi.ProductVersion -eq $dllInfo.ProductVersion)
        if (-not $matches) {
            $allBuiltDllsMatch = $false
        }
        $builtChecks += [ordered]@{
            path           = $builtPath
            exists         = $true
            sha256         = $builtHash
            fileVersion    = $builtVi.FileVersion
            productVersion = $builtVi.ProductVersion
            matchesCache   = $matches
        }
    }
}

$sourceMatchesOfficial = ([string]$meta.source -eq $ExpectedOfficialSource)
$contentHashMatchesAssets = ([string]$meta.contentHash -eq $assetsSha512)

$report = [ordered]@{
    package                  = $packageKey
    cacheDir                 = [System.IO.Path]::GetFullPath($CacheDir)
    assetsPath               = [System.IO.Path]::GetFullPath($AssetsPath)
    expectedOfficialSource   = $ExpectedOfficialSource
    nugetSources             = [ordered]@{
        checked             = $nugetSources.checked
        enabled             = $nugetSources.enabled
        nonOfficialEnabled  = $nonOfficialSources
        error               = $nugetSources.error
    }
    metadataSource           = $meta.source
    metadataContentHash      = $meta.contentHash
    assetsSha512             = $assetsSha512
    sourceMatchesOfficial    = $sourceMatchesOfficial
    contentHashMatchesAssets = $contentHashMatchesAssets
    cacheDllPath             = [System.IO.Path]::GetFullPath($dllPath)
    cacheDllSha256           = $dllSha256
    cacheDllFileVersion      = $dllInfo.FileVersion
    cacheDllProductVersion   = $dllInfo.ProductVersion
    builtDllsChecked         = [bool]$CheckBuiltDlls
    allBuiltDllsMatchCache   = if ($CheckBuiltDlls) { $allBuiltDllsMatch } else { $null }
    builtDlls                = $builtChecks
}

$json = ($report | ConvertTo-Json -Depth 8) + "`n"
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    Write-Utf8NoBom -Path $OutputPath -Content $json
}
Write-Output $json

if ($FailOnNonOfficialNuGetSource) {
    if (-not $nugetSources.checked) {
        throw "Could not inspect NuGet sources while -FailOnNonOfficialNuGetSource was requested: $($nugetSources.error)"
    }
    if ($nonOfficialSources.Count -gt 0) {
        throw "Non-official enabled NuGet source(s) detected: $($nonOfficialSources -join ', ')"
    }
}
if (-not $sourceMatchesOfficial) {
    throw "Sharlayan.Lite metadata source '$($meta.source)' does not match official source '$ExpectedOfficialSource'."
}
if (-not $contentHashMatchesAssets) {
    throw "Sharlayan.Lite contentHash does not match '$packageKey' sha512 in $AssetsPath."
}
if ($CheckBuiltDlls -and -not $allBuiltDllsMatch) {
    throw "One or more built Sharlayan.dll outputs do not match the official cached Sharlayan.dll!"
}
