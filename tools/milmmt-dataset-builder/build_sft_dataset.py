#!/usr/bin/env python3
"""Build asymmetric multilingual Glossary-SFT datasets for MiLMMT-46-1B/4B-v1.0.

Zero external dependencies (Python 3.10+ standard library only).

Confirmed tokenizer contract (verified on MiLMMT-46-1B-v1.0.Q8_0.gguf):
  - template: empty, add_special_tokens: false (no <bos>)
  - Prompt ends directly with `{TargetName}:`
  - Output starts immediately with target text (NO leading space) and ends with `<eos>` (id=1)

Design:
  - Non-synthetic 1st pass (human-localized ffxiv-datamining + cleaned Koala chat + optional replay)
  - Asymmetric multilingual mix: JA->KO 50%, EN->KO 30%, DE->KO 5%, FR->KO 5%, KO->JA 5%, KO->EN 5%
  - Axis 1 (55%): FFXIV Glossary-Conditioned (65% matched / 20% empty / 15% distractor;
                  20% of matched use Counterfactual Glossary Swapping to enforce glossary obedience)
  - Axis 2 (25%): Short colloquial / real game chat (Balloon, NpcYell, Completion, Koala 🟡소통)
  - Axis 3 (20%): General On-policy Replay (optional --replay-jsonl, no glossary block)
  - Hard data-leakage barrier against eval.jsonl (exact normalized hash + 5-gram Jaccard >= 0.85)
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import hashlib
import json
import random
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET
import zipfile
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence

# Share the exact 8-way Korean speech-style classifier with build_glossary.py
_GLOSSARY_BUILDER_DIR = Path(__file__).resolve().parent.parent / "milmmt-glossary-builder"
if str(_GLOSSARY_BUILDER_DIR) not in sys.path:
    sys.path.insert(0, str(_GLOSSARY_BUILDER_DIR))
from korean_style import (  # noqa: E402
    KOREAN_SPEECH_STYLES,
    classify_korean_sentence_style,
)

LANG_CODE_TO_NAME: dict[str, str] = {
    "ja": "Japanese",
    "en": "English",
    "de": "German",
    "fr": "French",
    "ko": "Korean",
}
NAME_TO_LANG_CODE: dict[str, str] = {v: k for k, v in LANG_CODE_TO_NAME.items()}

# Asymmetric multilingual target weights
PAIR_WEIGHTS: dict[tuple[str, str], float] = {
    ("ja", "ko"): 0.50,
    ("en", "ko"): 0.30,
    ("de", "ko"): 0.05,
    ("fr", "ko"): 0.05,
    ("ko", "ja"): 0.05,
    ("ko", "en"): 0.05,
}

# Benign SE-String inline formatting tags that can be stripped cleanly
RE_BENIGN_SE_TAG = re.compile(
    r"</?(?:Emphasis|Highlight|Italic|Indent|SoftHyphen|UIForeground|UIGlow)[^>]*>",
    re.IGNORECASE,
)
RE_ANY_SE_TAG = re.compile(r"<[^>]{1,120}>")
RE_ITALIC_WRAP = re.compile(r"^\*([^*]+)\*$")
RE_HANGUL = re.compile(r"[\uAC00-\uD7A3]")
RE_KANA = re.compile(r"[\u3040-\u309F\u30A0-\u30FF]")
RE_LATIN = re.compile(r"[A-Za-zÀ-ÖØ-öø-ÿ]")
RE_DUMMY = re.compile(
    r"(?:テスト|ダミー|未使用|無効値|開発用|削除予定|中国版専用|\*{3,}|^TBD$|^TODO$|\bUNKNOWN\b|\bPlayer\s+Player\b)",
    re.IGNORECASE,
)
RE_DANGLING_JA_PARTICLE = re.compile(r"^[はがのをにでへと、。]")
RE_DANGLING_KO_PARTICLE = re.compile(r"^(?:은|는|이|가|을|를|의|에서|에게|으로|로|와|과)(?:\s|$)")
RE_REPEAT_RUN = re.compile(r"(.)\1{4,}")
RE_ONLY_SYMBOLS = re.compile(r"^[\W\d_]+$")
RE_BRACKET_WRAP = re.compile(r"^【(.+)】$")
RE_INLINE_SPEAKER_PREFIX = re.compile(r"^\(-[^)]+-\)\s*")

# Axis 4 (NPC Speech-Style) language weights: heavily weighted toward Latin->KO (runtime target)
# while retaining JA->KO for cross-lingual richness on rare Korean registers.
STYLE_PAIR_WEIGHTS: dict[tuple[str, str], float] = {
    ("en", "ko"): 0.55,
    ("ja", "ko"): 0.25,
    ("de", "ko"): 0.10,
    ("fr", "ko"): 0.10,
}

# Styles that must only be emitted when explicitly requested via `Style:` (prevents unprompted tone bleed)
STYLE_GATED_ONLY: frozenset[str] = frozenset({"애교 존댓말", "고어체", "하오체"})


# ---------------------------------------------------------------------------
# Prompt rendering & normalization
# ---------------------------------------------------------------------------

def normalize_text(text: str) -> str:
    text = unicodedata.normalize("NFKC", text)
    return re.sub(r"\s+", " ", text).strip()


def pair_hash(source_lang: str, target_lang: str, source_text: str) -> str:
    src_name = LANG_CODE_TO_NAME.get(source_lang, source_lang)
    dst_name = LANG_CODE_TO_NAME.get(target_lang, target_lang)
    key = f"{src_name}\u0000{dst_name}\u0000{normalize_text(source_text)}"
    return hashlib.sha256(key.encode("utf-8")).hexdigest()


def char_ngrams(text: str, n: int = 5) -> set[str]:
    norm = normalize_text(text).casefold()
    if len(norm) < n:
        return {norm} if norm else set()
    return {norm[i : i + n] for i in range(len(norm) - n + 1)}


def jaccard(a: set[str], b: set[str]) -> float:
    if not a or not b:
        return 0.0
    inter = len(a & b)
    if inter == 0:
        return 0.0
    return inter / len(a | b)


def render_prompt(
    source_language: str,
    target_language: str,
    text: str,
    glossary_terms: Sequence[tuple[str, str]] = (),
    style: str | None = None,
) -> str:
    """Render exact MiLMMT prompt (verified byte-for-byte against MiLMMTTranslator.RenderPrompt)."""
    src_name = LANG_CODE_TO_NAME.get(source_language, source_language)
    dst_name = LANG_CODE_TO_NAME.get(target_language, target_language)
    trimmed = text.strip()
    core = (
        f"Translate this from {src_name} to {dst_name}:\n"
        f"{src_name}: {trimmed}\n"
        f"{dst_name}:"
    )
    header_parts: list[str] = []
    if style and style.strip():
        header_parts.append(f"Style: {style.strip()}")
    if glossary_terms:
        gl_lines = "\n".join(f"{s} => {t}" for s, t in glossary_terms)
        header_parts.append(f"Glossary:\n{gl_lines}")
    if not header_parts:
        return core
    return "\n".join(header_parts) + f"\n\n{core}"


def clean_dialogue_text(raw: str) -> str | None:
    """Clean FFXIV EXD dialogue/system line; returns None if complex tags or noise remain."""
    if not raw or not raw.strip():
        return None
    text = raw.replace("\r\n", " ").replace("\n", " ")
    text = RE_INLINE_SPEAKER_PREFIX.sub("", text)
    text = RE_BENIGN_SE_TAG.sub("", text)
    if RE_ANY_SE_TAG.search(text):
        return None
    text = normalize_text(text)
    m_br = RE_BRACKET_WRAP.match(text)
    if m_br:
        text = m_br.group(1).strip()
    m_it = RE_ITALIC_WRAP.match(text)
    if m_it:
        text = m_it.group(1).strip()
    if not text or len(text) < 2 or len(text) > 260:
        return None
    if RE_ONLY_SYMBOLS.match(text) or RE_DUMMY.search(text) or RE_REPEAT_RUN.search(text):
        return None
    if RE_DANGLING_JA_PARTICLE.match(text) or RE_DANGLING_KO_PARTICLE.match(text):
        return None
    # Reject oxidizer literal "Player" placeholder in any language
    if re.search(r"\bPlayer\b", text):
        return None
    return text


def is_valid_parallel(src_lang: str, dst_lang: str, src: str, dst: str) -> bool:
    if not src or not dst:
        return False
    if src.casefold() == dst.casefold():
        return False
    # Length ratio guard (prevents severe misalignments or truncated translations)
    ratio = len(dst) / max(len(src), 1)
    if ratio < 0.25 or ratio > 4.0:
        return False
    if dst_lang == "ko":
        if not RE_HANGUL.search(dst) or RE_KANA.search(dst):
            return False
    elif dst_lang == "ja":
        if RE_HANGUL.search(dst) or not (RE_KANA.search(dst) or re.search(r"[\u4E00-\u9FFF]", dst)):
            return False
    elif dst_lang == "en":
        if RE_HANGUL.search(dst) or RE_KANA.search(dst) or not RE_LATIN.search(dst):
            return False

    if src_lang in ("en", "de", "fr") and (RE_KANA.search(src) or RE_HANGUL.search(src)):
        return False
    if src_lang == "ja" and RE_HANGUL.search(src):
        return False
    if src_lang == "ko" and (not RE_HANGUL.search(src) or RE_KANA.search(src)):
        return False
    return True


# ---------------------------------------------------------------------------
# Boundary-safe glossary matcher + counterfactual swapper
# ---------------------------------------------------------------------------

def _is_katakana(ch: str) -> bool:
    return ("\u30A0" <= ch <= "\u30FF") or ch == "\u30FC"


class FastGlossaryMatcher:
    """Fast length-filtered, boundary-safe glossary matcher for SFT construction."""

    def __init__(self, glossary_path: Path | None, max_terms_per_pair: int = 25000) -> None:
        # pair -> list of (source, target, category, tier)
        self.terms_by_pair: dict[tuple[str, str], list[tuple[str, str, str, int]]] = defaultdict(list)
        # First-2-char index for fast lookup without scanning 100k terms per sentence
        self._index: dict[tuple[str, str], dict[str, list[tuple[str, str, str, int]]]] = defaultdict(
            lambda: defaultdict(list)
        )
        # Pool of same-category targets for counterfactual swapping
        self.swap_pool: dict[tuple[str, str, str], list[str]] = defaultdict(list)
        # Global <-> KR divergence swaps loaded optionally from community_curated.json
        self.divergence_swaps: dict[tuple[str, str, str], str] = {}

        if not glossary_path or not glossary_path.exists():
            return

        data = json.loads(glossary_path.read_text(encoding="utf-8"))
        for pair_key, items in data.get("pairs", {}).items():
            if "->" not in pair_key:
                continue
            src_name, dst_name = pair_key.split("->", 1)
            src_code = NAME_TO_LANG_CODE.get(src_name)
            dst_code = NAME_TO_LANG_CODE.get(dst_name)
            if not src_code or not dst_code:
                continue

            # Prioritize Tier 0-3 first, then concise Tier 4 entities (PlaceName, ClassJob, Duty, Status, Item, Action, NPC)
            prioritized = sorted(
                items,
                key=lambda x: (int(x.get("tier", 3)), -len(x["source"]), x["source"]),
            )
            kept = 0
            for item in prioritized:
                src = item["source"]
                tgt = item["target"]
                tier = int(item.get("tier", 3))
                cat = str(item.get("category", "general"))
                # Skip very short ambiguous terms for SFT matching
                min_len = 2 if src_code in ("ja", "ko") else 4
                if len(src) < min_len or len(tgt) < 2:
                    continue
                rec = (src, tgt, cat, tier)
                self.terms_by_pair[(src_code, dst_code)].append(rec)
                prefix = (src[:2].casefold() if src_code in ("en", "de", "fr") else src[:2])
                self._index[(src_code, dst_code)][prefix].append(rec)
                if cat in ("place_name", "class_job", "duty", "npc_name", "status") and 2 <= len(tgt) <= 14:
                    self.swap_pool[(src_code, dst_code, cat)].append(tgt)
                kept += 1
                if kept >= max_terms_per_pair:
                    break

            # Also build reverse index (ko -> ja/en) from Tier 0-2 core terms
            if dst_code == "ko" and src_code in ("ja", "en"):
                for src, tgt, cat, tier in self.terms_by_pair[(src_code, dst_code)]:
                    if tier <= 2 and len(tgt) >= 2 and len(src) >= 2:
                        rrec = (tgt, src, cat, tier)
                        self.terms_by_pair[("ko", src_code)].append(rrec)
                        self._index[("ko", src_code)][tgt[:2]].append(rrec)

        for pair_key, bucket_map in self._index.items():
            for pref in bucket_map:
                bucket_map[pref].sort(key=lambda r: (-len(r[0]), r[3]))

    def load_divergence_swaps(self, curated_path: Path | None) -> None:
        if not curated_path or not curated_path.exists():
            return
        data = json.loads(curated_path.read_text(encoding="utf-8"))
        for pair_key, items in data.get("pairs", {}).items():
            if "->" not in pair_key:
                continue
            src_name, dst_name = pair_key.split("->", 1)
            sc = NAME_TO_LANG_CODE.get(src_name)
            dc = NAME_TO_LANG_CODE.get(dst_name)
            if not sc or not dc:
                continue
            for item in items:
                kr_off = item.get("kr_official")
                if kr_off and kr_off != item["target"]:
                    # Map official -> global transliteration AND vice versa
                    self.divergence_swaps[(sc, dc, kr_off)] = item["target"]
                    self.divergence_swaps[(sc, dc, item["target"])] = kr_off

    def find_verified_matches(
        self,
        src_lang: str,
        dst_lang: str,
        source_text: str,
        target_text: str,
        limit: int = 8,
    ) -> list[tuple[str, str, str]]:
        """Return [(source_term, target_term, category)] present in BOTH source_text and target_text."""
        bucket_map = self._index.get((src_lang, dst_lang))
        if not bucket_map or len(source_text) < 2:
            return []

        is_latin = src_lang in ("en", "de", "fr")
        hay = source_text.casefold() if is_latin else source_text
        tgt_hay = target_text.casefold() if dst_lang in ("en", "de", "fr") else target_text

        # Gather unique candidate records whose 2-char prefix occurs in haystack
        seen_prefixes: set[str] = set()
        candidates: list[tuple[str, str, str, int]] = []
        for i in range(len(hay) - 1):
            pref = hay[i : i + 2]
            if pref in seen_prefixes:
                continue
            seen_prefixes.add(pref)
            if pref in bucket_map:
                candidates.extend(bucket_map[pref])
        if not candidates:
            return []

        candidates.sort(key=lambda r: (-len(r[0]), r[3]))
        matches: list[tuple[str, str, str]] = []
        used_spans: list[tuple[int, int]] = []
        used_sources: set[str] = set()

        for term_src, term_tgt, cat, _ in candidates:
            needle = term_src.casefold() if is_latin else term_src
            if needle in used_sources:
                continue
            tgt_needle = term_tgt.casefold() if dst_lang in ("en", "de", "fr") else term_tgt
            if tgt_needle not in tgt_hay:
                continue

            start_pos = 0
            found_span = None
            while True:
                idx = hay.find(needle, start_pos)
                if idx < 0:
                    break
                end = idx + len(needle)
                start_pos = idx + 1
                if is_latin:
                    if idx > 0 and hay[idx - 1].isalnum():
                        continue
                    if end < len(hay) and hay[end].isalnum():
                        continue
                elif src_lang == "ja":
                    if _is_katakana(needle[0]) and idx > 0 and _is_katakana(hay[idx - 1]):
                        continue
                    if _is_katakana(needle[-1]) and end < len(hay) and _is_katakana(hay[end]):
                        continue
                if any(not (end <= s or idx >= e) for s, e in used_spans):
                    continue
                found_span = (idx, end)
                break

            if found_span:
                used_spans.append(found_span)
                used_sources.add(needle)
                matches.append((term_src, term_tgt, cat))
                if len(matches) >= limit:
                    break
        return matches

    def sample_distractors(
        self,
        src_lang: str,
        dst_lang: str,
        source_text: str,
        target_text: str,
        rng: random.Random,
        count: int,
    ) -> list[tuple[str, str]]:
        pool = self.terms_by_pair.get((src_lang, dst_lang), [])
        if not pool or count <= 0:
            return []
        out: list[tuple[str, str]] = []
        src_low = source_text.casefold()
        tgt_low = target_text.casefold()
        for _ in range(count * 8):
            if len(out) >= count:
                break
            s, t, _, _ = rng.choice(pool)
            if s.casefold() not in src_low and t.casefold() not in tgt_low and all(x[0] != s for x in out):
                out.append((s, t))
        return out

    def apply_counterfactual_swap(
        self,
        src_lang: str,
        dst_lang: str,
        target_text: str,
        matches: list[tuple[str, str, str]],
        rng: random.Random,
    ) -> tuple[str, list[tuple[str, str]], bool]:
        """Swap one matched term's target both in glossary and in target_text to enforce glossary obedience."""
        swapped_terms = [(s, t) for s, t, _ in matches]
        for idx, (term_src, term_tgt, cat) in enumerate(matches):
            if target_text.count(term_tgt) != 1:
                continue
            # Prefer real global-vs-KR divergence swap if available
            alt = self.divergence_swaps.get((src_lang, dst_lang, term_tgt))
            if not alt:
                pool = self.swap_pool.get((src_lang, dst_lang, cat), [])
                if len(pool) >= 5:
                    for _ in range(6):
                        cand = rng.choice(pool)
                        if cand != term_tgt and cand not in target_text and abs(len(cand) - len(term_tgt)) <= 4:
                            alt = cand
                            break
            if alt and alt != term_tgt:
                new_target = target_text.replace(term_tgt, alt, 1)
                swapped_terms[idx] = (term_src, alt)
                return new_target, swapped_terms, True
        return target_text, swapped_terms, False


# ---------------------------------------------------------------------------
# Source loaders (ffxiv-datamining, Koala 🟡소통, Replay JSONL, Eval guard)
# ---------------------------------------------------------------------------

def resolve_lang_csv_dir(datamining_root: Path, lang: str) -> Path:
    direct = datamining_root / "csv" / lang
    nested = direct / "csv"
    if nested.is_dir() and (nested / "ClassJob.csv").exists():
        return nested
    if direct.is_dir():
        return direct
    raise FileNotFoundError(f"Language CSV dir not found for '{lang}' under {datamining_root}")


def load_aligned_sheet_rows(
    datamining_root: Path,
    sheet_rel_path: str,
    column_name: str,
    langs: Sequence[str],
) -> list[dict[str, str]]:
    """Load rows aligned by `#` ID across requested languages for one CSV sheet."""
    lang_maps: dict[str, dict[str, str]] = {}
    for lang in langs:
        csv_path = resolve_lang_csv_dir(datamining_root, lang) / sheet_rel_path
        if not csv_path.exists():
            return []
        col_map: dict[str, str] = {}
        with csv_path.open("r", encoding="utf-8", errors="replace", newline="") as handle:
            reader = csv.reader(handle)
            headers = next(reader, [])
            if column_name not in headers:
                return []
            ci = headers.index(column_name)
            for row in reader:
                if row and len(row) > ci and row[ci]:
                    col_map[row[0]] = row[ci]
        lang_maps[lang] = col_map

    ko_map = lang_maps.get("ko")
    if not ko_map:
        return []
    rows: list[dict[str, str]] = []
    for row_id, raw_ko in ko_map.items():
        clean_ko = clean_dialogue_text(raw_ko)
        if not clean_ko:
            continue
        item = {"id": row_id, "ko": clean_ko}
        for lang in langs:
            if lang == "ko":
                continue
            raw_v = lang_maps[lang].get(row_id, "")
            clean_v = clean_dialogue_text(raw_v) if raw_v else None
            if clean_v:
                item[lang] = clean_v
        if len(item) >= 3:
            rows.append(item)
    return rows


def load_quest_and_cutscene_pool(
    datamining_root: Path,
    langs: Sequence[str],
    rng: random.Random,
    max_files: int = 1200,
    max_lines_per_file: int = 14,
) -> list[dict[str, str]]:
    """Stratified sample of quest/ and cut_scene/ aligned dialogue lines."""
    ja_dir = resolve_lang_csv_dir(datamining_root, "ja")
    ko_dir = resolve_lang_csv_dir(datamining_root, "ko")
    rel_files: list[str] = []
    for sub in ("quest", "cut_scene", "custom"):
        sub_ja = ja_dir / sub
        sub_ko = ko_dir / sub
        if not (sub_ja.exists() and sub_ko.exists()):
            continue
        for p in sorted(sub_ja.rglob("*.csv")):
            rel = str(p.relative_to(ja_dir))
            if (ko_dir / rel).exists():
                rel_files.append(rel)

    rng.shuffle(rel_files)
    sampled_rows: list[dict[str, str]] = []
    for rel in rel_files[:max_files]:
        # Quest/cutscene CSVs have columns ['#', 'Key', 'Text'] (column index 2 or header 'Text')
        lang_maps: dict[str, dict[str, str]] = {}
        ok = True
        for lang in langs:
            fp = resolve_lang_csv_dir(datamining_root, lang) / rel
            if not fp.exists():
                ok = False
                break
            m: dict[str, str] = {}
            with fp.open("r", encoding="utf-8", errors="replace", newline="") as handle:
                reader = csv.reader(handle)
                hdr = next(reader, [])
                ci = hdr.index("Text") if "Text" in hdr else (2 if len(hdr) > 2 else 1)
                for row in reader:
                    if row and len(row) > ci and row[ci]:
                        m[row[0]] = row[ci]
            lang_maps[lang] = m
        if not ok:
            continue
        file_rows: list[dict[str, str]] = []
        for rid, raw_ko in lang_maps["ko"].items():
            ck = clean_dialogue_text(raw_ko)
            if not ck:
                continue
            rec = {"id": f"{rel}#{rid}", "ko": ck}
            for lang in langs:
                if lang == "ko":
                    continue
                cv = clean_dialogue_text(lang_maps[lang].get(rid, ""))
                if cv:
                    rec[lang] = cv
            if len(rec) >= 3:
                file_rows.append(rec)
        if len(file_rows) > max_lines_per_file:
            file_rows = rng.sample(file_rows, max_lines_per_file)
        sampled_rows.extend(file_rows)
    return sampled_rows


def load_style_balanced_dialogue_pool(
    datamining_root: Path,
    langs: Sequence[str],
    rng: random.Random,
    max_per_style_per_lang: int = 3000,
) -> dict[tuple[str, str, str], list[tuple[str, str, str]]]:
    """Scan all aligned quest/cutscene/custom dialogue files and bucket clean parallel lines by Korean style.

    Returns {(src_lang, 'ko', style): [(src_text, ko_text, origin), ...]}.
    Scanning all files ensures rare registers (애교 존댓말, 고어체, 하오체, 하게체) are captured completely.
    """
    ko_dir = resolve_lang_csv_dir(datamining_root, "ko")
    lang_dirs = {lang: resolve_lang_csv_dir(datamining_root, lang) for lang in langs}
    src_langs = [l for l in langs if l != "ko"]

    rel_files: list[str] = []
    for sub in ("quest", "cut_scene", "custom"):
        sub_ko = ko_dir / sub
        if not sub_ko.exists():
            continue
        for p in sorted(sub_ko.rglob("*.csv")):
            rel_files.append(str(p.relative_to(ko_dir)))
    rng.shuffle(rel_files)

    buckets: dict[tuple[str, str, str], list[tuple[str, str, str]]] = defaultdict(list)
    rare_styles = {"애교 존댓말", "고어체", "하오체", "하게체"}

    for rel in rel_files:
        ko_fp = ko_dir / rel
        ko_styled: dict[str, tuple[str, str]] = {}  # rid -> (clean_ko, style)
        with ko_fp.open("r", encoding="utf-8", errors="replace", newline="") as handle:
            reader = csv.reader(handle)
            hdr = next(reader, [])
            ci = hdr.index("Text") if "Text" in hdr else (2 if len(hdr) > 2 else 1)
            for row in reader:
                if not row or len(row) <= ci or not row[ci]:
                    continue
                if len(row) > 1 and ("_SEQ_" in row[1] or "_TODO_" in row[1]):
                    continue
                ck = clean_dialogue_text(row[ci])
                if not ck or len(ck) < 6:
                    continue
                st = classify_korean_sentence_style(ck)
                if not st:
                    continue
                # Stop collecting common styles once every source language bucket is saturated
                if st not in rare_styles and all(
                    len(buckets[(sl, "ko", st)]) >= max_per_style_per_lang for sl in src_langs
                ):
                    continue
                ko_styled[row[0]] = (ck, st)

        if not ko_styled:
            continue

        origin = f"style_{rel.split(chr(92))[0].split('/')[0]}"
        for sl in src_langs:
            src_fp = lang_dirs[sl] / rel
            if not src_fp.exists():
                continue
            with src_fp.open("r", encoding="utf-8", errors="replace", newline="") as handle:
                reader = csv.reader(handle)
                hdr = next(reader, [])
                ci = hdr.index("Text") if "Text" in hdr else (2 if len(hdr) > 2 else 1)
                for row in reader:
                    if not row or row[0] not in ko_styled or len(row) <= ci or not row[ci]:
                        continue
                    ck, st = ko_styled[row[0]]
                    bkey = (sl, "ko", st)
                    if st not in rare_styles and len(buckets[bkey]) >= max_per_style_per_lang:
                        continue
                    cs = clean_dialogue_text(row[ci])
                    if not cs or not is_valid_parallel(sl, "ko", cs, ck):
                        continue
                    buckets[bkey].append((cs, ck, origin))

    return buckets


def load_koala_chat_phrases(koala_xlsx: Path | None) -> list[tuple[str, str]]:
    """Extract clean JA->KO conversational lines from Koala's 🟡소통 sheet (filtering unprompted apologies)."""
    if not koala_xlsx or not koala_xlsx.exists():
        return []
    ns = {
        "a": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
        "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
    }
    pairs: list[tuple[str, str]] = []
    seen: set[tuple[str, str]] = set()
    with zipfile.ZipFile(koala_xlsx) as zf:
        shared = []
        if "xl/sharedStrings.xml" in zf.namelist():
            for si in ET.fromstring(zf.read("xl/sharedStrings.xml")).findall("a:si", ns):
                shared.append("".join(t.text or "" for t in si.findall(".//a:t", ns)))
        wb = ET.fromstring(zf.read("xl/workbook.xml"))
        rels = {el.attrib["Id"]: el.attrib["Target"] for el in ET.fromstring(zf.read("xl/_rels/workbook.xml.rels"))}
        target = None
        for sh in wb.findall(".//a:sheet", ns):
            if sh.attrib["name"] == "🟡소통":
                rid = sh.attrib["{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"]
                t = rels[rid]
                target = t if t.startswith("xl/") else "xl/" + t.lstrip("/")
                break
        if not target or target not in zf.namelist():
            return []

        sroot = ET.fromstring(zf.read(target))
        for row in sroot.findall(".//a:sheetData/a:row", ns):
            cells: dict[str, str] = {}
            for c in row.findall("a:c", ns):
                ref = c.attrib.get("r", "")
                col = "".join(ch for ch in ref if ch.isalpha())
                t = c.attrib.get("t")
                v = c.find("a:v", ns)
                is_el = c.find("a:is", ns)
                val = ""
                if t == "s" and v is not None and v.text:
                    val = shared[int(v.text)]
                elif t == "inlineStr" and is_el is not None:
                    val = "".join(x.text or "" for x in is_el.findall(".//a:t", ns))
                elif v is not None and v.text:
                    val = v.text
                if val.strip():
                    cells[col] = val.strip()

            for src_cols, tgt_col in ((("B", "C"), "D"), (("G",), "H"), (("I",), "J")):
                raw_tgt = cells.get(tgt_col, "")
                if not raw_tgt or "\n" in raw_tgt:
                    continue
                # Strip leading/trailing notes in parens
                tgt = normalize_text(re.sub(r"\([^)]{1,30}\)", "", raw_tgt))
                if not tgt or not RE_HANGUL.search(tgt) or "/" in tgt:
                    continue
                for sc in src_cols:
                    raw_src = cells.get(sc, "")
                    # Skip multiline compound cells that caused misalignment
                    if not raw_src or "\n" in raw_src or RE_HANGUL.search(raw_src):
                        continue
                    src = normalize_text(raw_src)
                    if not src or len(src) < 2 or not RE_KANA.search(src):
                        continue
                    # Drop over-translated unprompted apologies
                    if any(w in tgt for w in ("죄송", "미안")) and not any(
                        w in src for w in ("すみま", "すいま", "ごめん", "申し訳", "失礼", "すま")
                    ):
                        continue
                    if is_valid_parallel("ja", "ko", src, tgt) and (src, tgt) not in seen:
                        seen.add((src, tgt))
                        pairs.append((src, tgt))
    return pairs


def load_jsonl_pairs(path: Path | None) -> list[dict[str, str]]:
    if not path or not path.exists():
        return []
    out: list[dict[str, str]] = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        rec = json.loads(line)
        sl = NAME_TO_LANG_CODE.get(rec.get("source_language", ""), rec.get("source_language", ""))
        dl = NAME_TO_LANG_CODE.get(rec.get("target_language", ""), rec.get("target_language", ""))
        src = str(rec.get("source_text", "")).strip()
        dst = str(rec.get("target_text") or rec.get("reference_translation") or "").strip()
        if sl and dl and src and dst and is_valid_parallel(sl, dl, src, dst):
            out.append({"src_lang": sl, "dst_lang": dl, "source_text": src, "target_text": dst, "origin": str(rec.get("origin", path.name))})
    return out


class EvalLeakageGuard:
    """Blocks exact normalized hashes and high-similarity 3-gram matches against eval.jsonl."""

    def __init__(self, eval_jsonl: Path | None, jaccard_threshold: float = 0.70) -> None:
        self.exact_hashes: set[str] = set()
        self.eval_ngrams: list[tuple[str, str, set[str]]] = []
        self.threshold = jaccard_threshold
        if not eval_jsonl or not eval_jsonl.exists():
            return
        for line in eval_jsonl.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line:
                continue
            rec = json.loads(line)
            sl = NAME_TO_LANG_CODE.get(rec["source_language"], rec["source_language"])
            dl = NAME_TO_LANG_CODE.get(rec["target_language"], rec["target_language"])
            src = rec["source_text"]
            self.exact_hashes.add(pair_hash(sl, dl, src))
            self.eval_ngrams.append((sl, dl, char_ngrams(src, n=3)))

    def is_leaked(self, src_lang: str, dst_lang: str, source_text: str) -> bool:
        if pair_hash(src_lang, dst_lang, source_text) in self.exact_hashes:
            return True
        if not self.eval_ngrams:
            return False
        cand_ng = char_ngrams(source_text, n=3)
        for sl, dl, ev_ng in self.eval_ngrams:
            if sl == src_lang and dl == dst_lang and jaccard(cand_ng, ev_ng) >= self.threshold:
                return True
        return False


# ---------------------------------------------------------------------------
# Dataset assembly
# ---------------------------------------------------------------------------

@dataclass
class BuiltSample:
    instruction: str
    output: str
    axis: str
    pair: str
    origin: str
    glossary_mode: str  # "matched" | "counterfactual" | "distractor" | "empty"
    source_hash: str
    style: str = ""


def build_dataset(
    *,
    datamining_dir: Path,
    glossary_full: Path,
    community_curated: Path | None,
    koala_xlsx: Path | None,
    eval_jsonl: Path | None,
    replay_jsonl: Path | None,
    extra_colloquial_jsonl: Path | None,
    total_samples: int,
    val_size: int,
    seed: int,
    enable_style_axis: bool = False,
    style_backtranslated_jsonl: Path | None = None,
) -> tuple[list[BuiltSample], list[BuiltSample], dict[str, Any]]:
    rng = random.Random(seed)
    guard = EvalLeakageGuard(eval_jsonl)
    matcher = FastGlossaryMatcher(glossary_full)
    matcher.load_divergence_swaps(community_curated)

    langs = ("ja", "en", "de", "fr", "ko")
    pools_axis1: dict[tuple[str, str], list[tuple[str, str, str]]] = defaultdict(list)
    pools_axis2: dict[tuple[str, str], list[tuple[str, str, str]]] = defaultdict(list)

    # Load structured FFXIV sheets
    sheet_configs = [
        ("LogMessage.csv", "Text", "log_message", True, True),
        ("Balloon.csv", "Dialogue", "balloon", True, True),
        ("NpcYell.csv", "Text", "npc_yell", True, True),
        ("InstanceContentTextData.csv", "Text", "instance_text", True, False),
        ("DefaultTalk.csv", "Text[0]", "default_talk", True, True),
        ("Completion.csv", "Text", "completion", False, True),
    ]
    for filename, col, origin, to_axis1, to_axis2 in sheet_configs:
        rows = load_aligned_sheet_rows(datamining_dir, filename, col, langs)
        for row in rows:
            ko_style = classify_korean_sentence_style(row["ko"]) if enable_style_axis else None
            for src_l, dst_l in PAIR_WEIGHTS:
                if src_l not in row or dst_l not in row:
                    continue
                if enable_style_axis and dst_l == "ko" and ko_style in STYLE_GATED_ONLY:
                    continue
                s, t = row[src_l], row[dst_l]
                if not is_valid_parallel(src_l, dst_l, s, t):
                    continue
                if to_axis1 and len(s) >= 6:
                    pools_axis1[(src_l, dst_l)].append((s, t, origin))
                if to_axis2 and len(s) <= 28:
                    pools_axis2[(src_l, dst_l)].append((s, t, origin))

    # Stratified quest / cutscene / custom dialogues for Axis 1 (and short ones for Axis 2)
    quest_rows = load_quest_and_cutscene_pool(datamining_dir, langs, rng)
    for row in quest_rows:
        ko_style = classify_korean_sentence_style(row["ko"]) if enable_style_axis else None
        for src_l, dst_l in PAIR_WEIGHTS:
            if src_l not in row or dst_l not in row:
                continue
            if enable_style_axis and dst_l == "ko" and ko_style in STYLE_GATED_ONLY:
                continue
            s, t = row[src_l], row[dst_l]
            if not is_valid_parallel(src_l, dst_l, s, t):
                continue
            pools_axis1[(src_l, dst_l)].append((s, t, "quest_dialogue"))
            if len(s) <= 22:
                pools_axis2[(src_l, dst_l)].append((s, t, "quest_short"))

    # Koala 🟡소통 real player chat phrases -> high-priority Axis 2 (JA->KO)
    koala_pairs = load_koala_chat_phrases(koala_xlsx)
    koala_axis2 = [(s, t, "koala_chat") for s, t in koala_pairs]

    # Extra colloquial (optional Tatoeba / approved user chat)
    for rec in load_jsonl_pairs(extra_colloquial_jsonl):
        key = (rec["src_lang"], rec["dst_lang"])
        if key in PAIR_WEIGHTS:
            pools_axis2[key].append((rec["source_text"], rec["target_text"], rec["origin"]))

    # Replay pool (Axis 3)
    replay_pools: dict[tuple[str, str], list[tuple[str, str, str]]] = defaultdict(list)
    for rec in load_jsonl_pairs(replay_jsonl):
        key = (rec["src_lang"], rec["dst_lang"])
        if key in PAIR_WEIGHTS:
            replay_pools[key].append((rec["source_text"], rec["target_text"], rec["origin"]))

    has_replay = sum(len(v) for v in replay_pools.values()) > 0
    if enable_style_axis:
        axis1_ratio = 0.34 if has_replay else 0.40
        axis2_ratio = 0.16 if has_replay else 0.20
        axis4_ratio = 0.32 if has_replay else 0.40
        axis3_ratio = 0.18 if has_replay else 0.0
    else:
        axis1_ratio = 0.55 if has_replay else 0.68
        axis2_ratio = 0.25 if has_replay else 0.32
        axis4_ratio = 0.0
        axis3_ratio = 0.20 if has_replay else 0.0

    target_axis1 = int(total_samples * axis1_ratio)
    target_axis2 = int(total_samples * axis2_ratio)
    target_axis4 = int(total_samples * axis4_ratio) if enable_style_axis else 0
    target_axis3 = total_samples - target_axis1 - target_axis2 - target_axis4 if has_replay else 0

    built: list[BuiltSample] = []
    seen_hashes: set[str] = set()
    leaked_skipped = 0

    def try_add_sample(
        src_l: str,
        dst_l: str,
        src: str,
        dst: str,
        axis: str,
        origin: str,
        forced_mode: str,
        precomputed_matches: list[tuple[str, str, str]] | None = None,
        style: str | None = None,
    ) -> bool:
        nonlocal leaked_skipped
        sh = pair_hash(src_l, dst_l, src)
        if sh in seen_hashes:
            return False
        if guard.is_leaked(src_l, dst_l, src):
            leaked_skipped += 1
            return False

        glossary_terms: list[tuple[str, str]] = []
        mode = "empty"
        final_dst = dst

        if forced_mode in ("matched", "counterfactual"):
            verified = (
                precomputed_matches
                if precomputed_matches is not None
                else matcher.find_verified_matches(src_l, dst_l, src, dst, limit=8)
            )
            if not verified:
                return False
            if forced_mode == "counterfactual":
                final_dst, glossary_terms, did_swap = matcher.apply_counterfactual_swap(
                    src_l, dst_l, dst, verified, rng
                )
                mode = "counterfactual" if did_swap else "matched"
            else:
                glossary_terms = [(s, t) for s, t, _ in verified]
                mode = "matched"
        elif forced_mode == "distractor":
            verified = (
                precomputed_matches
                if precomputed_matches is not None
                else matcher.find_verified_matches(src_l, dst_l, src, dst, limit=3)
            )
            kept_matches = [(s, t) for s, t, _ in verified[:3]] if verified else []
            distractors = matcher.sample_distractors(src_l, dst_l, src, dst, rng, rng.randint(1, 2))
            if not distractors:
                return False
            glossary_terms = kept_matches + distractors
            rng.shuffle(glossary_terms)
            mode = "distractor"
        else:
            mode = "empty"

        prompt = render_prompt(src_l, dst_l, src, glossary_terms, style=style)
        seen_hashes.add(sh)
        built.append(
            BuiltSample(
                instruction=prompt,
                output=final_dst.strip(),
                axis=axis,
                pair=f"{LANG_CODE_TO_NAME[src_l]}->{LANG_CODE_TO_NAME[dst_l]}",
                origin=origin,
                glossary_mode=mode,
                source_hash=sh,
                style=style or "",
            )
        )
        return True

    # --- Build Axis 4 FIRST (when enabled) so rare Korean styles get first pick without hash starvation ---
    if enable_style_axis and target_axis4 > 0:
        if style_backtranslated_jsonl and style_backtranslated_jsonl.exists():
            # v0.2-r2: Use 1:1 semantically aligned on-policy back-translations for EN/DE/FR -> KO
            # and direct 1:1 official JA -> KO quest dialogue (eliminates FFXIV EN transcreation drift).
            style_buckets: dict[tuple[str, str, str], list[tuple[str, str, str]]] = defaultdict(list)
            ja_only_buckets = load_style_balanced_dialogue_pool(datamining_dir, ("ja", "ko"), rng)
            for bkey, items in ja_only_buckets.items():
                style_buckets[bkey].extend(items)
            for line in style_backtranslated_jsonl.read_text(encoding="utf-8").splitlines():
                line = line.strip()
                if not line:
                    continue
                rec = json.loads(line)
                sl = NAME_TO_LANG_CODE.get(rec.get("source_language", ""), rec.get("source_language", ""))
                dl = NAME_TO_LANG_CODE.get(rec.get("target_language", ""), rec.get("target_language", ""))
                src = str(rec.get("source_text", "")).strip()
                dst = str(rec.get("target_text", "")).strip()
                st = str(rec.get("style", "")).strip()
                orig = str(rec.get("origin", "style_backtranslated"))
                if dl == "ko" and sl in ("en", "de", "fr") and st and is_valid_parallel(sl, dl, src, dst):
                    if classify_korean_sentence_style(dst) == st:
                        style_buckets[(sl, dl, st)].append((src, dst, orig))
        else:
            style_buckets = load_style_balanced_dialogue_pool(datamining_dir, langs, rng)
        per_style_total = max(1, target_axis4 // len(KOREAN_SPEECH_STYLES))
        for style_name in KOREAN_SPEECH_STYLES:
            # First allocate per-pair quotas, then spill any shortfall (e.g. EN->KO Tataru ~680) to sibling langs
            pair_quotas = {
                pair_k: max(1, int(per_style_total * w))
                for pair_k, w in STYLE_PAIR_WEIGHTS.items()
            }
            added_for_style = 0
            leftover_pools: list[tuple[str, str, list[tuple[str, str, str]]]] = []

            for (src_l, dst_l), quota in pair_quotas.items():
                pool = list(style_buckets.get((src_l, dst_l, style_name), []))
                rng.shuffle(pool)
                q_gl_total = int(quota * 0.40)
                q_cf = int(q_gl_total * 0.25)
                q_dist = int(quota * 0.10)
                q_emp = quota - q_gl_total - q_dist
                c_cf = c_m = c_dist = c_emp = 0
                unused: list[tuple[str, str, str]] = []

                for s, t, orig in pool:
                    if (c_cf + c_m) >= q_gl_total and c_dist >= q_dist and c_emp >= q_emp:
                        unused.append((s, t, orig))
                        continue
                    verified = matcher.find_verified_matches(src_l, dst_l, s, t, limit=6)
                    if verified and (c_cf + c_m) < q_gl_total:
                        mode_req = "counterfactual" if c_cf < q_cf else "matched"
                        if try_add_sample(src_l, dst_l, s, t, "axis4_npc_style", orig, mode_req, verified, style=style_name):
                            if built[-1].glossary_mode == "counterfactual":
                                c_cf += 1
                            else:
                                c_m += 1
                            added_for_style += 1
                            continue
                    if c_dist < q_dist:
                        if try_add_sample(src_l, dst_l, s, t, "axis4_npc_style", orig, "distractor", verified or [], style=style_name):
                            c_dist += 1
                            added_for_style += 1
                            continue
                    if c_emp < q_emp:
                        if try_add_sample(src_l, dst_l, s, t, "axis4_npc_style", orig, "empty", [], style=style_name):
                            c_emp += 1
                            added_for_style += 1
                            continue
                    unused.append((s, t, orig))

                # Fill remaining glossary quota from unused if matched hits were sparse
                still_unused: list[tuple[str, str, str]] = []
                for s, t, orig in unused:
                    if (c_cf + c_m + c_dist + c_emp) < quota:
                        if try_add_sample(src_l, dst_l, s, t, "axis4_npc_style", orig, "empty", [], style=style_name):
                            c_emp += 1
                            added_for_style += 1
                            continue
                    still_unused.append((s, t, orig))
                if still_unused:
                    leftover_pools.append((src_l, dst_l, still_unused))

            # Spillover fill across sibling languages if EN->KO alone had fewer than target (e.g. 애교 존댓말)
            if added_for_style < per_style_total and leftover_pools:
                flat_leftovers: list[tuple[str, str, str, str, str]] = []
                for sl, dl, items in leftover_pools:
                    for s, t, orig in items:
                        flat_leftovers.append((sl, dl, s, t, orig))
                rng.shuffle(flat_leftovers)
                for sl, dl, s, t, orig in flat_leftovers:
                    if added_for_style >= per_style_total:
                        break
                    verified = matcher.find_verified_matches(sl, dl, s, t, limit=6) if rng.random() < 0.35 else []
                    mode_req = "matched" if verified else "empty"
                    if try_add_sample(sl, dl, s, t, "axis4_npc_style", orig, mode_req, verified, style=style_name):
                        added_for_style += 1

    # --- Build Axis 1 (FFXIV Glossary-Conditioned with explicit 65/20/15 mode quotas) ---
    for (src_l, dst_l), weight in PAIR_WEIGHTS.items():
        quota = max(1, int(target_axis1 * weight))
        q_matched_total = int(quota * 0.65)
        q_counterfactual = int(q_matched_total * 0.25)
        q_matched_plain = q_matched_total - q_counterfactual
        q_distractor = int(quota * 0.15)
        q_empty = quota - q_matched_total - q_distractor

        pool = list(pools_axis1[(src_l, dst_l)])
        rng.shuffle(pool)

        # Pass 1: fill matched + counterfactual strictly from sentences with verified glossary hits
        unmatched_overflow: list[tuple[str, str, str]] = []
        c_cf = 0
        c_m = 0
        c_dist = 0
        c_emp = 0
        for s, t, orig in pool:
            if (c_cf + c_m) >= q_matched_total and c_dist >= q_distractor and c_emp >= q_empty:
                break
            verified = matcher.find_verified_matches(src_l, dst_l, s, t, limit=8)
            if verified:
                if c_cf < q_counterfactual and (c_cf + c_m) < q_matched_total:
                    if try_add_sample(src_l, dst_l, s, t, "axis1_ffxiv_glossary", orig, "counterfactual", verified):
                        if built[-1].glossary_mode == "counterfactual":
                            c_cf += 1
                        else:
                            c_m += 1
                    continue
                if (c_cf + c_m) < q_matched_total:
                    if try_add_sample(src_l, dst_l, s, t, "axis1_ffxiv_glossary", orig, "matched", verified):
                        c_m += 1
                    continue
                if c_dist < q_distractor:
                    if try_add_sample(src_l, dst_l, s, t, "axis1_ffxiv_glossary", orig, "distractor", verified):
                        c_dist += 1
                    continue
                if c_emp < q_empty:
                    if try_add_sample(src_l, dst_l, s, t, "axis1_ffxiv_glossary", orig, "empty", verified):
                        c_emp += 1
                    continue
            else:
                unmatched_overflow.append((s, t, orig))

        # Pass 2: fill any remaining distractor/empty slots from unmatched overflow
        for s, t, orig in unmatched_overflow:
            if c_dist < q_distractor:
                if try_add_sample(src_l, dst_l, s, t, "axis1_ffxiv_glossary", orig, "distractor", []):
                    c_dist += 1
            elif c_emp < q_empty:
                if try_add_sample(src_l, dst_l, s, t, "axis1_ffxiv_glossary", orig, "empty", []):
                    c_emp += 1
            else:
                break

    # --- Build Axis 2 (Short Colloquial & Game Chat) ---
    rng.shuffle(koala_axis2)
    koala_added = 0
    for s, t, orig in koala_axis2:
        if try_add_sample("ja", "ko", s, t, "axis2_colloquial_chat", orig, "empty"):
            koala_added += 1

    for (src_l, dst_l), weight in PAIR_WEIGHTS.items():
        quota = max(1, int(target_axis2 * weight))
        if (src_l, dst_l) == ("ja", "ko"):
            quota = max(0, quota - koala_added)
        pool = list(pools_axis2[(src_l, dst_l)])
        rng.shuffle(pool)
        added = 0
        for s, t, orig in pool:
            if added >= quota:
                break
            mode = "empty"
            verified = None
            if rng.random() < 0.20:
                verified = matcher.find_verified_matches(src_l, dst_l, s, t, limit=4)
                if verified:
                    mode = "matched"
            if try_add_sample(src_l, dst_l, s, t, "axis2_colloquial_chat", orig, mode, verified):
                added += 1

    # --- Build Axis 3 (General On-Policy Replay, NO glossary) ---
    if has_replay and target_axis3 > 0:
        for (src_l, dst_l), weight in PAIR_WEIGHTS.items():
            quota = max(1, int(target_axis3 * weight))
            pool = list(replay_pools[(src_l, dst_l)])
            rng.shuffle(pool)
            added = 0
            for s, t, orig in pool:
                if added >= quota:
                    break
                if try_add_sample(src_l, dst_l, s, t, "axis3_general_replay", orig, "empty"):
                    added += 1

    rng.shuffle(built)
    actual_val = min(val_size, max(1, len(built) // 20)) if built else 0
    val_samples = built[:actual_val]
    train_samples = built[actual_val:]

    # Invariant: zero overlap between train, val, and eval
    train_hashes = {s.source_hash for s in train_samples}
    val_hashes = {s.source_hash for s in val_samples}
    assert len(train_hashes & val_hashes) == 0
    assert len((train_hashes | val_hashes) & guard.exact_hashes) == 0

    stats = {
        "generated_at": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "seed": seed,
        "train_count": len(train_samples),
        "val_count": len(val_samples),
        "leaked_eval_skipped": leaked_skipped,
        "eval_overlap_verified": 0,
        "has_replay_axis3": has_replay,
        "enable_style_axis": enable_style_axis,
        "by_axis": dict(Counter(s.axis for s in built)),
        "by_pair": dict(Counter(s.pair for s in built).most_common()),
        "by_glossary_mode": dict(Counter(s.glossary_mode for s in built)),
        "by_style": dict(Counter(s.style for s in built if s.style)),
        "by_origin": dict(Counter(s.origin for s in built).most_common()),
    }
    return train_samples, val_samples, stats


def write_llamafactory_jsonl(path: Path, samples: Sequence[BuiltSample]) -> None:
    lines = [
        json.dumps({"instruction": s.instruction, "input": "", "output": s.output}, ensure_ascii=False)
        for s in samples
    ]
    path.write_text("\n".join(lines) + ("\n" if lines else ""), encoding="utf-8")


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Build LlamaFactory SFT dataset for MiLMMT-46-1B/4B-v1.0.")
    parser.add_argument("--datamining-dir", type=Path, required=True)
    parser.add_argument("--glossary-full", type=Path, required=True)
    parser.add_argument("--community-curated", type=Path)
    parser.add_argument("--koala-xlsx", type=Path)
    parser.add_argument("--eval-jsonl", type=Path)
    parser.add_argument("--replay-jsonl", type=Path, help="Optional general on-policy replay JSONL (Axis 3).")
    parser.add_argument("--extra-colloquial-jsonl", type=Path, help="Optional extra colloquial chat JSONL (Axis 2).")
    parser.add_argument("--enable-style-axis", action="store_true", help="Enable v0.2 Axis 4 (8-way Korean NPC speech-style conditioning).")
    parser.add_argument("--style-backtranslated-jsonl", type=Path, help="Optional 1:1 on-policy back-translated EN/DE/FR->KO JSONL for Axis 4.")
    parser.add_argument("--total-samples", type=int, default=22000)
    parser.add_argument("--val-size", type=int, default=500)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--out-dir", type=Path, required=True)
    args = parser.parse_args(argv)

    train_s, val_s, stats = build_dataset(
        datamining_dir=args.datamining_dir,
        glossary_full=args.glossary_full,
        community_curated=args.community_curated,
        koala_xlsx=args.koala_xlsx,
        eval_jsonl=args.eval_jsonl,
        replay_jsonl=args.replay_jsonl,
        extra_colloquial_jsonl=args.extra_colloquial_jsonl,
        total_samples=args.total_samples,
        val_size=args.val_size,
        seed=args.seed,
        enable_style_axis=args.enable_style_axis,
        style_backtranslated_jsonl=args.style_backtranslated_jsonl,
    )

    args.out_dir.mkdir(parents=True, exist_ok=True)
    write_llamafactory_jsonl(args.out_dir / "train_sft.jsonl", train_s)
    write_llamafactory_jsonl(args.out_dir / "val_sft.jsonl", val_s)
    (args.out_dir / "dataset_stats.json").write_text(
        json.dumps(stats, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(
        f"Built SFT dataset -> {args.out_dir}\n"
        f"  train={stats['train_count']:,}, val={stats['val_count']:,}, "
        f"leaked_skipped={stats['leaked_eval_skipped']}, has_replay={stats['has_replay_axis3']}, "
        f"style_axis={stats['enable_style_axis']}\n"
        f"  by_axis={stats['by_axis']}\n"
        f"  by_glossary_mode={stats['by_glossary_mode']}\n"
        f"  by_style={stats['by_style']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
