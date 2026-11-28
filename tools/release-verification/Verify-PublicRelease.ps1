<#
.SYNOPSIS
Verifies a published GitHub Release for IronworksTranslator by anonymously downloading all assets.

.DESCRIPTION
Confirms the live public release contract after publishing to GitHub:
- Queries GitHub API via `gh api` for the release tag (and `/releases/latest` for Stable releases).
- Verifies draft=false, prerelease matches the selected channel, and Stable releases resolve as `/releases/latest`.
- Verifies release notes body matches -NotesFile when provided.
- Checks required channel assets exist and no opposite-channel Velopack assets were uploaded.
- Downloads every public asset anonymously via browser_download_url and verifies size, local SHA-256, and GitHub digest.
#>
param(
    [Parameter(Mandatory)]
    [string]$Version,
    [Parameter(Mandatory)]
    [string]$ReleaseDir,
    [ValidateSet("Stable", "Beta")]
    [string]$ReleaseChannel = "Stable",
    [string]$NotesFile = "",
    [string]$Repository = "sappho192/IronworksTranslator",
    [string]$DownloadDir = "",
    [switch]$KeepDownloads,
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

if (-not (Test-Path -LiteralPath $ReleaseDir -PathType Container)) {
    throw "ReleaseDir does not exist: $ReleaseDir"
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $ReleaseDir "PUBLIC-VERIFICATION.json"
}

$channel = if ($ReleaseChannel -eq "Beta") { "beta" } else { "win" }
$oppositeChannel = if ($ReleaseChannel -eq "Beta") { "win" } else { "beta" }

$rel = (& gh api "repos/$Repository/releases/tags/$Version") -join "`n" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $null -eq $rel) {
    throw "Failed to fetch GitHub release for tag '$Version' in $Repository."
}

$latestTag = $null
if ($ReleaseChannel -eq "Stable") {
    $latest = (& gh api "repos/$Repository/releases/latest") -join "`n" | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $null -eq $latest) {
        throw "Failed to fetch latest GitHub release for $Repository."
    }
    $latestTag = [string]$latest.tag_name
}

$bodyMatches = $null
if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
    if (-not (Test-Path -LiteralPath $NotesFile -PathType Leaf)) {
        throw "NotesFile does not exist: $NotesFile"
    }
    $expectedNotes = (Get-Content -LiteralPath $NotesFile -Raw -Encoding UTF8).Replace("`r`n", "`n").TrimEnd("`n")
    $actualBody = ([string]$rel.body).Replace("`r`n", "`n").TrimEnd("`n")
    $bodyMatches = ($expectedNotes -eq $actualBody)
}

$createdTempDownloadDir = $false
if ([string]::IsNullOrWhiteSpace($DownloadDir)) {
    $DownloadDir = Join-Path $env:TEMP ("iw-public-verify-" + $Version + "-" + [guid]::NewGuid().ToString("N"))
    $createdTempDownloadDir = $true
}
New-Item -ItemType Directory -Force -Path $DownloadDir | Out-Null

try {
    $requiredAssets = @(
        "Sappho192.IronworksTranslator-$Version-full.nupkg",
        "Sappho192.IronworksTranslator-$channel-Setup.exe",
        "releases.$channel.json"
    )
    $uploadedNames = @($rel.assets | ForEach-Object { [string]$_.name })
    $missingRequired = @($requiredAssets | Where-Object { $_ -notin $uploadedNames })
    $crossChannelAssets = @(
        $uploadedNames | Where-Object {
            $_ -eq "releases.$oppositeChannel.json" -or
            $_ -eq "Sappho192.IronworksTranslator-$oppositeChannel-Setup.exe"
        }
    )

    $assetResults = @()
    $allAssetsMatch = $true
    foreach ($asset in ($rel.assets | Sort-Object name)) {
        $dlPath = Join-Path $DownloadDir $asset.name
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $dlPath -UseBasicParsing
        $dlHash = (Get-FileHash -LiteralPath $dlPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $dlSize = (Get-Item -LiteralPath $dlPath).Length

        $localPath = Join-Path $ReleaseDir $asset.name
        $localExists = Test-Path -LiteralPath $localPath -PathType Leaf
        $localHash = if ($localExists) { (Get-FileHash -LiteralPath $localPath -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
        $localSize = if ($localExists) { (Get-Item -LiteralPath $localPath).Length } else { -1 }

        $digestMatches = [string]::IsNullOrWhiteSpace([string]$asset.digest) -or ([string]$asset.digest -eq "sha256:$dlHash")
        $matchesLocal = $localExists -and ($dlHash -eq $localHash) -and ($dlSize -eq $localSize) -and ($dlSize -eq [int64]$asset.size) -and $digestMatches
        if (-not $matchesLocal) {
            $allAssetsMatch = $false
        }

        $assetResults += [ordered]@{
            name          = $asset.name
            state         = $asset.state
            size          = $dlSize
            sha256        = $dlHash
            githubDigest  = $asset.digest
            digestMatches = $digestMatches
            matchesLocal  = $matchesLocal
            url           = $asset.browser_download_url
        }
    }

    $expectedPrerelease = ($ReleaseChannel -eq "Beta")
    $out = [ordered]@{
        repository               = $Repository
        url                      = $rel.html_url
        tag                      = $rel.tag_name
        title                    = $rel.name
        releaseChannel           = $ReleaseChannel
        velopackChannel          = $channel
        draft                    = [bool]$rel.draft
        prerelease               = [bool]$rel.prerelease
        prereleaseMatchesChannel = ([bool]$rel.prerelease -eq $expectedPrerelease)
        latestEndpointTag        = $latestTag
        bodyMatchesSuppliedNotes = $bodyMatches
        missingRequiredAssets    = $missingRequired
        crossChannelAssets       = $crossChannelAssets
        allAssetsMatchLocal      = $allAssetsMatch
        downloadDirectory        = if ($KeepDownloads -or -not $createdTempDownloadDir) { [System.IO.Path]::GetFullPath($DownloadDir) } else { "(cleaned temp dir)" }
        assets                   = $assetResults
    }

    $json = ($out | ConvertTo-Json -Depth 8) + "`n"
    Write-Utf8NoBom -Path $OutputPath -Content $json
    Write-Output $json

    $contractPassed = (-not $out.draft) -and
        $out.prereleaseMatchesChannel -and
        ($ReleaseChannel -ne "Stable" -or $out.latestEndpointTag -eq $Version) -and
        ($null -eq $bodyMatches -or $bodyMatches) -and
        ($missingRequired.Count -eq 0) -and
        ($crossChannelAssets.Count -eq 0) -and
        $allAssetsMatch

    if (-not $contractPassed) {
        throw "Public release verification failed! Inspect $OutputPath for details."
    }
}
finally {
    if ($createdTempDownloadDir -and -not $KeepDownloads -and (Test-Path -LiteralPath $DownloadDir)) {
        Remove-Item -LiteralPath $DownloadDir -Recurse -Force
    }
}
