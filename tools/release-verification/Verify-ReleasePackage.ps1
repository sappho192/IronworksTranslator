<#
.SYNOPSIS
Verifies a locally built and Velopack-packaged IronworksTranslator release before publishing to GitHub.

.DESCRIPTION
Performs end-to-end pre-publication checks on a release output:
- Re-verifies Sharlayan.Lite official NuGet provenance and built Sharlayan.dll SHA-256 (unless -SkipProvenanceCheck is set).
- Extracts the Velopack full .nupkg and checks app/launcher/probe/WatchDog binaries against publish outputs and expected version.
- Verifies bundled llama.cpp MIT license and MiLMMT runtime-packs manifest match repository sources.
- Checks for forbidden legacy Hermes/FFXIVClientStructs artifacts inside the packaged app root.
- Runs NativeRuntimePacks\Verify-WindowsNativePackage.ps1 on packaged CUDA and Vulkan runtime packs.
- Optionally runs published IronworksMiLMMTNativeProbe.exe on real hardware for CUDA and Vulkan backends.
- Verifies releases.<channel>.json asset sizes, SHA-1, and SHA-256 hashes.
- Optionally writes <Version>-sha256.txt for uploadable release assets.
#>
param(
    [Parameter(Mandatory)]
    [string]$Version,
    [ValidateSet("Stable", "Beta")]
    [string]$ReleaseChannel = "Stable",
    [string]$ReleaseDir = "",
    [string]$PublishDir = "",
    [string]$CacheDir = $env:NUGET_PACKAGES,
    [string]$ProvenancePath = "",
    [switch]$SkipProvenanceCheck,
    [switch]$RunHardwareNativeProbe,
    [switch]$WriteSha256File,
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

function Invoke-HardwareProbe([string]$ProbeExe, [string]$Backend, [string]$LlamaDllPath, [string]$ExpectedBufferPrefix) {
    $rawLines = @(& $ProbeExe --backend $Backend --native-library $LlamaDllPath 2>&1 | ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
    $text = $rawLines -join "`n"
    $parsed = $null
    for ($i = $rawLines.Count - 1; $i -ge 0; $i--) {
        $candidate = $rawLines[$i].Trim()
        if ($candidate.StartsWith("{") -and $candidate.EndsWith("}")) {
            try {
                $parsed = $candidate | ConvertFrom-Json
                break
            }
            catch {
                $parsed = $null
            }
        }
    }
    $buffers = @()
    if ($null -ne $parsed) {
        $rawBuffers = if ($null -ne $parsed.nativeBackendBufferTypes) { $parsed.nativeBackendBufferTypes } else { $parsed.buffers }
        if ($null -ne $rawBuffers) {
            $buffers = @($rawBuffers | ForEach-Object { [string]$_ })
        }
    }
    $hasExpectedBuffer = @($buffers | Where-Object { $_ -like "$ExpectedBufferPrefix*" }).Count -gt 0
    $passed = ($exitCode -eq 0) -and ($null -ne $parsed) -and ([bool]$parsed.available) -and $hasExpectedBuffer
    return [ordered]@{
        backend              = $Backend
        nativeLibrary        = [System.IO.Path]::GetFullPath($LlamaDllPath)
        exitCode             = $exitCode
        available            = if ($null -ne $parsed) { [bool]$parsed.available } else { $false }
        buffers              = $buffers
        expectedBufferPrefix = $ExpectedBufferPrefix
        hasExpectedBuffer    = $hasExpectedBuffer
        passed               = $passed
        rawOutput            = if ($passed) { $null } else { $text.Trim() }
    }
}

$channel = if ($ReleaseChannel -eq "Beta") { "beta" } else { "win" }
if ([string]::IsNullOrWhiteSpace($ReleaseDir)) {
    $ReleaseDir = if ($ReleaseChannel -eq "Beta") { "Releases\beta" } else { "Releases" }
}
if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = "publish\IronworksTranslator ($Version)"
}
if ([string]::IsNullOrWhiteSpace($ProvenancePath)) {
    $ProvenancePath = Join-Path $ReleaseDir "SHARLAYAN-PROVENANCE.json"
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ReleaseDir "PACKAGE-VERIFICATION.json"
}

if (-not (Test-Path -LiteralPath $ReleaseDir -PathType Container)) {
    throw "ReleaseDir does not exist: $ReleaseDir"
}
if (-not (Test-Path -LiteralPath $PublishDir -PathType Container)) {
    throw "PublishDir does not exist: $PublishDir"
}

$commit = (git rev-parse HEAD).Trim()
$baseSemVer = ($Version -split '-')[0]

$provenanceSummary = $null
$provenancePassed = $true
if (-not $SkipProvenanceCheck) {
    $provenanceScript = Join-Path $PSScriptRoot "Verify-SharlayanProvenance.ps1"
    $effectiveCacheDir = $CacheDir
    if ([string]::IsNullOrWhiteSpace($effectiveCacheDir) -and (Test-Path -LiteralPath $ProvenancePath -PathType Leaf)) {
        $existingProv = Get-Content -LiteralPath $ProvenancePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $effectiveCacheDir = [string]$existingProv.cacheDir
    }
    if ([string]::IsNullOrWhiteSpace($effectiveCacheDir)) {
        throw "Sharlayan provenance check requires -CacheDir, NUGET_PACKAGES, or an existing SHARLAYAN-PROVENANCE.json (or pass -SkipProvenanceCheck)."
    }

    $provRaw = & $provenanceScript -CacheDir $effectiveCacheDir -CheckBuiltDlls -OutputPath $ProvenancePath
    $provenanceSummary = ($provRaw -join "`n") | ConvertFrom-Json
    $provenancePassed = [bool]$provenanceSummary.sourceMatchesOfficial -and
        [bool]$provenanceSummary.contentHashMatchesAssets -and
        [bool]$provenanceSummary.allBuiltDllsMatchCache
}

$fullNupkgName = "Sappho192.IronworksTranslator-$Version-full.nupkg"
$deltaNupkgName = "Sappho192.IronworksTranslator-$Version-delta.nupkg"
$setupExeName = "Sappho192.IronworksTranslator-$channel-Setup.exe"
$releasesJsonName = "releases.$channel.json"
$fullNupkgPath = Join-Path $ReleaseDir $fullNupkgName

if (-not (Test-Path -LiteralPath $fullNupkgPath -PathType Leaf)) {
    throw "Full Velopack package not found: $fullNupkgPath"
}

$extractDir = Join-Path $env:TEMP ("iw-verify-" + $Version + "-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $extractDir | Out-Null
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $fullNupkgPath).Path, $extractDir)

    $appRoot = Join-Path $extractDir "lib\net45"
    if (-not (Test-Path -LiteralPath $appRoot -PathType Container)) {
        $appRoot = Get-ChildItem -Directory (Join-Path $extractDir "lib") | Select-Object -First 1 -ExpandProperty FullName
    }
    if ([string]::IsNullOrWhiteSpace($appRoot) -or -not (Test-Path -LiteralPath $appRoot -PathType Container)) {
        throw "Could not locate lib/<tfm> application directory inside $fullNupkgPath"
    }

    $binaries = [ordered]@{}
    $allBinariesValid = $true
    foreach ($binName in @("IronworksMiLMMTNativeProbe.exe", "IronworksTranslator.exe", "IronworksTranslator.Launcher.exe", "WatchDogMain.exe")) {
        $pkgBin = Join-Path $appRoot $binName
        $pubBin = Join-Path $PublishDir $binName
        if (-not (Test-Path -LiteralPath $pkgBin -PathType Leaf)) {
            throw "Expected binary missing from packaged nupkg: $binName"
        }
        if (-not (Test-Path -LiteralPath $pubBin -PathType Leaf)) {
            throw "Expected binary missing from PublishDir: $pubBin"
        }
        $pkgHash = (Get-FileHash -LiteralPath $pkgBin -Algorithm SHA256).Hash.ToLowerInvariant()
        $pubHash = (Get-FileHash -LiteralPath $pubBin -Algorithm SHA256).Hash.ToLowerInvariant()
        $vi = (Get-Item -LiteralPath $pkgBin).VersionInfo
        $matchesPublish = ($pkgHash -eq $pubHash)
        $versionMatches = if ($binName -in @("IronworksTranslator.exe", "IronworksTranslator.Launcher.exe")) {
            ($vi.FileVersion -like "$baseSemVer.*")
        } else {
            $true
        }
        if (-not ($matchesPublish -and $versionMatches)) {
            $allBinariesValid = $false
        }
        $binaries[$binName] = [ordered]@{
            sha256         = $pkgHash
            fileVersion    = $vi.FileVersion
            productVersion = $vi.ProductVersion
            matchesPublish = $matchesPublish
            versionMatches = $versionMatches
        }
    }

    $forbiddenFiles = @("address.json", "FFXIVClientStructs.dll")
    $foundForbidden = @(
        Get-ChildItem -LiteralPath $appRoot -Recurse -File |
        Where-Object { $_.Name -in $forbiddenFiles } |
        ForEach-Object { $_.FullName.Substring($appRoot.Length).TrimStart('\') }
    )

    $licPkg = (Get-FileHash -LiteralPath (Join-Path $appRoot "LICENSES\llama.cpp-MIT.txt") -Algorithm SHA256).Hash.ToLowerInvariant()
    $licSrc = (Get-FileHash -LiteralPath "NativeRuntimePacks\LICENSES\llama.cpp-MIT.txt" -Algorithm SHA256).Hash.ToLowerInvariant()
    $manPkg = (Get-FileHash -LiteralPath (Join-Path $appRoot "runtimes\win-x64\native\milmmt-runtime-packs-manifest.json") -Algorithm SHA256).Hash.ToLowerInvariant()
    $manSrc = (Get-FileHash -LiteralPath "NativeRuntimePacks\runtime-packs-manifest.json" -Algorithm SHA256).Hash.ToLowerInvariant()

    $reportDir = Split-Path -Parent ([System.IO.Path]::GetFullPath($OutputPath))
    $verifyNativeScript = "NativeRuntimePacks\Verify-WindowsNativePackage.ps1"
    $cudaRaw = (& $verifyNativeScript -Backend cuda -PackagePath (Join-Path $appRoot "runtimes\win-x64\native\milmmt-cuda")) -join "`n"
    Write-Utf8NoBom -Path (Join-Path $reportDir "packaged-cuda-verification.json") -Content ($cudaRaw.TrimEnd() + "`n")
    $cudaObj = $cudaRaw | ConvertFrom-Json

    $vulkanRaw = (& $verifyNativeScript -Backend vulkan -PackagePath (Join-Path $appRoot "runtimes\win-x64\native\milmmt-vulkan")) -join "`n"
    Write-Utf8NoBom -Path (Join-Path $reportDir "packaged-vulkan-verification.json") -Content ($vulkanRaw.TrimEnd() + "`n")
    $vulkanObj = $vulkanRaw | ConvertFrom-Json

    $hardwareProbe = $null
    $hardwareProbePassed = $true
    if ($RunHardwareNativeProbe) {
        $probeExe = Join-Path $PublishDir "IronworksMiLMMTNativeProbe.exe"
        $cudaProbe = Invoke-HardwareProbe `
            -ProbeExe $probeExe `
            -Backend "cuda" `
            -LlamaDllPath (Join-Path $PublishDir "runtimes\win-x64\native\milmmt-cuda\llama.dll") `
            -ExpectedBufferPrefix "CUDA"
        $vulkanProbe = Invoke-HardwareProbe `
            -ProbeExe $probeExe `
            -Backend "vulkan" `
            -LlamaDllPath (Join-Path $PublishDir "runtimes\win-x64\native\milmmt-vulkan\llama.dll") `
            -ExpectedBufferPrefix "Vulkan"
        $hardwareProbePassed = $cudaProbe.passed -and $vulkanProbe.passed
        $hardwareProbe = [ordered]@{
            cuda   = $cudaProbe
            vulkan = $vulkanProbe
            passed = $hardwareProbePassed
        }
    }

    $releasesJsonPath = Join-Path $ReleaseDir $releasesJsonName
    if (-not (Test-Path -LiteralPath $releasesJsonPath -PathType Leaf)) {
        throw "Velopack feed file not found: $releasesJsonPath"
    }
    $relJson = Get-Content -LiteralPath $releasesJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $relChecks = @()
    $allFeedAssetsValid = $true
    foreach ($asset in $relJson.Assets) {
        $assetPath = Join-Path $ReleaseDir $asset.FileName
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            throw "Asset listed in $releasesJsonName not found: $assetPath"
        }
        $item = Get-Item -LiteralPath $assetPath
        $sha1 = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA1).Hash.ToUpperInvariant()
        $sha256 = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $ok = ($item.Length -eq [int64]$asset.Size) -and
            ($sha1 -eq ([string]$asset.SHA1).ToUpperInvariant()) -and
            ($sha256 -eq ([string]$asset.SHA256).ToLowerInvariant())
        if (-not $ok) {
            $allFeedAssetsValid = $false
        }
        $relChecks += [ordered]@{
            fileName = $asset.FileName
            type     = $asset.Type
            version  = $asset.Version
            size     = $item.Length
            sha256   = $sha256
            verified = $ok
        }
    }

    $sha256FilePath = $null
    if ($WriteSha256File) {
        $shaLines = @()
        foreach ($name in @($fullNupkgName, $deltaNupkgName, $setupExeName, $releasesJsonName)) {
            $assetPath = Join-Path $ReleaseDir $name
            if (Test-Path -LiteralPath $assetPath -PathType Leaf) {
                $hash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash.ToLowerInvariant()
                $shaLines += "$hash  $name"
            }
        }
        $sha256FilePath = Join-Path $ReleaseDir "$Version-sha256.txt"
        Write-Utf8NoBom -Path $sha256FilePath -Content (($shaLines -join "`n") + "`n")
    }

    $summary = [ordered]@{
        version                         = $Version
        releaseChannel                  = $ReleaseChannel
        velopackChannel                 = $channel
        commit                          = $commit
        provenanceChecked               = (-not $SkipProvenanceCheck)
        provenancePassed                = $provenancePassed
        sharlayanPackage                = if ($null -ne $provenanceSummary) { $provenanceSummary.package } else { $null }
        sharlayanDllSha256              = if ($null -ne $provenanceSummary) { $provenanceSummary.cacheDllSha256 } else { $null }
        sharlayanDllProductVersion      = if ($null -ne $provenanceSummary) { $provenanceSummary.cacheDllProductVersion } else { $null }
        allBinariesValid                = $allBinariesValid
        binaries                        = $binaries
        forbiddenLegacyArtifacts        = $foundForbidden
        licenseMatchesSource            = ($licPkg -eq $licSrc)
        runtimeManifestMatchesSource    = ($manPkg -eq $manSrc)
        packagedCudaIntegrityVerified   = [bool]$cudaObj.integrityVerified
        packagedVulkanIntegrityVerified = [bool]$vulkanObj.integrityVerified
        hardwareNativeProbe             = $hardwareProbe
        releasesFeedFile                = $releasesJsonName
        allFeedAssetsValid              = $allFeedAssetsValid
        releasesFeedAssets              = $relChecks
        sha256FilePath                  = $sha256FilePath
    }

    $summaryJson = ($summary | ConvertTo-Json -Depth 10) + "`n"
    Write-Utf8NoBom -Path $OutputPath -Content $summaryJson
    Write-Output $summaryJson

    $allPassed = $provenancePassed -and
        $allBinariesValid -and
        ($foundForbidden.Count -eq 0) -and
        $summary.licenseMatchesSource -and
        $summary.runtimeManifestMatchesSource -and
        $summary.packagedCudaIntegrityVerified -and
        $summary.packagedVulkanIntegrityVerified -and
        $hardwareProbePassed -and
        $allFeedAssetsValid

    if (-not $allPassed) {
        throw "One or more release package verification checks failed! Inspect $OutputPath for details."
    }
}
finally {
    if (Test-Path -LiteralPath $extractDir) {
        Remove-Item -LiteralPath $extractDir -Recurse -Force
    }
}
