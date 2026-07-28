---
name: release-ironworks-translator
description: Build, validate, tag, and publish IronworksTranslator Stable or Beta releases with GitVersion, Velopack, official NuGet provenance checks, MiLMMT native-runtime verification, GitHub assets, checksums, and public-download validation. Use when preparing or publishing an IronworksTranslator release, generating Stable/Beta update packages, validating release artifacts, or diagnosing a release gate failure.
---

# Release IronworksTranslator

## Preserve release integrity

- Read the repository `AGENTS.md`, `PUBLISH-README.md`, `UPDATE-TEST-CHECKLIST.md`, and `publish-release.ps1` before acting.
- Start with `git status --short --branch`. Preserve unrelated work and stop if the release input is not understood.
- Treat build/test, package integrity, public assets, installed update, and live FFXIV smoke as separate evidence layers.
- Never claim installation, automatic update, restart, FFXIV behavior, or CUDA/Vulkan model inference from packaging evidence alone.
- Never delete user NuGet caches, installed app files, `Setup.exe`, settings, logs, models, or unrelated release artifacts.
- Do not run an installer or another state-changing UI flow without immediate user confirmation.
- Use release notes exactly as supplied. If notes are absent, complete local packaging and validation, then stop immediately before GitHub release creation and request them.
- Record unsigned-package warnings. Do not imply that the binaries are signed.

## 1. Establish the release target

1. Resolve the repository, expected branch, target commit, version, and channel.
2. Use `master` for a normal Stable release unless the user explicitly chooses another target.
3. Fetch the target branch and compare local/remote SHAs without overwriting local tags.
4. Check local tags, remote tags, and GitHub releases for a version collision.
5. Confirm GitHub authentication and repository permissions.
6. Confirm Velopack CLI matches the project package version:

```powershell
dotnet tool list --global
```

7. Inspect `Releases` before packaging. Preserve the preceding full package so Velopack can generate a delta.
8. For Stable, use the `win` channel and a normal GitHub release. For Beta, use the `beta` channel and a GitHub prerelease. Never mix their JSON, installer, or package assets.

## 2. Tag locally before versioned validation

Create an annotated local tag on the exact release commit so GitVersion resolves the intended version:

```powershell
git tag -a <VERSION> <FULL_COMMIT_SHA> -m "IronworksTranslator <VERSION>"
```

- Verify `git rev-list -n 1 <VERSION>` equals `HEAD`.
- Keep the tag local until all local release gates pass.
- Do not silently move or replace an existing tag.
- If a gate fails, leave the unpublished tag and report the failure unless the user asks to remove or retarget it.

For Beta, use the final SemVer tag such as `2.1.0-beta.1` and pass only the suffix such as `beta.1` to `publish-release.ps1`.

## 3. Restore only from official NuGet

Do not trust the user-wide NuGet cache or enabled local feeds for release inputs.

1. Create a new, release-specific cache under a temporary directory. Fail if the selected path already exists.
2. Set both variables in every restore, test, build, and publish process:

```powershell
$env:NUGET_PACKAGES = "C:\tmp\ironworks-<VERSION>-nuget"
$env:RestoreSources = "https://api.nuget.org/v3/index.json"
```

3. Restore the solution:

```powershell
dotnet restore src\IronworksTranslator\IronworksTranslator.sln
```

4. Verify `Sharlayan.Lite/<version>/.nupkg.metadata`:
   - `source` equals `https://api.nuget.org/v3/index.json`.
   - `contentHash` equals the `Sharlayan.Lite/<version>` `sha512` value in `src/IronworksTranslator/obj/project.assets.json`.
5. Record the official cache DLL SHA-256, file version, and product/informational version commit.
6. After build/publish, compare that DLL with the Release/RID build input under `src/IronworksTranslator/bin/Release`.
7. Expect the final app to be a single-file executable; absence of a separate `Sharlayan.dll` in the publish directory is not a mismatch. Never substitute a local-feed DLL.

## 4. Run Release tests and native-runtime gates

Run the full test project in Release with the official cache:

```powershell
dotnet test tests\IronworksTranslator.Tests\IronworksTranslator.Tests.csproj -c Release --no-restore
```

- Require zero failed tests and record the passed/skipped counts.
- Verify the source CUDA and Vulkan packages with:

```powershell
.\NativeRuntimePacks\Verify-WindowsNativePackage.ps1 `
  -Backend cuda `
  -PackagePath .\NativeRuntimePacks\runtimes\win-x64\native\milmmt-cuda

.\NativeRuntimePacks\Verify-WindowsNativePackage.ps1 `
  -Backend vulkan `
  -PackagePath .\NativeRuntimePacks\runtimes\win-x64\native\milmmt-vulkan
```

- Require `integrityVerified=true` for both.
- Prefer the bundle verifier as an additional gate.
- Account for Git CRLF conversion if the bundle verifier rejects only delivery text files. Compare raw and LF-normalized bytes with `runtime-packs-manifest.json`; accept the checkout-format explanation only when normalized hashes and sizes match exactly.
- Never waive a DLL, `native-manifest.json`, shader profile, runtime manifest, or license mismatch as a line-ending issue.

## 5. Build the correct channel

Keep the official NuGet environment active and run one of:

```powershell
# Stable
.\publish-release.ps1 -ReleaseChannel Stable

# Beta
.\publish-release.ps1 `
  -ReleaseChannel Beta `
  -PrereleaseLabel beta.N `
  -VelopackOutputDir Releases\beta
```

Require and record:

- GitVersion and package version equal the intended release.
- Stable reports channel `win`; Beta reports channel `beta`.
- Release builds finish with zero warnings and zero errors.
- App and launcher file versions equal the intended version.
- App product version names the exact release commit.
- Full package, installer, channel JSON, and delta package when available are newly generated.
- Stable packages are rebuilt as Stable; never rename Beta outputs.

## 6. Inspect the packaged application

Extract the full `.nupkg` into a new unique temporary directory. Do not reuse or recursively clear a broad directory.

Verify:

- `IronworksTranslator.exe`, `IronworksTranslator.Launcher.exe`, and `IronworksMiLMMTNativeProbe.exe` exist.
- App and launcher file/product versions match the tag and commit.
- `LICENSES\llama.cpp-MIT.txt` matches the repository source hash.
- `milmmt-runtime-packs-manifest.json` and both backend `native-manifest.json` files exist.
- Running `Verify-WindowsNativePackage.ps1` against the extracted CUDA and Vulkan directories returns `integrityVerified=true`.
- `releases.win.json` or `releases.beta.json` contains the new full/delta entries, correct hashes and sizes, and the expected previous full package entry when generating an update.

Do not treat static runtime integrity as hardware or model-inference smoke.

## 7. Prepare release notes and checksums

Save the supplied notes as `Releases\<VERSION>-release-notes.md` without rewriting them.

Create `Releases\<VERSION>-sha256.txt` containing SHA-256 entries for:

- the new full package;
- the new delta package when generated;
- the channel installer;
- the channel release JSON.

Recompute every hash and verify the checksum file before publishing.

Stable release assets:

- `Sappho192.IronworksTranslator-<VERSION>-full.nupkg`
- `Sappho192.IronworksTranslator-<VERSION>-delta.nupkg` when generated
- `Sappho192.IronworksTranslator-win-Setup.exe`
- `releases.win.json`
- `<VERSION>-sha256.txt`

Beta release assets use the Beta package names, Beta installer, and `releases.beta.json`.

## 8. Publish only after local gates pass

1. Verify the worktree is clean apart from ignored release outputs.
2. Verify the local tag targets the exact release commit.
3. Push the branch and tag atomically when supported:

```powershell
git push --atomic origin master refs/tags/<VERSION>
```

4. Verify the remote branch SHA and dereferenced annotated-tag target.
5. Confirm the GitHub release still does not exist.
6. Prefer the GitHub connector for repository metadata. Use `gh` for release creation and binary upload when the connector has no release-asset operation.
7. Create the release with `--verify-tag`:

```powershell
gh release create <VERSION> `
  --repo sappho192/IronworksTranslator `
  --verify-tag `
  --title "IronworksTranslator <VERSION>" `
  --notes-file "Releases\<VERSION>-release-notes.md" `
  <ASSET_PATHS>
```

- Add `--prerelease` for Beta.
- Do not use `--latest=false` for a normal Stable release.
- If creation partially fails, inspect the existing release and uploaded assets before retrying. Do not create a duplicate or overwrite unexplained assets.

## 9. Verify the public release

1. Query the release and require:
   - correct tag and title;
   - `isDraft=false`;
   - Stable `isPrerelease=false`, or Beta `isPrerelease=true`;
   - all expected assets in `uploaded` state with the expected sizes and digests.
2. For Stable, query the repository latest-release endpoint and require the new tag.
3. Download every asset anonymously from the public release URL into a new temporary directory.
4. Compare every public SHA-256 with the local artifact.
5. Read the published body and compare it with the supplied notes.
6. Recheck `git status --short --branch` and remote synchronization.

## 10. Report evidence and remaining gates

Lead with the release URL and outcome. Include:

- tag and target commit;
- channel and version;
- build warning/error counts;
- test counts;
- Sharlayan source, content-hash match, DLL hash, and informational commit;
- source and packaged CUDA/Vulkan integrity results;
- uploaded asset list and public hash result;
- whether packages are unsigned;
- whether installation, automatic update/restart, live FFXIV, and CUDA/Vulkan model smoke were actually performed.

Request separate confirmation immediately before running the installer or replacing an installed channel.
