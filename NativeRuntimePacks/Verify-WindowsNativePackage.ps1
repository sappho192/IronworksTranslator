param(
    [Parameter(Mandatory)]
    [ValidateSet("vulkan", "cuda")]
    [string]$Backend,
    [Parameter(Mandatory)]
    [string]$PackagePath,
    [string]$ArchivePath = "",
    [switch]$RequireRuntimePrerequisites
)

$ErrorActionPreference = "Stop"
Import-Module Microsoft.PowerShell.Utility -ErrorAction Stop

function Resolve-AbsolutePath([string]$Path) {
    return (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path
}

function Get-ExpectedPackageNames([string]$SelectedBackend) {
    $names = @("llama.dll", "ggml.dll", "ggml-base.dll", "ggml-cpu.dll")
    if ($SelectedBackend -eq "vulkan") {
        return @($names + "ggml-vulkan.dll", "shader-profile.json", "native-manifest.json")
    }

    return @($names + "ggml-cuda.dll", "native-manifest.json")
}

function Get-PathResolvedFiles([string[]]$Names) {
    $directories = @(
        $env:PATH -split [System.IO.Path]::PathSeparator |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
    )
    $files = [ordered]@{}
    foreach ($name in $Names) {
        $files[$name] = $null
        foreach ($directory in $directories) {
            $candidate = Join-Path $directory $name
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $files[$name] = [System.IO.Path]::GetFullPath($candidate)
                break
            }
        }
    }

    return $files
}

function Get-VcRedistState {
    $registryPaths = @(
        "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
    )
    $states = @(
        foreach ($registryPath in $registryPaths) {
            $item = Get-ItemProperty -Path $registryPath -ErrorAction SilentlyContinue
            if ($null -ne $item) {
                [ordered]@{
                    registryPath = $registryPath
                    installed = ($item.Installed -eq 1)
                    version = $item.Version
                }
            }
        }
    )

    return [ordered]@{
        installed = @($states | Where-Object { $_.installed }).Count -gt 0
        registrations = $states
    }
}

$package = Resolve-AbsolutePath $PackagePath
if (-not (Test-Path -LiteralPath $package -PathType Container)) {
    throw "PackagePath must be a directory: $package"
}

$expectedNames = @(Get-ExpectedPackageNames $Backend | Sort-Object)
$actualItems = @(Get-ChildItem -LiteralPath $package -Force)
$actualNames = @($actualItems | Where-Object { -not $_.PSIsContainer } | Select-Object -ExpandProperty Name | Sort-Object)
$unexpectedDirectories = @($actualItems | Where-Object { $_.PSIsContainer } | Select-Object -ExpandProperty Name)
$missingNames = @($expectedNames | Where-Object { $_ -notin $actualNames })
$unexpectedNames = @($actualNames | Where-Object { $_ -notin $expectedNames })
if ($missingNames.Count -gt 0 -or $unexpectedNames.Count -gt 0 -or $unexpectedDirectories.Count -gt 0) {
    throw "Package file set mismatch. Missing=[$($missingNames -join ', ')]; unexpected files=[$($unexpectedNames -join ', ')]; unexpected directories=[$($unexpectedDirectories -join ', ')]"
}

$manifestPath = Join-Path $package "native-manifest.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$cudaRuntimeDependencyMode = if ($Backend -eq "cuda" -and -not [string]::IsNullOrWhiteSpace($manifest.cudaRuntimeDependencyMode)) {
    [string]$manifest.cudaRuntimeDependencyMode
} else {
    # Packages built before the profiled CUDA package manifest field existed
    # retain the historical runtime/cuBLAS prerequisite policy.
    "cuda-runtime-cublas"
}
if ($Backend -eq "cuda" -and $cudaRuntimeDependencyMode -notin @("driver-only", "cuda-runtime-cublas")) {
    throw "Unknown CUDA runtime dependency mode in native-manifest.json: $cudaRuntimeDependencyMode"
}
$expectedNativeNames = @("llama.dll", "ggml.dll", "ggml-base.dll", "ggml-cpu.dll", "ggml-$Backend.dll" | Sort-Object)
$manifestNativeNames = @($manifest.nativeLibraries | ForEach-Object { $_.name } | Sort-Object)
$manifestNameDifference = @(Compare-Object -ReferenceObject $expectedNativeNames -DifferenceObject $manifestNativeNames)
if ($manifestNameDifference.Count -gt 0) {
    throw "native-manifest.json does not describe the expected $Backend native DLL set."
}

$libraryChecks = @(
    foreach ($entry in $manifest.nativeLibraries) {
        $path = Join-Path $package $entry.name
        $item = Get-Item -LiteralPath $path
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($item.Length -ne [int64]$entry.bytes -or $hash -ne $entry.sha256.ToLowerInvariant()) {
            throw "Manifest hash or size mismatch for $($entry.name)."
        }

        [ordered]@{
            name = $entry.name
            bytes = $item.Length
            sha256 = $hash
        }
    }
)

$shaderProfileCheck = $null
if ($Backend -eq "vulkan") {
    $shaderProfilePath = Join-Path $package "shader-profile.json"
    $shaderProfileHash = (Get-FileHash -LiteralPath $shaderProfilePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($manifest.shaderProfileSha256) -or $shaderProfileHash -ne $manifest.shaderProfileSha256.ToLowerInvariant()) {
        throw "Manifest shader profile hash does not match shader-profile.json."
    }

    $shaderProfileCheck = [ordered]@{
        name = "shader-profile.json"
        sha256 = $shaderProfileHash
    }
}

$cudaFatbinCheck = $null
if ($Backend -eq "cuda") {
    $hasFatbinEvidence =
        ($manifest.PSObject.Properties.Name -contains "cudaNativeCubinArchitectures") -and
        ($manifest.PSObject.Properties.Name -contains "cudaPtxArchitectures") -and
        ($manifest.PSObject.Properties.Name -contains "cudaFatbinAudit") -and
        ($manifest.PSObject.Properties.Name -contains "cudaDependencyAudit")
    if ($manifest.modelProfile -eq "milmmt-gemma3-q4km" -and -not $hasFatbinEvidence) {
        throw "Profiled CUDA package manifest is missing the required fatbin target audit. Rebuild with the current CUDA native builder."
    }
    if ($hasFatbinEvidence) {
        $nativeCubins = @($manifest.cudaNativeCubinArchitectures | ForEach-Object { ([string]$_).ToLowerInvariant() } | Sort-Object -Unique)
        $ptxTargets = @($manifest.cudaPtxArchitectures | ForEach-Object { ([string]$_).ToLowerInvariant() } | Sort-Object -Unique)
        $auditedCubins = @($manifest.cudaFatbinAudit.actual.sass.targets | ForEach-Object { ([string]$_.target).ToLowerInvariant() } | Sort-Object -Unique)
        $auditedPtx = @($manifest.cudaFatbinAudit.actual.ptx.targets | ForEach-Object { ([string]$_.target).ToLowerInvariant() } | Sort-Object -Unique)
        if ($nativeCubins.Count -eq 0) {
            throw "CUDA fatbin audit contains no native SASS/cubin target."
        }
        if (@(Compare-Object -ReferenceObject $nativeCubins -DifferenceObject $auditedCubins).Count -gt 0 -or
            @(Compare-Object -ReferenceObject $ptxTargets -DifferenceObject $auditedPtx).Count -gt 0) {
            throw "CUDA fatbin target fields do not match cudaFatbinAudit."
        }
        $generic12xPtx = @($ptxTargets | Where-Object { $_ -match '^sm_12[0-9]$' })
        $preservesGeneric12x = [bool]$manifest.cudaPreserveGeneric12x
        if ($generic12xPtx.Count -gt 0 -and -not $preservesGeneric12x) {
            throw "Generic 12X PTX requires cudaPreserveGeneric12x=true in native-manifest.json."
        }
        if ($preservesGeneric12x -and $generic12xPtx.Count -eq 0) {
            throw "cudaPreserveGeneric12x=true was recorded but the fatbin contains no generic 12X PTX fallback target."
        }
        $forbiddenCudaDependencies = @($manifest.cudaDependencyAudit.forbiddenDependencies | ForEach-Object { ([string]$_).ToLowerInvariant() })
        $auditedDependencies = @($manifest.cudaDependencyAudit.dependencies | ForEach-Object { ([string]$_).ToLowerInvariant() })
        if (-not [bool]$manifest.cudaDependencyAudit.passes -or
            @($auditedDependencies | Where-Object { $_ -in $forbiddenCudaDependencies }).Count -gt 0) {
            throw "CUDA dependency audit does not prove the driver-only dependency policy."
        }
        $cudaFatbinCheck = [ordered]@{
            schemaVersion = $manifest.cudaFatbinAudit.schemaVersion
            nativeCubinArchitectures = $nativeCubins
            ptxArchitectures = $ptxTargets
            generic12xPtxArchitectures = $generic12xPtx
            preservesGeneric12x = $preservesGeneric12x
            cuobjdumpPath = $manifest.cudaFatbinAudit.cuobjdump.path
            dependencyAudit = [ordered]@{
                dependencies = $auditedDependencies
                forbiddenDependencies = $forbiddenCudaDependencies
                dumpbinPath = $manifest.cudaDependencyAudit.dumpbinPath
            }
        }
    }
}

$archiveCheck = $null
if (-not [string]::IsNullOrWhiteSpace($ArchivePath)) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = Resolve-AbsolutePath $ArchivePath
    $zip = [System.IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $archiveNames = @($zip.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) } | ForEach-Object { $_.FullName } | Sort-Object)
        $archiveDifference = @(Compare-Object -ReferenceObject $expectedNames -DifferenceObject $archiveNames)
        if ($archiveDifference.Count -gt 0) {
            throw "Archive file set does not match the package directory: $archive"
        }
        $archiveCheck = [ordered]@{
            path = $archive
            bytes = (Get-Item -LiteralPath $archive).Length
            sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
            entries = $archiveNames
        }
    }
    finally {
        $zip.Dispose()
    }
}

$vcRedist = Get-VcRedistState
$runtime = if ($Backend -eq "vulkan") {
    $vulkanLoader = Join-Path $env:WINDIR "System32\vulkan-1.dll"
    [ordered]@{
        prerequisite = "Vulkan 1.2+ driver/loader and Microsoft Visual C++ v14 x64 Redistributable"
        vcRedist = $vcRedist
        vulkanLoaderPath = if (Test-Path -LiteralPath $vulkanLoader -PathType Leaf) { $vulkanLoader } else { $null }
    }
}
else {
    if ($cudaRuntimeDependencyMode -eq "driver-only") {
        $cudaDriver = Join-Path $env:WINDIR "System32\nvcuda.dll"
        [ordered]@{
            mode = $cudaRuntimeDependencyMode
            prerequisite = "NVIDIA display driver (nvcuda.dll) and Microsoft Visual C++ v14 x64 Redistributable"
            vcRedist = $vcRedist
            cudaDriverPath = if (Test-Path -LiteralPath $cudaDriver -PathType Leaf) { $cudaDriver } else { $null }
        }
    }
    else {
        $cudaFiles = Get-PathResolvedFiles @("cudart64_12.dll", "cublas64_12.dll", "cublasLt64_12.dll")
        [ordered]@{
            mode = $cudaRuntimeDependencyMode
            prerequisite = "CUDA 12 runtime/cuBLAS bundle compatible with this package and Microsoft Visual C++ v14 x64 Redistributable"
            vcRedist = $vcRedist
            cudaRuntimeFiles = $cudaFiles
        }
    }
}

$runtimeReady = if ($Backend -eq "vulkan") {
    $runtime.vcRedist.installed -and -not [string]::IsNullOrWhiteSpace($runtime.vulkanLoaderPath)
}
else {
    if ($runtime.mode -eq "driver-only") {
        $runtime.vcRedist.installed -and -not [string]::IsNullOrWhiteSpace($runtime.cudaDriverPath)
    }
    else {
        $runtime.vcRedist.installed -and (@($runtime.cudaRuntimeFiles.Values | Where-Object { $null -eq $_ }).Count -eq 0)
    }
}

$report = [ordered]@{
    backend = $Backend
    packagePath = $package
    expectedFiles = $expectedNames
    manifest = [ordered]@{
        sourceCommit = $manifest.sourceCommit
        expectedRevision = $manifest.expectedRevision
        runtime = $manifest.runtime
        cudaArchitectures = $manifest.cudaArchitectures
        cudaPreserveGeneric12x = if ($Backend -eq "cuda") { [bool]$manifest.cudaPreserveGeneric12x } else { $null }
        cudaRuntimeDependencyMode = if ($Backend -eq "cuda") { $cudaRuntimeDependencyMode } else { $null }
        cudaFatbin = $cudaFatbinCheck
        libraries = $libraryChecks
        shaderProfile = $shaderProfileCheck
    }
    archive = $archiveCheck
    runtimePrerequisites = $runtime
    runtimePrerequisitesReady = $runtimeReady
    integrityVerified = $true
}

$report | ConvertTo-Json -Depth 10
if ($RequireRuntimePrerequisites -and -not $runtimeReady) {
    exit 3
}
