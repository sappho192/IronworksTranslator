---
name: triage-translation-reports
description: Inspect pending IronworksTranslator user translation reports in Cloudflare D1, classify root causes (app preprocessing/matcher bug vs runtime glossary gap/noise vs model limitation), apply code/glossary fixes with tests, rebuild and deploy the runtime glossary to Cloudflare R2, and verify the live public endpoint. Use when reviewing user translation feedback, updating community_curated.json or runtime glossary, or publishing a new biggs.sapphosound.com glossary snapshot.
---

# Triage Translation Reports and Publish Runtime Glossary

## 1. Safety and privacy rules

- Never print, log, or commit Cloudflare account IDs, API tokens, local user paths, player names, or raw player chat into Git or `docs/agent-log/`.
- When `wrangler` requires non-interactive account selection, pass `CLOUDFLARE_ACCOUNT_ID` via the process environment only (never write it into tracked files).
- Always run read-only/local prep commands inside the sandbox first; use unsandboxed execution only for the individual `wrangler` / network commands that require it.

## 2. Inspect pending Cloudflare D1 reports

Query pending reports from the `ironworks-reports` D1 database (via `wrangler d1 execute --remote` or the curator tool under `tools/milmmt-report-curator/`).

For each pending report, classify it into one of 4 buckets before writing code:
1. **App / Text Preprocessing Bug**: SeString control bytes (`\x02...\x03`), inline tags, or encoding artifacts leaking into prompts → fix in `src/IronworksTranslator/Services/FFXIV/FfxivTextSanitizer.cs` and add xUnit tests.
2. **Glossary Matcher Bug**: Valid term missed due to punctuation, Korean particle boundary, or English inflection/plural → fix **both** `src/IronworksTranslator/Utils/Translator/MiLMMTGlossaryMatcher.cs` and `tools/milmmt-dataset-builder/build_sft_dataset.py` together, and update both C# and Python tests.
3. **Runtime Glossary Noise or Gap**:
   - Generic single English/Latin word misfiring (e.g., `Excellent`, `Stern`) → verify `LATIN_NOISY_SINGLE_WORD_CATEGORIES` and `PLACE_NAME_EN_STOPWORDS` in `tools/milmmt-glossary-builder/build_glossary.py`.
   - Missing community shorthand, slang, or proper term → add to `tools/milmmt-glossary-builder/community_curated.json` without duplicates or banned generic noise.
4. **Model Translation Limitation**: Grammar, nuance, or literal translation error with clean input and glossary → curate into future SFT / CPO preference data (`tools/milmmt-report-curator/curate_reports.py`) instead of hacking the glossary.

## 3. Validate code and glossary changes locally

1. Run Python tests:
   ```powershell
   artifacts\.venv-milmmt\Scripts\python.exe -m unittest discover -s tools/milmmt-glossary-builder -p "test_*.py" -v
   artifacts\.venv-milmmt\Scripts\python.exe -m unittest discover -s tools/milmmt-dataset-builder -p "test_*.py" -v
   ```
2. If any C# file changed, run:
   ```powershell
   dotnet test tests\IronworksTranslator.Tests\IronworksTranslator.Tests.csproj -c Debug
   ```
3. Rebuild the runtime glossary snapshot with an explicit dated version (`YYYY-MM-DD.N`):
   - Inspect pair counts, `npc_names`, `npc_styles`, dropped-noise counts, and spot-check every newly added/removed term directly in the generated `glossary.runtime.json`.
   - Confirm no generic single-word English noise terms remain in the runtime JSON.

## 4. Publish runtime glossary to Cloudflare R2 and verify live

1. Upload the verified `glossary.runtime.json` (and versioned snapshot if applicable) to R2 with `wrangler r2 object put --remote`.
2. Verify the public live endpoint (`https://biggs.sapphosound.com/v2/glossary`) by downloading anonymously and confirming:
   - `version` equals the newly published `YYYY-MM-DD.N`;
   - SHA-256 of the public response matches the local generated `glossary.runtime.json` byte-for-byte;
   - expected newly curated terms are present and removed noise terms are absent.
3. Update processed D1 report statuses only after the fix or live glossary deployment is verified.

## 5. Report summary

Summarize:
- number of reports reviewed and classification breakdown;
- code/glossary changes and test results;
- live glossary version, entry counts (`pairs`, `npc_names`, `npc_styles`), and public SHA-256 match (omitting account IDs and private player text).
