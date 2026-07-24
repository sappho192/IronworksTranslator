param(
    [Parameter(Mandatory)]
    [string]$BundlePath,
    [switch]$RequireRuntimePrerequisites
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath([string]$Path) {
    return (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
}

$bundle = Resolve-AbsolutePath $BundlePath
if (-not (Test-Path -LiteralPath $bundle -PathType Container)) {
    throw "BundlePath must be a directory: $bundle"
}

$manifestPath = Join-Path $bundle "runtime-packs-manifest.json"
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "runtime-packs-manifest.json was not found: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$manifest.schemaVersion -ne 1) {
    throw "Unsupported runtime-pack manifest schema: $($manifest.schemaVersion)"
}
if ([string]$manifest.managedLLamaSharpVersion -ne "0.27.0") {
    throw "This bundle is only validated with LLamaSharp 0.27.0; manifest declares $($manifest.managedLLamaSharpVersion)."
}

$deliveryChecks = @(
    foreach ($entry in @($manifest.deliveryFiles)) {
        $path = Join-Path $bundle ([string]$entry.relativePath)
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Bundle delivery file is missing: $($entry.relativePath)"
        }
        $item = Get-Item -LiteralPath $path
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($item.Length -ne [int64]$entry.bytes -or $hash -ne ([string]$entry.sha256).ToLowerInvariant()) {
            throw "Bundle delivery file hash or size mismatch: $($entry.relativePath)"
        }
        [ordered]@{
            relativePath = $entry.relativePath
            bytes = $item.Length
            sha256 = $hash
        }
    }
)

$nativeVerifier = Join-Path $bundle "Verify-WindowsNativePackage.ps1"
if (-not (Test-Path -LiteralPath $nativeVerifier -PathType Leaf)) {
    throw "Bundled native verifier was not found: $nativeVerifier"
}

$nativeChecks = @(
    foreach ($pack in @($manifest.nativePacks)) {
        $backend = [string]$pack.backend
        if ($backend -notin @("cuda", "vulkan")) {
            throw "Unsupported bundled backend: $backend"
        }

        $packPath = Join-Path $bundle ([string]$pack.relativeDirectory)
        $nativeManifestPath = Join-Path $packPath "native-manifest.json"
        if (-not (Test-Path -LiteralPath $nativeManifestPath -PathType Leaf)) {
            throw "Bundled $backend manifest is missing: $nativeManifestPath"
        }
        $nativeManifestHash = (Get-FileHash -LiteralPath $nativeManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($nativeManifestHash -ne ([string]$pack.nativeManifestSha256).ToLowerInvariant()) {
            throw "Bundled $backend native-manifest.json hash does not match runtime-packs-manifest.json."
        }

        if ($RequireRuntimePrerequisites) {
            $raw = & $nativeVerifier -Backend $backend -PackagePath $packPath -RequireRuntimePrerequisites
        }
        else {
            $raw = & $nativeVerifier -Backend $backend -PackagePath $packPath
        }
        if (-not $?) {
            throw "Bundled $backend native verification failed."
        }

        [ordered]@{
            backend = $backend
            relativeDirectory = $pack.relativeDirectory
            nativeManifestSha256 = $nativeManifestHash
            verification = ($raw | ConvertFrom-Json)
        }
    }
)

[ordered]@{
    bundlePath = $bundle
    managedLLamaSharpVersion = $manifest.managedLLamaSharpVersion
    sourceCommit = $manifest.sourceCommit
    deliveryFiles = $deliveryChecks
    nativePacks = $nativeChecks
    integrityVerified = $true
} | ConvertTo-Json -Depth 12
