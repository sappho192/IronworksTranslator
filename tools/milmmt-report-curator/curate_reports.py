#!/usr/bin/env python3
"""Local CLI for pulling, reviewing, syncing, and exporting D1 translation reports.

Zero external dependencies (Python 3.10+ standard library only).

Subcommands:
  pull        Fetch translation_reports from Cloudflare D1 (via wrangler) or import JSON.
  summary     Print queue stats, automatic badges, and glossary/diff hints for pending rows.
  review      Interactive terminal review loop writing to curated/curation_log.jsonl.
  apply       Apply batch review decisions from a JSON/JSONL file deterministically.
  sync-status Generate (and with --execute, apply) D1 review_status UPDATE SQL.
  export      Build eval.jsonl, sft_user.jsonl, cpo_user.jsonl, reference_only.jsonl,
              overrides.json (Tier-0 glossary), and export_summary.json.
"""

from __future__ import annotations

import argparse
import datetime as dt
import difflib
import hashlib
import json
import re
import subprocess
import sys
import tempfile
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable, Sequence

VALID_REVIEW_STATUSES = ("approved", "rejected")
VALID_SPLITS = ("eval", "train", "reference_only")
VALID_FAILURE_TAGS = (
    "glossary",
    "language_drift",
    "register",
    "colloquial",
    "short_repetition",
    "tag_preservation",
    "other",
)

RE_UUID = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
RE_KATAKANA_RUN = re.compile(r"[\u30A0-\u30FF\u30FC]{3,}")
RE_BRACKET_TERM = re.compile(r"[「『]([^「」『』]{2,24})[」』]")
RE_PROPER_LATIN = re.compile(r"\b[A-Z][a-zA-Z'-]{2,}(?:\s+[A-Z][a-zA-Z'-]{2,}){0,3}\b")
RE_REPETITION_LOOP = re.compile(r"(.{1,4})\1{7,}")
RE_KANA = re.compile(r"[\u3040-\u309F\u30A0-\u30FF]")
RE_HANGUL = re.compile(r"[\uAC00-\uD7A3]")
RE_GAME_TAG = re.compile(r"(?:\[HQ\]|×\d+|[「『].+?[」』]|[A-Z]\.[A-Z]\.)")


# ---------------------------------------------------------------------------
# Normalization & hashing helpers
# ---------------------------------------------------------------------------

def normalize_text(text: str) -> str:
    text = unicodedata.normalize("NFKC", text)
    return re.sub(r"\s+", " ", text).strip()


def text_hash(source_language: str, target_language: str, source_text: str) -> str:
    key = f"{source_language}\u0000{target_language}\u0000{normalize_text(source_text)}"
    return hashlib.sha256(key.encode("utf-8")).hexdigest()


def utc_now_iso() -> str:
    return dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


# ---------------------------------------------------------------------------
# Wrangler JSON parsing & file I/O
# ---------------------------------------------------------------------------

def parse_wrangler_d1_json(raw_stdout: str) -> list[dict[str, Any]]:
    """Parse wrangler d1 execute --json stdout, skipping any leading npm notice lines."""
    text = raw_stdout.lstrip()
    bracket_idx = text.find("[")
    if bracket_idx < 0:
        raise ValueError("Could not find JSON array '[' in wrangler output.")
    payload = json.loads(text[bracket_idx:])
    if not isinstance(payload, list) or not payload:
        raise ValueError("Unexpected wrangler JSON structure: expected non-empty list.")
    first = payload[0]
    if not isinstance(first, dict) or "results" not in first:
        raise ValueError("Unexpected wrangler JSON structure: missing 'results'.")
    rows = first["results"]
    if not isinstance(rows, list):
        raise ValueError("Unexpected wrangler JSON structure: 'results' is not a list.")
    return [normalize_report_row(r) for r in rows]


def normalize_report_row(row: dict[str, Any]) -> dict[str, Any]:
    """Normalize a translation_reports row (parse glossary_terms_json safely)."""
    out = dict(row)
    raw_terms = out.get("glossary_terms_json")
    if isinstance(raw_terms, str):
        try:
            parsed = json.loads(raw_terms)
            out["glossary_terms"] = parsed if isinstance(parsed, list) else []
        except json.JSONDecodeError:
            out["glossary_terms"] = []
    elif isinstance(out.get("glossary_terms"), list):
        pass
    else:
        out["glossary_terms"] = []
    out["draft_edited"] = int(bool(out.get("draft_edited", 0)))
    return out


def load_reports(work_dir: Path) -> list[dict[str, Any]]:
    path = work_dir / "raw" / "reports_latest.json"
    if not path.exists():
        raise FileNotFoundError(f"Pulled reports file not found: {path}. Run 'pull' first.")
    data = json.loads(path.read_text(encoding="utf-8"))
    rows = data.get("reports", data) if isinstance(data, dict) else data
    return [normalize_report_row(r) for r in rows]


def load_curation_log(work_dir: Path) -> dict[str, dict[str, Any]]:
    """Load append-only curated/curation_log.jsonl, returning latest record per report id."""
    path = work_dir / "curated" / "curation_log.jsonl"
    latest: dict[str, dict[str, Any]] = {}
    if not path.exists():
        return latest
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        rec = json.loads(line)
        rid = rec.get("id")
        if rid:
            latest[rid] = rec
    return latest


def append_curation_decision(work_dir: Path, decision: dict[str, Any]) -> None:
    path = work_dir / "curated" / "curation_log.jsonl"
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8", newline="\n") as handle:
        handle.write(json.dumps(decision, ensure_ascii=False) + "\n")


# ---------------------------------------------------------------------------
# Automatic analysis: badges, failure tags, diff & glossary hints
# ---------------------------------------------------------------------------

def is_reference_only_candidate(row: dict[str, Any]) -> bool:
    return row.get("draft_source") in ("papago", "deepl") and int(row.get("draft_edited", 0)) == 0


def detect_badges(row: dict[str, Any], duplicate_counts: Counter[str]) -> list[str]:
    badges: list[str] = []
    if is_reference_only_candidate(row):
        badges.append("REFERENCE_ONLY")
    h = text_hash(row["source_language"], row["target_language"], row["source_text"])
    if duplicate_counts[h] > 1:
        badges.append(f"DUPLICATE_SOURCE({duplicate_counts[h]}x)")
    app_ver = str(row.get("app_version") or "")
    model_id = str(row.get("model_id") or "")
    if "-" in app_ver or "v0.1" in model_id:
        badges.append("DEV_OR_RETIRED")
    orig = str(row.get("original_translation") or "")
    if RE_REPETITION_LOOP.search(orig):
        badges.append("REPETITION_LOOP")
    if row.get("target_language") == "Korean" and RE_KANA.search(orig):
        badges.append("LANGUAGE_DRIFT")
    return badges


def suggest_failure_tags(row: dict[str, Any], glossary_hits: list[dict[str, str]]) -> list[str]:
    tags: list[str] = []
    orig = str(row.get("original_translation") or "")
    src = str(row.get("source_text") or "")
    if RE_REPETITION_LOOP.search(orig):
        tags.append("short_repetition")
    if row.get("target_language") == "Korean" and RE_KANA.search(orig):
        tags.append("language_drift")
    if glossary_hits:
        tags.append("glossary")
    if RE_GAME_TAG.search(src):
        tags.append("tag_preservation")
    if not tags and len(src.strip()) <= 16 and row.get("translation_kind") == "chat":
        tags.append("colloquial")
    return tags


def extract_diff_spans(original: str, suggested: str) -> list[tuple[str, str]]:
    """Return list of (old_span, new_span) token-level pairs where suggested differs from original."""
    if not original or not suggested or original.strip() == suggested.strip():
        return []
    tok_pattern = re.compile(r"[^\s'\"‘’“”「」『』\[\](),.!?]+")
    orig_tokens = tok_pattern.findall(original)
    sugg_tokens = tok_pattern.findall(suggested)
    matcher = difflib.SequenceMatcher(None, orig_tokens, sugg_tokens)
    spans: list[tuple[str, str]] = []
    for tag, i1, i2, j1, j2 in matcher.get_opcodes():
        if tag in ("replace", "insert"):
            old_s = " ".join(orig_tokens[i1:i2]).strip()
            new_s = " ".join(sugg_tokens[j1:j2]).strip()
            if new_s and len(new_s) <= 40:
                spans.append((old_s, new_s))
    return spans


def extract_source_term_candidates(source_text: str, source_language: str) -> list[str]:
    """Heuristically extract notable proper-noun / bracketed / katakana spans from source_text."""
    cands: list[str] = []
    seen: set[str] = set()
    for m in RE_BRACKET_TERM.finditer(source_text):
        term = m.group(1).strip()
        if term and term not in seen:
            seen.add(term)
            cands.append(term)
    if source_language == "Japanese":
        for m in RE_KATAKANA_RUN.finditer(source_text):
            term = m.group(0).strip()
            if term and term not in seen:
                seen.add(term)
                cands.append(term)
    elif source_language in ("English", "German", "French"):
        for m in RE_PROPER_LATIN.finditer(source_text):
            term = m.group(0).strip()
            if term and term not in seen and term.casefold() not in ("the", "welcome", "this", "that", "there"):
                seen.add(term)
                cands.append(term)
    return cands


class GlossaryLookup:
    """Longest-match lookup over a glossary JSON built by build_glossary.py."""

    def __init__(self, glossary_path: Path | None) -> None:
        self._pairs: dict[str, list[dict[str, Any]]] = {}
        if glossary_path and glossary_path.exists():
            data = json.loads(glossary_path.read_text(encoding="utf-8"))
            for pair_key, items in data.get("pairs", {}).items():
                # Keep sorted longest-first for greedy matching
                self._pairs[pair_key] = sorted(items, key=lambda x: -len(x["source"]))

    def find_matches(
        self,
        source_language: str,
        target_language: str,
        source_text: str,
        limit: int = 8,
    ) -> list[dict[str, str]]:
        pair_key = f"{source_language}->{target_language}"
        items = self._pairs.get(pair_key)
        if not items or not source_text:
            return []
        is_latin = source_language in ("English", "German", "French")
        haystack = source_text.casefold() if is_latin else source_text
        hits: list[dict[str, str]] = []
        matched_spans: list[tuple[int, int]] = []
        for item in items:
            src = item["source"]
            needle = src.casefold() if is_latin else src
            start_pos = 0
            while True:
                idx = haystack.find(needle, start_pos)
                if idx < 0:
                    break
                end = idx + len(needle)
                start_pos = idx + 1
                if is_latin:
                    # Word boundary check for Latin terms to avoid substring noise
                    if idx > 0 and haystack[idx - 1].isalnum():
                        continue
                    if end < len(haystack) and haystack[end].isalnum():
                        continue
                elif source_language == "Japanese":
                    # Katakana boundary check: avoid matching 'ステラ' inside 'カステラ'
                    is_katakana = lambda ch: ("\u30A0" <= ch <= "\u30FF") or ch == "\u30FC"
                    if is_katakana(needle[0]) and idx > 0 and is_katakana(haystack[idx - 1]):
                        continue
                    if is_katakana(needle[-1]) and end < len(haystack) and is_katakana(haystack[end]):
                        continue
                if any(not (end <= s or idx >= e) for s, e in matched_spans):
                    continue
                matched_spans.append((idx, end))
                hits.append(
                    {
                        "source": src,
                        "target": item["target"],
                        "origin": str(item.get("origin", "glossary")),
                    }
                )
                break
            if len(hits) >= limit:
                break
        return hits


def suggest_glossary_candidates(
    row: dict[str, Any],
    lookup: GlossaryLookup,
) -> list[dict[str, str]]:
    """Combine user-provided glossary_terms, official glossary hits, and diff-matched hints."""
    results: list[dict[str, str]] = []
    seen: set[tuple[str, str]] = set()

    # 1. User-submitted glossary_terms
    for t in row.get("glossary_terms", []):
        src = str(t.get("source", "")).strip()
        tgt = str(t.get("target", "")).strip()
        if src and tgt and (src, tgt) not in seen:
            seen.add((src, tgt))
            results.append({"source": src, "target": tgt, "origin": "user_report"})

    # 2. Official/curated glossary hits where original translation missed the target
    orig = str(row.get("original_translation") or "")
    sugg = str(row.get("suggested_translation") or "")
    hits = lookup.find_matches(row["source_language"], row["target_language"], row["source_text"])
    for h in hits:
        tgt_no_space = h["target"].replace(" ", "")
        orig_no_space = orig.replace(" ", "")
        sugg_no_space = sugg.replace(" ", "")
        if tgt_no_space not in orig_no_space or tgt_no_space in sugg_no_space:
            key = (h["source"], h["target"])
            if key not in seen:
                seen.add(key)
                results.append(h)

    return results


# ---------------------------------------------------------------------------
# Subcommand: pull
# ---------------------------------------------------------------------------

def cmd_pull(args: argparse.Namespace) -> int:
    work_dir: Path = args.work_dir
    raw_dir = work_dir / "raw"
    raw_dir.mkdir(parents=True, exist_ok=True)

    if args.from_json:
        raw_text = Path(args.from_json).read_text(encoding="utf-8")
        if raw_text.lstrip().startswith("{") and '"reports"' in raw_text[:200]:
            data = json.loads(raw_text)
            rows = [normalize_report_row(r) for r in data["reports"]]
        else:
            rows = parse_wrangler_d1_json(raw_text)
        source_label = f"file:{args.from_json}"
    else:
        where_clause = "WHERE review_status = 'pending'" if args.pending_only else ""
        sql = f"SELECT * FROM translation_reports {where_clause} ORDER BY created_at ASC;"
        cmd = [
            "npx",
            "wrangler",
            "d1",
            "execute",
            args.db_name,
            "--remote",
            "--json",
            "--command",
            sql,
        ]
        proc = subprocess.run(
            cmd,
            cwd=str(args.endpoint_dir),
            capture_output=True,
            text=True,
            encoding="utf-8",
            shell=(sys.platform == "win32"),
        )
        if proc.returncode != 0:
            sys.stderr.write(proc.stderr or proc.stdout)
            return proc.returncode
        rows = parse_wrangler_d1_json(proc.stdout)
        source_label = f"d1:{args.db_name}"

    snapshot = {
        "pulled_at": utc_now_iso(),
        "source": source_label,
        "count": len(rows),
        "reports": rows,
    }
    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%d_%H%M%S")
    latest_path = raw_dir / "reports_latest.json"
    archive_path = raw_dir / f"reports_{stamp}.json"
    serialized = json.dumps(snapshot, ensure_ascii=False, indent=2) + "\n"
    latest_path.write_text(serialized, encoding="utf-8")
    archive_path.write_text(serialized, encoding="utf-8")

    status_counts = Counter(r.get("review_status", "unknown") for r in rows)
    print(f"Pulled {len(rows)} reports from {source_label} -> {latest_path}")
    print(f"Status breakdown: {dict(status_counts)}")
    return 0


# ---------------------------------------------------------------------------
# Subcommand: summary
# ---------------------------------------------------------------------------

def cmd_summary(args: argparse.Namespace) -> int:
    rows = load_reports(args.work_dir)
    curated = load_curation_log(args.work_dir)
    lookup = GlossaryLookup(args.glossary_json)
    dup_counts = Counter(text_hash(r["source_language"], r["target_language"], r["source_text"]) for r in rows)

    pending_local = [r for r in rows if r["id"] not in curated and r.get("review_status") == "pending"]
    print(f"Total pulled: {len(rows)} | Locally curated: {len(curated)} | Pending review: {len(pending_local)}")
    print(f"By lang pair: {dict(Counter((r['source_language'] + '->' + r['target_language']) for r in rows))}")
    print(f"By engine:    {dict(Counter(r['engine'] for r in rows))}")
    print(f"By draft:     {dict(Counter((r['draft_source'], r['draft_edited']) for r in rows))}")

    if args.verbose:
        print("\n--- Pending items preview ---")
        for idx, r in enumerate(pending_local, 1):
            badges = detect_badges(r, dup_counts)
            gl_hits = suggest_glossary_candidates(r, lookup)
            tags = suggest_failure_tags(r, gl_hits)
            diff_spans = extract_diff_spans(r["original_translation"], r.get("suggested_translation") or "")
            print(f"\n[{idx}/{len(pending_local)}] id={r['id']}  {r['source_language']}->{r['target_language']}  kind={r['translation_kind']}  engine={r['engine']}({r.get('model_id') or '-'})  draft={r['draft_source']}/edited={r['draft_edited']}")
            if badges:
                print(f"  Badges:   {', '.join(badges)}")
            if tags:
                print(f"  AutoTags: {', '.join(tags)}")
            print(f"  Source:   {r['source_text']}")
            print(f"  Orig:     {r['original_translation']}")
            print(f"  Sugg:     {r.get('suggested_translation')}")
            if diff_spans:
                print(f"  Diff:     {diff_spans[:4]}")
            if gl_hits:
                print(f"  Glossary: {[(g['source'] + ' => ' + g['target'] + ' [' + g['origin'] + ']') for g in gl_hits]}")
    return 0


# ---------------------------------------------------------------------------
# Subcommand: review (interactive) & apply (batch)
# ---------------------------------------------------------------------------

def validate_decision(decision: dict[str, Any]) -> dict[str, Any]:
    rid = str(decision.get("id", "")).strip()
    if not rid:
        raise ValueError("Decision missing 'id'")
    status = str(decision.get("review_status", "")).strip()
    if status not in VALID_REVIEW_STATUSES:
        raise ValueError(f"Invalid review_status '{status}' for {rid}")
    split = str(decision.get("split", "eval")).strip()
    if split not in VALID_SPLITS:
        raise ValueError(f"Invalid split '{split}' for {rid}")
    curated_tr = decision.get("curated_translation")
    if curated_tr is not None:
        curated_tr = str(curated_tr).strip() or None
    tags = [str(t).strip() for t in decision.get("failure_tags", []) if str(t).strip()]
    terms: list[dict[str, str]] = []
    for item in decision.get("confirmed_glossary_terms", []):
        src = str(item.get("source", "")).strip()
        tgt = str(item.get("target", "")).strip()
        if src and tgt:
            terms.append({"source": src, "target": tgt})
    return {
        "id": rid,
        "review_status": status,
        "curated_translation": curated_tr,
        "split": split,
        "failure_tags": tags,
        "confirmed_glossary_terms": terms,
        "reviewer_note": (str(decision.get("reviewer_note")).strip() or None) if decision.get("reviewer_note") else None,
        "reviewed_at": decision.get("reviewed_at") or utc_now_iso(),
    }


def parse_glossary_input(raw: str) -> list[dict[str, str]]:
    """Parse 'src => tgt; src2 => tgt2' interactive input."""
    terms: list[dict[str, str]] = []
    for chunk in re.split(r"[;\n]+", raw):
        chunk = chunk.strip()
        if not chunk:
            continue
        sep = "=>" if "=>" in chunk else ("=" if "=" in chunk else None)
        if not sep:
            continue
        src, tgt = chunk.split(sep, 1)
        src, tgt = src.strip(), tgt.strip()
        if src and tgt:
            terms.append({"source": src, "target": tgt})
    return terms


def cmd_review(args: argparse.Namespace) -> int:
    rows = load_reports(args.work_dir)
    curated = load_curation_log(args.work_dir)
    lookup = GlossaryLookup(args.glossary_json)
    dup_counts = Counter(text_hash(r["source_language"], r["target_language"], r["source_text"]) for r in rows)

    queue = [
        r for r in rows
        if (args.all or (r["id"] not in curated and r.get("review_status") == "pending"))
    ]
    if not queue:
        print("No reports pending review.")
        return 0

    print(f"Starting interactive review for {len(queue)} reports. (Ctrl+C or 'q' to quit safely)\n")
    for idx, r in enumerate(queue, 1):
        badges = detect_badges(r, dup_counts)
        gl_cands = suggest_glossary_candidates(r, lookup)
        auto_tags = suggest_failure_tags(r, gl_cands)
        diff_spans = extract_diff_spans(r["original_translation"], r.get("suggested_translation") or "")

        print("=" * 72)
        print(f"[{idx}/{len(queue)}] ID: {r['id']}  |  {r['source_language']} -> {r['target_language']}  |  kind: {r['translation_kind']}")
        print(f"Engine: {r['engine']} ({r.get('model_id') or '-'})  |  App: {r['app_version']}  |  Draft: {r['draft_source']} (edited={r['draft_edited']})")
        if r.get("npc_speaker"):
            print(f"NPC Speaker: {r['npc_speaker']}")
        if badges:
            print(f"Badges: {', '.join(badges)}")
        print(f"\n  Source:    {r['source_text']}")
        print(f"  Original:  {r['original_translation']}")
        print(f"  Suggested: {r.get('suggested_translation')}")
        if r.get("comment"):
            print(f"  Comment:   {r['comment']}")
        if diff_spans:
            print(f"  Diff:      {diff_spans[:4]}")
        if gl_cands:
            print("  Glossary suggestions:")
            for g in gl_cands:
                print(f"    - {g['source']} => {g['target']}  ({g['origin']})")

        action = input("\nDecision [a=approve / r=reject / s=skip / q=quit]: ").strip().lower()
        if action == "q":
            break
        if action in ("s", ""):
            continue
        if action not in ("a", "r"):
            print("Unknown choice, skipping.")
            continue

        status = "approved" if action == "a" else "rejected"
        curated_tr = None
        default_split = "reference_only" if is_reference_only_candidate(r) else args.default_split
        split = default_split
        tags = auto_tags
        confirmed_terms: list[dict[str, str]] = []

        if status == "approved":
            tr_in = input("Curated translation (Enter = keep Suggested): ").strip()
            if tr_in:
                curated_tr = tr_in
            split_in = input(f"Split [eval/train/reference_only] (default={default_split}): ").strip().lower()
            if split_in in VALID_SPLITS:
                split = split_in
            tags_in = input(f"Failure tags comma-separated (default={','.join(auto_tags) or 'none'}): ").strip()
            if tags_in:
                tags = [t.strip() for t in tags_in.split(",") if t.strip()]
            default_gl = "; ".join(f"{g['source']} => {g['target']}" for g in gl_cands)
            gl_in = input(f"Glossary terms 'src => tgt; ...' (Enter=accept hints, '-'=none) [{default_gl}]: ").strip()
            if gl_in == "-":
                confirmed_terms = []
            elif gl_in == "":
                confirmed_terms = [{"source": g["source"], "target": g["target"]} for g in gl_cands]
            else:
                confirmed_terms = parse_glossary_input(gl_in)

        note = input("Reviewer note (optional): ").strip() or None
        dec = validate_decision(
            {
                "id": r["id"],
                "review_status": status,
                "curated_translation": curated_tr,
                "split": split,
                "failure_tags": tags,
                "confirmed_glossary_terms": confirmed_terms,
                "reviewer_note": note,
            }
        )
        append_curation_decision(args.work_dir, dec)
        print(f"Saved: {r['id']} -> {status} (split={split}, glossary={len(confirmed_terms)})\n")

    return 0


def cmd_apply(args: argparse.Namespace) -> int:
    rows_by_id = {r["id"]: r for r in load_reports(args.work_dir)}
    raw_text = Path(args.decisions_file).read_text(encoding="utf-8").strip()
    if raw_text.startswith("["):
        items = json.loads(raw_text)
    else:
        items = [json.loads(line) for line in raw_text.splitlines() if line.strip()]

    applied = 0
    for item in items:
        dec = validate_decision(item)
        if dec["id"] not in rows_by_id:
            raise ValueError(f"Decision references unknown report id: {dec['id']}")
        append_curation_decision(args.work_dir, dec)
        applied += 1
    print(f"Applied {applied} curation decisions to {args.work_dir / 'curated' / 'curation_log.jsonl'}")
    return 0


# ---------------------------------------------------------------------------
# Subcommand: sync-status
# ---------------------------------------------------------------------------

def build_sync_sql(
    rows: Sequence[dict[str, Any]],
    curated: dict[str, dict[str, Any]],
) -> list[str]:
    remote_status = {r["id"]: r.get("review_status") for r in rows}
    statements: list[str] = []
    for rid, dec in sorted(curated.items()):
        target_status = dec["review_status"]
        if target_status not in VALID_REVIEW_STATUSES:
            raise ValueError(f"Unsafe review_status: {target_status}")
        if not RE_UUID.match(rid):
            raise ValueError(f"Unsafe non-UUID id: {rid}")
        if rid not in remote_status or remote_status[rid] == target_status:
            continue
        statements.append(
            f"UPDATE translation_reports SET review_status = '{target_status}' WHERE id = '{rid}';"
        )
    return statements


def cmd_sync_status(args: argparse.Namespace) -> int:
    rows = load_reports(args.work_dir)
    curated = load_curation_log(args.work_dir)
    statements = build_sync_sql(rows, curated)
    if not statements:
        print("Remote D1 review_status is already up to date with local curation_log.jsonl.")
        return 0

    sql_body = "\n".join(statements) + "\n"
    if not args.execute:
        print(f"[DRY RUN] {len(statements)} UPDATE statement(s) ready (pass --execute to apply to D1):\n")
        print(sql_body)
        return 0

    with tempfile.NamedTemporaryFile("w", suffix=".sql", encoding="utf-8", delete=False) as tmp:
        tmp.write(sql_body)
        sql_path = Path(tmp.name)
    try:
        cmd = [
            "npx",
            "wrangler",
            "d1",
            "execute",
            args.db_name,
            "--remote",
            "--yes",
            "--file",
            str(sql_path),
        ]
        proc = subprocess.run(
            cmd,
            cwd=str(args.endpoint_dir),
            capture_output=True,
            text=True,
            encoding="utf-8",
            shell=(sys.platform == "win32"),
        )
        if proc.returncode != 0:
            sys.stderr.write(proc.stderr or proc.stdout)
            return proc.returncode
        print(f"Synced {len(statements)} review_status updates to D1 ({args.db_name}).")
        return 0
    finally:
        sql_path.unlink(missing_ok=True)


# ---------------------------------------------------------------------------
# Subcommand: export
# ---------------------------------------------------------------------------

def export_datasets(
    rows: Sequence[dict[str, Any]],
    curated: dict[str, dict[str, Any]],
    *,
    version: str,
) -> dict[str, Any]:
    """Build deterministic eval, sft, cpo, reference_only, overrides, and summary payloads."""
    merged: list[dict[str, Any]] = []
    for r in rows:
        dec = curated.get(r["id"])
        if not dec:
            continue
        if dec["review_status"] != "approved":
            continue
        final_tr = dec.get("curated_translation") or r.get("suggested_translation")
        final_tr = final_tr.strip() if isinstance(final_tr, str) and final_tr.strip() else None
        merged.append(
            {
                **r,
                "review_status": "approved",
                "final_translation": final_tr,
                "curated_translation": dec.get("curated_translation"),
                "split": dec["split"],
                "failure_tags": dec.get("failure_tags", []),
                "confirmed_glossary_terms": dec.get("confirmed_glossary_terms", []),
                "reviewer_note": dec.get("reviewer_note"),
                "reviewed_at": dec.get("reviewed_at", ""),
                "source_hash": text_hash(r["source_language"], r["target_language"], r["source_text"]),
            }
        )

    # Sort chronologically so latest overwrites earlier on dedup
    merged.sort(key=lambda x: (x.get("reviewed_at", ""), x.get("created_at", ""), x["id"]))

    # 1. Fixed Hold-out eval.jsonl (highest isolation priority)
    eval_by_hash: dict[str, dict[str, Any]] = {}
    eval_ids: set[str] = set()
    for item in merged:
        if item["split"] != "eval" or not item["final_translation"]:
            continue
        eval_ids.add(item["id"])
        eval_by_hash[item["source_hash"]] = {
            "id": item["id"],
            "source_hash": item["source_hash"],
            "translation_kind": item["translation_kind"],
            "npc_speaker": item.get("npc_speaker"),
            "source_language": item["source_language"],
            "target_language": item["target_language"],
            "source_text": item["source_text"],
            "reference_translation": item["final_translation"],
            "baseline_engine": item["engine"],
            "baseline_model_id": item.get("model_id"),
            "baseline_model_sha256": item.get("model_sha256"),
            "baseline_output": item["original_translation"],
            "failure_tags": item["failure_tags"],
            "glossary_terms": item["confirmed_glossary_terms"],
        }
    eval_hashes = set(eval_by_hash.keys())

    # 2. reference_only.jsonl (unmodified external MT drafts or explicitly tagged reference_only)
    ref_by_hash: dict[str, dict[str, Any]] = {}
    for item in merged:
        if item["split"] == "reference_only" or is_reference_only_candidate(item):
            if not item["final_translation"]:
                continue
            ref_by_hash[item["source_hash"]] = {
                "id": item["id"],
                "source_hash": item["source_hash"],
                "translation_kind": item["translation_kind"],
                "source_language": item["source_language"],
                "target_language": item["target_language"],
                "source_text": item["source_text"],
                "engine": item["engine"],
                "model_id": item.get("model_id"),
                "original_translation": item["original_translation"],
                "reference_translation": item["final_translation"],
                "draft_source": item["draft_source"],
                "draft_edited": item["draft_edited"],
                "glossary_terms": item["confirmed_glossary_terms"],
            }

    # 3. sft_user.jsonl (strictly isolated from eval_ids/eval_hashes and unmodified external MT)
    sft_by_hash: dict[str, dict[str, Any]] = {}
    for item in merged:
        if item["split"] != "train" or not item["final_translation"]:
            continue
        if is_reference_only_candidate(item):
            continue
        if item["id"] in eval_ids or item["source_hash"] in eval_hashes:
            continue
        sft_by_hash[item["source_hash"]] = {
            "id": item["id"],
            "source_hash": item["source_hash"],
            "translation_kind": item["translation_kind"],
            "npc_speaker": item.get("npc_speaker"),
            "source_language": item["source_language"],
            "target_language": item["target_language"],
            "source_text": item["source_text"],
            "target_text": item["final_translation"],
            "failure_tags": item["failure_tags"],
            "glossary_terms": item["confirmed_glossary_terms"],
        }

    # 4. cpo_user.jsonl (separate dedup key including model_sha256 so Papago never hides MiLMMT)
    cpo_by_key: dict[tuple[str, str], dict[str, Any]] = {}
    for item in merged:
        if item["split"] != "train" or not item["final_translation"]:
            continue
        if is_reference_only_candidate(item):
            continue
        if item["engine"] != "MiLMMT":
            continue
        if normalize_text(item["final_translation"]) == normalize_text(item["original_translation"]):
            continue
        if item["id"] in eval_ids or item["source_hash"] in eval_hashes:
            continue
        dedup_key = (str(item.get("model_sha256") or "unknown"), item["source_hash"])
        cpo_by_key[dedup_key] = {
            "id": item["id"],
            "source_hash": item["source_hash"],
            "translation_kind": item["translation_kind"],
            "npc_speaker": item.get("npc_speaker"),
            "source_language": item["source_language"],
            "target_language": item["target_language"],
            "source_text": item["source_text"],
            "chosen": item["final_translation"],
            "rejected": item["original_translation"],
            "model_id": item.get("model_id"),
            "model_sha256": item.get("model_sha256"),
            "failure_tags": item["failure_tags"],
            "glossary_terms": item["confirmed_glossary_terms"],
        }

    # 5. Tier-0 glossary overrides (from all approved reports, including eval & reference_only)
    glossary_pairs: dict[str, dict[str, str]] = defaultdict(dict)
    glossary_freq: Counter[tuple[str, str, str]] = Counter()
    for item in merged:
        pair_key = f"{item['source_language']}->{item['target_language']}"
        for g in item["confirmed_glossary_terms"]:
            src = normalize_text(g["source"])
            tgt = normalize_text(g["target"])
            if src and tgt:
                glossary_pairs[pair_key][src] = tgt
                glossary_freq[(pair_key, src, tgt)] += 1

    overrides_payload = {
        "version": version,
        "pairs": {
            pair_key: [
                {"source": src, "target": tgt}
                for src, tgt in sorted(mapping.items(), key=lambda kv: (-len(kv[0]), kv[0]))
            ]
            for pair_key, mapping in sorted(glossary_pairs.items())
        },
    }

    eval_records = list(eval_by_hash.values())
    sft_records = list(sft_by_hash.values())
    cpo_records = list(cpo_by_key.values())
    ref_records = list(ref_by_hash.values())

    # Leakage verification invariant
    train_hashes = {x["source_hash"] for x in sft_records} | {x["source_hash"] for x in cpo_records}
    leakage_count = len(eval_hashes & train_hashes)
    assert leakage_count == 0, f"Eval/train data leakage detected: {leakage_count}"

    summary = {
        "version": version,
        "exported_at": utc_now_iso(),
        "approved_total": len(merged),
        "counts": {
            "eval": len(eval_records),
            "sft_user": len(sft_records),
            "cpo_user": len(cpo_records),
            "reference_only": len(ref_records),
            "glossary_override_terms": sum(len(v) for v in overrides_payload["pairs"].values()),
        },
        "eval_train_overlap": leakage_count,
        "failure_tag_counts": dict(Counter(t for m in merged for t in m["failure_tags"]).most_common()),
        "eval_by_lang_pair": dict(Counter(f"{x['source_language']}->{x['target_language']}" for x in eval_records)),
    }

    return {
        "eval": eval_records,
        "sft_user": sft_records,
        "cpo_user": cpo_records,
        "reference_only": ref_records,
        "overrides": overrides_payload,
        "summary": summary,
    }


def write_jsonl(path: Path, records: Sequence[dict[str, Any]]) -> None:
    content = "\n".join(json.dumps(r, ensure_ascii=False) for r in records)
    if content:
        content += "\n"
    path.write_text(content, encoding="utf-8")


def cmd_export(args: argparse.Namespace) -> int:
    rows = load_reports(args.work_dir)
    curated = load_curation_log(args.work_dir)
    result = export_datasets(rows, curated, version=args.version)

    out_dir = args.out_dir or (args.work_dir / "exports" / args.version)
    out_dir.mkdir(parents=True, exist_ok=True)
    write_jsonl(out_dir / "eval.jsonl", result["eval"])
    write_jsonl(out_dir / "sft_user.jsonl", result["sft_user"])
    write_jsonl(out_dir / "cpo_user.jsonl", result["cpo_user"])
    write_jsonl(out_dir / "reference_only.jsonl", result["reference_only"])
    (out_dir / "overrides.json").write_text(
        json.dumps(result["overrides"], ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (out_dir / "export_summary.json").write_text(
        json.dumps(result["summary"], ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )

    c = result["summary"]["counts"]
    print(
        f"Exported {args.version} -> {out_dir}\n"
        f"  eval={c['eval']}, sft_user={c['sft_user']}, cpo_user={c['cpo_user']}, "
        f"reference_only={c['reference_only']}, overrides={c['glossary_override_terms']} "
        f"(eval_train_overlap={result['summary']['eval_train_overlap']})"
    )
    return 0


# ---------------------------------------------------------------------------
# CLI entrypoint
# ---------------------------------------------------------------------------

def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Curate and export D1 translation reports for MiLMMT.")
    parser.add_argument(
        "--work-dir",
        type=Path,
        default=Path("artifacts/curation"),
        help="Local gitignored working directory (default: artifacts/curation).",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    p_pull = sub.add_parser("pull", help="Pull translation_reports from Cloudflare D1 or import JSON.")
    p_pull.add_argument("--endpoint-dir", type=Path, default=Path(r"D:\REPO\ironworks-db-endpoint"))
    p_pull.add_argument("--db-name", default="ironworks-reports")
    p_pull.add_argument("--pending-only", action="store_true")
    p_pull.add_argument("--from-json", type=Path, help="Import from previously captured wrangler JSON output.")
    p_pull.set_defaults(func=cmd_pull)

    p_sum = sub.add_parser("summary", help="Show queue summary and optional item-by-item hints.")
    p_sum.add_argument("--glossary-json", type=Path, default=Path("artifacts/glossary/glossary.full.json"))
    p_sum.add_argument("--verbose", action="store_true")
    p_sum.set_defaults(func=cmd_summary)

    p_rev = sub.add_parser("review", help="Interactively review pending reports.")
    p_rev.add_argument("--glossary-json", type=Path, default=Path("artifacts/glossary/glossary.full.json"))
    p_rev.add_argument("--default-split", choices=VALID_SPLITS, default="eval")
    p_rev.add_argument("--all", action="store_true", help="Include already curated/reviewed reports.")
    p_rev.set_defaults(func=cmd_review)

    p_apply = sub.add_parser("apply", help="Apply batch review decisions from JSON or JSONL.")
    p_apply.add_argument("--decisions-file", type=Path, required=True)
    p_apply.set_defaults(func=cmd_apply)

    p_sync = sub.add_parser("sync-status", help="Generate or execute D1 review_status UPDATE SQL.")
    p_sync.add_argument("--endpoint-dir", type=Path, default=Path(r"D:\REPO\ironworks-db-endpoint"))
    p_sync.add_argument("--db-name", default="ironworks-reports")
    p_sync.add_argument("--execute", action="store_true", help="Actually apply UPDATE statements to remote D1.")
    p_sync.set_defaults(func=cmd_sync_status)

    p_exp = sub.add_parser("export", help="Export eval, sft_user, cpo_user, reference_only, and overrides.json.")
    p_exp.add_argument("--version", default=dt.date.today().strftime("%Y-%m-%d.1"))
    p_exp.add_argument("--out-dir", type=Path)
    p_exp.set_defaults(func=cmd_export)

    args = parser.parse_args(argv)
    return int(args.func(args))


if __name__ == "__main__":
    raise SystemExit(main())
