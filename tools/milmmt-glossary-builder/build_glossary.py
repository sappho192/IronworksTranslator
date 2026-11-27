#!/usr/bin/env python3
"""Build tiered FFXIV translation glossaries from ffxiv-datamining CSVs and Drive notes.

Zero external dependencies (Python 3.10+ standard library only).

Outputs:
  - glossary.runtime.json : Curated, low-noise glossary for live app prompt injection.
  - glossary.full.json    : Expanded glossary (including items, actions, NPCs) for SFT/eval.
  - glossary.report.json  : Extraction stats, drop reasons, and cross-tier conflict log.
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import json
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET
import zipfile
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Iterator, Sequence

# ---------------------------------------------------------------------------
# Language & schema configuration
# ---------------------------------------------------------------------------

LANG_CODE_TO_NAME: dict[str, str] = {
    "ja": "Japanese",
    "en": "English",
    "de": "German",
    "fr": "French",
    "ko": "Korean",
}

DEFAULT_SOURCE_LANGS = ("ja", "en", "de", "fr")
DEFAULT_TARGET_LANG = "ko"


@dataclass(frozen=True)
class SheetSpec:
    filename: str
    column: str
    tier: int  # Lower number = higher priority
    category: str
    include_in_runtime: bool
    max_source_len: int = 48
    max_target_len: int = 48
    min_ja_len: int = 2
    min_latin_len: int = 3
    extra_columns: tuple[str, ...] = ()


# Priority Tiers:
#   0 = user_override (curated user reports / manual overrides)
#   1 = official_core (PlaceName, ClassJob, ContentFinderCondition, Status, etc.)
#   2 = community_chat (Google Drive player slang, PF recruitment, raid callouts)
#   3 = official_ui_completion (Completion auto-translate phrases, short Addon UI terms)
#   4 = official_extended (Action, Item, ENpcResident, BNpcName, Mount, Companion, etc.)
DATAMINING_SHEETS: tuple[SheetSpec, ...] = (
    # Tier 1: Official core terminology (high precision, included in runtime)
    SheetSpec("ClassJob.csv", "Name", 1, "class_job", True, min_ja_len=2, min_latin_len=3),
    SheetSpec("GrandCompany.csv", "Name", 1, "grand_company", True),
    SheetSpec("Race.csv", "Masculine", 1, "race", True),
    SheetSpec("Tribe.csv", "Masculine", 1, "tribe", True),
    SheetSpec("GuardianDeity.csv", "Name", 1, "deity", True),
    SheetSpec("PlaceName.csv", "Name", 1, "place_name", True, min_ja_len=2, min_latin_len=4),
    SheetSpec("ContentFinderCondition.csv", "Name", 1, "duty", True, max_source_len=64, max_target_len=64),
    SheetSpec("Status.csv", "Name", 1, "status", True, min_ja_len=2, min_latin_len=4),
    SheetSpec("Trait.csv", "Name", 1, "trait", True, min_ja_len=2, min_latin_len=4),
    SheetSpec("MainCommand.csv", "Name", 1, "ui_command", True, min_ja_len=2, min_latin_len=4),
    SheetSpec("GeneralAction.csv", "Name", 1, "general_action", True, min_ja_len=2, min_latin_len=4),
    SheetSpec("Weather.csv", "Name", 1, "weather", True, min_ja_len=2, min_latin_len=4),
    # Tier 3: Official UI & auto-translate completion phrases
    SheetSpec("Completion.csv", "Text", 3, "completion", True, max_source_len=36, max_target_len=36, min_ja_len=2, min_latin_len=4),
    SheetSpec("Addon.csv", "Text", 3, "addon_ui", True, max_source_len=18, max_target_len=24, min_ja_len=3, min_latin_len=5),
    # Tier 4: Official extended entities (full training/eval dictionary; runtime keeps concise subset or excludes noisy)
    SheetSpec("Action.csv", "Name", 4, "action", False, min_ja_len=2, min_latin_len=4),
    SheetSpec("CraftAction.csv", "Name", 4, "craft_action", False, min_ja_len=2, min_latin_len=4),
    SheetSpec("Item.csv", "Name", 4, "item", False, max_source_len=56, max_target_len=56, min_ja_len=3, min_latin_len=4),
    SheetSpec("ENpcResident.csv", "Singular", 4, "npc_name", False, min_ja_len=2, min_latin_len=4),
    SheetSpec("BNpcName.csv", "Singular", 4, "enemy_name", False, min_ja_len=2, min_latin_len=4),
    SheetSpec("Mount.csv", "Singular", 4, "mount", False, min_ja_len=2, min_latin_len=4),
    SheetSpec("Companion.csv", "Singular", 4, "minion", False, min_ja_len=2, min_latin_len=4),
    SheetSpec("TripleTriadCard.csv", "Name", 4, "tt_card", False, min_ja_len=2, min_latin_len=4),
    SheetSpec("Title.csv", "Masculine", 4, "title", False, min_ja_len=2, min_latin_len=5),
    SheetSpec("Fate.csv", "Name", 4, "fate", False, max_source_len=64, max_target_len=64, min_ja_len=3, min_latin_len=5),
    SheetSpec("Quest.csv", "Name", 4, "quest", False, max_source_len=64, max_target_len=64, min_ja_len=3, min_latin_len=5),
)

# ---------------------------------------------------------------------------
# Text normalization & validation helpers
# ---------------------------------------------------------------------------

RE_SE_TAG = re.compile(r"<[^>]{1,120}>")
RE_HANGUL = re.compile(r"[\uAC00-\uD7A3\u3131-\u318E]")
RE_KANA = re.compile(r"[\u3040-\u309F\u30A0-\u30FF\u31F0-\u31FF]")
RE_CJK = re.compile(r"[\u4E00-\u9FFF]")
RE_LATIN = re.compile(r"[A-Za-zÀ-ÖØ-öø-ÿ]")
RE_DUMMY_PATTERNS = re.compile(
    r"(?:テスト|ダミー|未使用|無効値|開発用|削除予定|中国版専用|^[A-Za-z0-9_]+_[A-Za-z0-9_]+$|\*{3,}|^TBD$|^TODO$|추가예정|8\.0\s*추가)",
    re.IGNORECASE,
)
RE_PLACEHOLDER_TOKEN = re.compile(r"(?:\{\d+\}|%[sd]|\[[^\]]*\]|\?{1,2}명|n시간|n밥|\(직업\)|\(숫자\)|\(아이템\)|\(기믹\))")
RE_UI_SENTENCE_PUNCT = re.compile(r"[。．！？!?…]")
RE_ONLY_SYMBOLS_OR_DIGITS = re.compile(r"^[\W\d_]+$")
RE_BRACKET_WRAP = re.compile(r"^【(.+)】$")
RE_ITALIC_WRAP = re.compile(r"^\*([^*]+)\*$")
RE_LEADING_THE = re.compile(r"^The\s+(.+)$")

# High-frequency generic words that cause noisy false matches in Addon/Completion/extended tables.
GENERIC_JA_STOPWORDS = frozenset({
    "はい", "いいえ", "なし", "あり", "すべて", "その他", "戻る", "閉じる", "決定",
    "開始", "終了", "確認", "変更", "設定", "削除", "追加", "保存", "検索",
    "移動", "実行", "選択", "表示", "次へ", "前へ", "上", "下", "左", "右",
    "前", "後", "中", "大", "小", "新", "旧", "名", "人", "回", "個", "秒", "分", "時間",
    "攻撃", "対象", "連続", "範囲", "基準", "必要", "可能", "調整", "募集",
})
GENERIC_EN_STOPWORDS = frozenset({
    "yes", "no", "ok", "none", "all", "other", "back", "close", "confirm", "cancel",
    "start", "end", "edit", "save", "delete", "add", "search", "select", "next", "help",
    "open", "left", "right", "front", "behind", "inside", "outside", "attack", "target",
    "move", "time", "name", "item", "quest", "party", "player", "action", "status",
    "normal", "hard", "extreme", "savage", "free", "level", "rank", "type", "text",
})
GENERIC_DE_FR_STOPWORDS = frozenset({
    "ja", "nein", "oui", "non", "alle", "tous", "zurück", "retour", "fermer",
    "schließen", "start", "ende", "fin", "name", "nom", "zeit", "temps", "text", "texte",
})


@dataclass
class TermEntry:
    pair: str  # e.g. "Japanese->Korean"
    source: str
    target: str
    tier: int
    origin: str  # e.g. "datamining:PlaceName.csv" or "drive:koala:🔵용어"
    category: str
    include_in_runtime: bool


@dataclass
class ConflictRecord:
    pair: str
    source: str
    winner_target: str
    winner_tier: int
    winner_origin: str
    loser_target: str
    loser_tier: int
    loser_origin: str


def normalize_space(text: str) -> str:
    text = unicodedata.normalize("NFKC", text)
    # Preserve full-width tilde/punctuation quirks only when meaningful; collapse whitespace.
    text = text.replace("\r\n", " ").replace("\n", " ").replace("\t", " ")
    return re.sub(r"\s+", " ", text).strip()


def clean_exd_text(raw: str) -> tuple[str | None, str | None]:
    """Normalize EXD cell text; returns (cleaned, drop_reason)."""
    if not raw or not raw.strip():
        return None, "empty"
    if "\n" in raw or "\r" in raw:
        return None, "multiline"
    if RE_SE_TAG.search(raw):
        return None, "has_se_tag"
    text = normalize_space(raw)
    m = RE_BRACKET_WRAP.match(text)
    if m:
        text = m.group(1).strip()
    m_it = RE_ITALIC_WRAP.match(text)
    if m_it:
        text = m_it.group(1).strip()
    if not text:
        return None, "empty"
    if RE_ONLY_SYMBOLS_OR_DIGITS.match(text):
        return None, "symbols_or_digits"
    if RE_DUMMY_PATTERNS.search(text):
        return None, "dummy_pattern"
    return text, None


def validate_pair(
    source_lang: str,
    target_lang: str,
    source: str,
    target: str,
    *,
    max_source_len: int = 48,
    max_target_len: int = 48,
    min_ja_len: int = 2,
    min_latin_len: int = 3,
    disallow_sentence_punct: bool = False,
    filter_generic_stopwords: bool = False,
) -> str | None:
    """Return None if valid, otherwise a short drop reason."""
    if not source or not target:
        return "empty"
    if len(source) > max_source_len or len(target) > max_target_len:
        return "too_long"
    if source.casefold() == target.casefold():
        return "identical"
    if RE_ONLY_SYMBOLS_OR_DIGITS.match(source) or RE_ONLY_SYMBOLS_OR_DIGITS.match(target):
        return "symbols_or_digits"
    if RE_DUMMY_PATTERNS.search(source) or RE_DUMMY_PATTERNS.search(target):
        return "dummy_pattern"
    if disallow_sentence_punct and (RE_UI_SENTENCE_PUNCT.search(source) or RE_UI_SENTENCE_PUNCT.search(target)):
        return "sentence_punct"

    # Script sanity checks
    if target_lang == "ko":
        if not RE_HANGUL.search(target):
            return "target_no_hangul"
        if RE_KANA.search(target):
            return "target_has_kana"

    if source_lang == "ja":
        if len(source) < min_ja_len:
            return "source_too_short"
        if RE_HANGUL.search(source):
            return "source_has_hangul"
        if filter_generic_stopwords and source in GENERIC_JA_STOPWORDS:
            return "generic_stopword"
    elif source_lang in ("en", "de", "fr"):
        if len(source) < min_latin_len:
            return "source_too_short"
        if RE_KANA.search(source) or RE_CJK.search(source) or RE_HANGUL.search(source):
            return "latin_source_has_cjk"
        if not RE_LATIN.search(source):
            return "latin_source_no_latin"
        low = source.casefold()
        if filter_generic_stopwords:
            if source_lang == "en" and low in GENERIC_EN_STOPWORDS:
                return "generic_stopword"
            if source_lang in ("de", "fr") and low in GENERIC_DE_FR_STOPWORDS:
                return "generic_stopword"

    return None


# ---------------------------------------------------------------------------
# ffxiv-datamining CSV extraction
# ---------------------------------------------------------------------------

def resolve_lang_csv_dir(datamining_root: Path, lang: str) -> Path:
    """Resolve `csv/<lang>` vs submodule `csv/<lang>/csv` layout."""
    direct = datamining_root / "csv" / lang
    nested = direct / "csv"
    if nested.is_dir() and (nested / "ClassJob.csv").exists():
        return nested
    if direct.is_dir():
        return direct
    raise FileNotFoundError(f"Language CSV directory not found for '{lang}' under {datamining_root}")


def load_csv_column(csv_path: Path, column_name: str) -> dict[str, str]:
    """Read a single named column keyed by row `#` ID from an xiv-data-oxidizer CSV."""
    rows: dict[str, str] = {}
    with csv_path.open("r", encoding="utf-8", errors="replace", newline="") as handle:
        reader = csv.reader(handle)
        try:
            headers = next(reader)
        except StopIteration:
            return rows
        if column_name not in headers:
            return rows
        col_idx = headers.index(column_name)
        for row in reader:
            if not row or len(row) <= col_idx:
                continue
            val = row[col_idx]
            if val:
                rows[row[0]] = val
    return rows


def extract_datamining_terms(
    datamining_root: Path,
    source_langs: Sequence[str],
    target_lang: str,
    drop_counts: Counter[str],
    sheets: Sequence[SheetSpec] = DATAMINING_SHEETS,
) -> list[TermEntry]:
    lang_dirs = {
        lang: resolve_lang_csv_dir(datamining_root, lang)
        for lang in (*source_langs, target_lang)
    }
    entries: list[TermEntry] = []

    for spec in sheets:
        target_csv = lang_dirs[target_lang] / spec.filename
        if not target_csv.exists():
            drop_counts[f"missing_sheet:{spec.filename}"] += 1
            continue
        target_map = load_csv_column(target_csv, spec.column)
        if not target_map:
            continue

        for src_lang in source_langs:
            src_csv = lang_dirs[src_lang] / spec.filename
            if not src_csv.exists():
                continue
            src_map = load_csv_column(src_csv, spec.column)
            pair_key = f"{LANG_CODE_TO_NAME[src_lang]}->{LANG_CODE_TO_NAME[target_lang]}"
            origin = f"datamining:{spec.filename}"
            is_ui_or_extended = spec.tier >= 3

            for row_id, raw_src in src_map.items():
                raw_tgt = target_map.get(row_id)
                if not raw_tgt:
                    continue
                src_clean, src_drop = clean_exd_text(raw_src)
                if src_drop:
                    drop_counts[f"datamining:{src_drop}"] += 1
                    continue
                tgt_clean, tgt_drop = clean_exd_text(raw_tgt)
                if tgt_drop:
                    drop_counts[f"datamining:{tgt_drop}"] += 1
                    continue

                reason = validate_pair(
                    src_lang,
                    target_lang,
                    src_clean,
                    tgt_clean,
                    max_source_len=spec.max_source_len,
                    max_target_len=spec.max_target_len,
                    min_ja_len=spec.min_ja_len,
                    min_latin_len=spec.min_latin_len,
                    disallow_sentence_punct=(spec.filename == "Addon.csv"),
                    filter_generic_stopwords=is_ui_or_extended,
                )
                if reason:
                    drop_counts[f"datamining:{reason}"] += 1
                    continue

                entries.append(
                    TermEntry(
                        pair=pair_key,
                        source=src_clean,
                        target=tgt_clean,
                        tier=spec.tier,
                        origin=origin,
                        category=spec.category,
                        include_in_runtime=spec.include_in_runtime,
                    )
                )
                # For English proper-noun places starting with "The " (e.g. "The Mizzenmast"),
                # also emit an article-free alias at slightly lower priority (tier + 1) when long enough.
                if src_lang == "en" and spec.filename == "PlaceName.csv":
                    m_the = RE_LEADING_THE.match(src_clean)
                    if m_the:
                        bare = m_the.group(1).strip()
                        if len(bare) >= 6 and bare.casefold() not in GENERIC_EN_STOPWORDS:
                            entries.append(
                                TermEntry(
                                    pair=pair_key,
                                    source=bare,
                                    target=tgt_clean,
                                    tier=spec.tier + 1,
                                    origin=f"{origin}#no_article",
                                    category=spec.category,
                                    include_in_runtime=spec.include_in_runtime,
                                )
                            )

    return entries


# ---------------------------------------------------------------------------
# XLSX reader (pure stdlib, with merged-cell propagation)
# ---------------------------------------------------------------------------

XLSX_NS = {
    "a": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
    "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
}


def col_to_index(col: str) -> int:
    idx = 0
    for ch in col:
        idx = idx * 26 + (ord(ch.upper()) - ord("A") + 1)
    return idx


def index_to_col(idx: int) -> str:
    chars: list[str] = []
    while idx > 0:
        idx, rem = divmod(idx - 1, 26)
        chars.append(chr(ord("A") + rem))
    return "".join(reversed(chars))


def parse_cell_ref(ref: str) -> tuple[str, int]:
    m = re.match(r"^([A-Za-z]+)(\d+)$", ref)
    if not m:
        raise ValueError(f"Invalid Excel cell reference: {ref}")
    return m.group(1).upper(), int(m.group(2))


def read_xlsx_sheets(xlsx_path: Path) -> dict[str, dict[int, dict[str, str]]]:
    """Return {sheet_name: {row_num: {col_letter: text}}} with merged-cell values propagated."""
    result: dict[str, dict[int, dict[str, str]]] = {}
    with zipfile.ZipFile(xlsx_path) as zf:
        shared: list[str] = []
        if "xl/sharedStrings.xml" in zf.namelist():
            sroot = ET.fromstring(zf.read("xl/sharedStrings.xml"))
            for si in sroot.findall("a:si", XLSX_NS):
                shared.append("".join(t.text or "" for t in si.findall(".//a:t", XLSX_NS)))

        wb = ET.fromstring(zf.read("xl/workbook.xml"))
        rels_root = ET.fromstring(zf.read("xl/_rels/workbook.xml.rels"))
        rel_map = {el.attrib["Id"]: el.attrib["Target"] for el in rels_root}

        for sh in wb.findall(".//a:sheet", XLSX_NS):
            name = sh.attrib["name"]
            rid = sh.attrib["{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"]
            target = rel_map.get(rid, "")
            if not target.startswith("xl/"):
                target = "xl/" + target.lstrip("/")
            if target not in zf.namelist():
                continue

            sroot = ET.fromstring(zf.read(target))
            rows_map: dict[int, dict[str, str]] = {}
            for row_el in sroot.findall(".//a:sheetData/a:row", XLSX_NS):
                row_num = int(row_el.attrib["r"])
                row_cells: dict[str, str] = {}
                for c_el in row_el.findall("a:c", XLSX_NS):
                    ref = c_el.attrib.get("r", "")
                    if not ref:
                        continue
                    col_letter, _ = parse_cell_ref(ref)
                    ctype = c_el.attrib.get("t")
                    v_el = c_el.find("a:v", XLSX_NS)
                    is_el = c_el.find("a:is", XLSX_NS)
                    val = ""
                    if ctype == "s" and v_el is not None and v_el.text:
                        idx = int(v_el.text)
                        val = shared[idx] if idx < len(shared) else ""
                    elif ctype == "inlineStr" and is_el is not None:
                        val = "".join(t.text or "" for t in is_el.findall(".//a:t", XLSX_NS))
                    elif v_el is not None and v_el.text:
                        val = v_el.text
                    val = val.strip()
                    if val:
                        row_cells[col_letter] = val
                if row_cells:
                    rows_map[row_num] = row_cells

            # Propagate top-left value across merged ranges (vertical or horizontal)
            for mc in sroot.findall(".//a:mergeCells/a:mergeCell", XLSX_NS):
                mref = mc.attrib.get("ref", "")
                if ":" not in mref:
                    continue
                start_ref, end_ref = mref.split(":", 1)
                c1, r1 = parse_cell_ref(start_ref)
                c2, r2 = parse_cell_ref(end_ref)
                top_val = rows_map.get(r1, {}).get(c1, "")
                if not top_val:
                    continue
                i1, i2 = col_to_index(c1), col_to_index(c2)
                for r in range(r1, r2 + 1):
                    row_dict = rows_map.setdefault(r, {})
                    for ci in range(i1, i2 + 1):
                        col = index_to_col(ci)
                        row_dict.setdefault(col, top_val)

            result[name] = rows_map
    return result


# ---------------------------------------------------------------------------
# Drive community sheet & doc parsers
# ---------------------------------------------------------------------------

def clean_community_gloss(raw: str) -> str:
    """Trim parenthetical explanations and pick concise Korean translation target."""
    text = normalize_space(raw)
    # Remove leading notes like "(보통은 마물런 후) " or "(직역:옷갈아입음)"
    text = re.sub(r"^\([^)]{1,30}\)\s*", "", text)
    # Remove trailing explanatory parentheticals like "(2상을 전제하는 단어)"
    text = re.sub(r"\s*\([^)]{1,40}\)$", "", text)
    # If multiple meanings are separated by "/" or "," take first concise variant if very long
    if len(text) > 28 and "/" in text:
        text = text.split("/", 1)[0].strip()
    return text.strip(" -·")


def split_ja_variants(raw: str) -> list[str]:
    """Split cells containing multiple JA variants separated by newline, '/', '、', or fullwidth space."""
    text = raw.replace("\r\n", "\n")
    # Strip hiragana reading hints in parens like "誤爆(ごばく)" or "しめ(시메)"
    text = re.sub(r"[(\（][^\)\）]{1,20}[\)\）]", "", text)
    parts = re.split(r"[\n/／、\u3000]+", text)
    cleaned: list[str] = []
    for p in parts:
        p = normalize_space(p)
        if not p:
            continue
        if RE_PLACEHOLDER_TOKEN.search(p):
            continue
        if any(ch in p for ch in ("?", "？", "～", "~", "○", "〇", "×")):
            continue
        cleaned.append(p)
    return cleaned


def extract_koala_xlsx(
    xlsx_path: Path,
    drop_counts: Counter[str],
    phrase_pairs: list[dict] | None = None,
) -> list[TermEntry]:
    """Parse Koala's FF14 Japanese note workbook (🟠모집, 🟡소통, 🟢기믹리딩, 🔵용어).

    Long conversational sentences (especially in 🟡소통) are exported to `phrase_pairs`
    for SFT training rather than polluting runtime glossary term matching.
    """
    sheets = read_xlsx_sheets(xlsx_path)
    entries: list[TermEntry] = []

    # (sheet_name, [(source_cols, target_col), ...], category, skip_generic_stopwords, max_glossary_len)
    sheet_layouts: list[tuple[str, list[tuple[tuple[str, ...], str]], str, bool, int]] = [
        ("🟠모집", [(("B",), "C"), (("E",), "F"), (("H",), "I"), (("K",), "L"), (("N",), "O")], "community_pf", True, 16),
        ("🟡소통", [(("B", "C"), "D"), (("G",), "H"), (("I",), "J")], "community_chat", False, 10),
        ("🟢기믹리딩", [(("B",), "D"), (("F",), "H"), (("J",), "L")], "community_raid", True, 16),
        ("🔵용어", [(("B",), "C"), (("E",), "F"), (("H",), "I"), (("K",), "L")], "community_term", True, 20),
    ]

    for sheet_name, blocks, category, filter_generic, max_glossary_len in sheet_layouts:
        rows = sheets.get(sheet_name)
        if not rows:
            continue
        for row_num, cells in sorted(rows.items()):
            for src_cols, tgt_col in blocks:
                raw_tgt = cells.get(tgt_col, "")
                if not raw_tgt:
                    continue
                if RE_PLACEHOLDER_TOKEN.search(raw_tgt):
                    drop_counts["koala:placeholder_target"] += 1
                    continue
                tgt = clean_community_gloss(raw_tgt)
                if not tgt:
                    drop_counts["koala:empty_target"] += 1
                    continue

                for sc in src_cols:
                    raw_src = cells.get(sc, "")
                    if not raw_src:
                        continue
                    # Skip section header cells (which are pure Korean)
                    if RE_HANGUL.search(raw_src):
                        drop_counts["koala:header_or_hangul_src"] += 1
                        continue
                    for variant in split_ja_variants(raw_src):
                        is_ascii_abbrev = bool(re.fullmatch(r"[A-Za-z0-9+.\-]{2,12}", variant))
                        if len(variant) < 2:
                            drop_counts["koala:too_short"] += 1
                            continue
                        if not is_ascii_abbrev and not (RE_KANA.search(variant) or RE_CJK.search(variant)):
                            drop_counts["koala:non_ja_src"] += 1
                            continue
                        reason = validate_pair(
                            "ja",
                            "ko",
                            variant,
                            tgt,
                            max_source_len=80,
                            max_target_len=80,
                            min_ja_len=2,
                            filter_generic_stopwords=filter_generic,
                        )
                        if reason:
                            drop_counts[f"koala:{reason}"] += 1
                            continue
                        # Always record conversational 🟡소통 rows into parallel phrase SFT export
                        if phrase_pairs is not None and sheet_name == "🟡소통":
                            phrase_pairs.append(
                                {
                                    "source_language": "Japanese",
                                    "target_language": "Korean",
                                    "source_text": variant,
                                    "target_text": tgt,
                                    "origin": f"drive:koala:{sheet_name}",
                                    "category": category,
                                }
                            )
                        # Only short term-like entries belong in glossary matching
                        if len(variant) > max_glossary_len or len(tgt) > 24:
                            drop_counts["koala:routed_to_phrases_only"] += 1
                            continue
                        entries.append(
                            TermEntry(
                                pair="Japanese->Korean",
                                source=variant,
                                target=tgt,
                                tier=2,
                                origin=f"drive:koala:{sheet_name}",
                                category=category,
                                include_in_runtime=True,
                            )
                        )
    return entries


def extract_jclient_terms_xlsx(xlsx_path: Path, drop_counts: Counter[str]) -> list[TermEntry]:
    """Parse 'FF14 J클라 게임 용어 정리' spreadsheet."""
    sheets = read_xlsx_sheets(xlsx_path)
    entries: list[TermEntry] = []
    rows = sheets.get("시트1") or next(iter(sheets.values()), {})
    for row_num, cells in sorted(rows.items()):
        if row_num <= 3:
            continue
        # Main table: C=원문, F=뜻
        for src_col, tgt_col in (("C", "F"), ("I", "K")):
            raw_src = cells.get(src_col, "")
            raw_tgt = cells.get(tgt_col, "")
            if not raw_src or not raw_tgt:
                continue
            if RE_HANGUL.search(raw_src):
                drop_counts["jclient:hangul_src"] += 1
                continue
            # Skip packed 1-char job list row (e.g., 暗戦ナガン白学占...)
            if len(raw_src) >= 12 and all(RE_CJK.match(ch) or RE_KANA.match(ch) for ch in raw_src) and "/" not in raw_src:
                drop_counts["jclient:packed_job_list"] += 1
                continue

            src_variants = split_ja_variants(raw_src)
            tgt_parts = [clean_community_gloss(p) for p in re.split(r"[/／]", raw_tgt) if p.strip()]
            if not src_variants or not tgt_parts:
                continue

            # If counts match 1:1 (e.g. "CF/PF/RF" <-> "컨텐츠파인더/파티찾기/레이드찾기"), align pairwise
            if len(src_variants) == len(tgt_parts):
                pairs = list(zip(src_variants, tgt_parts))
            else:
                primary_tgt = clean_community_gloss(raw_tgt)
                pairs = [(v, primary_tgt) for v in src_variants]

            for variant, tgt in pairs:
                if len(variant) < 2:
                    drop_counts["jclient:too_short"] += 1
                    continue
                if "O" in variant and "人" in variant:
                    drop_counts["jclient:placeholder_src"] += 1
                    continue
                reason = validate_pair(
                    "ja",
                    "ko",
                    variant,
                    tgt,
                    max_source_len=36,
                    max_target_len=36,
                    min_ja_len=2,
                    filter_generic_stopwords=True,
                )
                if reason:
                    drop_counts[f"jclient:{reason}"] += 1
                    continue
                entries.append(
                    TermEntry(
                        pair="Japanese->Korean",
                        source=variant,
                        target=tgt,
                        tier=2,
                        origin="drive:jclient_terms",
                        category="community_jclient",
                        include_in_runtime=True,
                    )
                )
    return entries


def extract_ff14_terms_md(md_path: Path, drop_counts: Counter[str]) -> list[TermEntry]:
    """Parse '파판 용어' markdown export (`Korean_gloss Japanese_term1 Japanese_term2 ...`)."""
    entries: list[TermEntry] = []
    text = md_path.read_text(encoding="utf-8", errors="replace")
    for raw_line in text.splitlines():
        line = raw_line.replace("\\~", "~").replace("\\*", "*").strip()
        if not line:
            continue
        # Remove explanatory parenthetical clauses first
        stripped = re.sub(r"\([^)]*\)", "", line).strip()
        # Split where Japanese/ASCII-abbrev token sequence begins after Korean gloss
        # Find the first token containing Kana, Kanji, or uppercase acronym (PT, AoE, DPS)
        tokens = re.split(r"[\s\u3000]+", stripped)
        split_idx = None
        for idx, tok in enumerate(tokens):
            if idx == 0:
                continue
            if RE_KANA.search(tok) or RE_CJK.search(tok) or re.fullmatch(r"(?:PT|AoE|DPS|CF|PF|RF|ID|MT|ST|D[1-4]|H[12])", tok):
                split_idx = idx
                break
        if split_idx is None:
            drop_counts["ff14_terms_md:no_split"] += 1
            continue
        ko_gloss = clean_community_gloss(" ".join(tokens[:split_idx]))
        # Drop long explanatory sentences pretending to be glosses
        if len(ko_gloss) > 20 or not RE_HANGUL.search(ko_gloss):
            drop_counts["ff14_terms_md:verbose_or_invalid_gloss"] += 1
            continue

        ja_tokens = []
        for tok in tokens[split_idx:]:
            if RE_HANGUL.search(tok) or tok in ("등등", "혹은"):
                break
            ja_tokens.append(tok)

        for variant in split_ja_variants("\n".join(ja_tokens)):
            if len(variant) < 2:
                drop_counts["ff14_terms_md:too_short"] += 1
                continue
            reason = validate_pair(
                "ja",
                "ko",
                variant,
                ko_gloss,
                max_source_len=36,
                max_target_len=24,
                min_ja_len=2,
                filter_generic_stopwords=True,
            )
            if reason:
                drop_counts[f"ff14_terms_md:{reason}"] += 1
                continue
            entries.append(
                TermEntry(
                    pair="Japanese->Korean",
                    source=variant,
                    target=ko_gloss,
                    tier=2,
                    origin="drive:ff14_terms_md",
                    category="community_raid_slang",
                    include_in_runtime=True,
                )
            )
    return entries


def extract_curated_community_json(curated_path: Path) -> list[TermEntry]:
    """Load hand-curated Tier-2 community glossary JSON."""
    data = json.loads(curated_path.read_text(encoding="utf-8"))
    entries: list[TermEntry] = []
    for pair_key, items in data.get("pairs", {}).items():
        for item in items:
            src = normalize_space(str(item.get("source", "")))
            tgt = normalize_space(str(item.get("target", "")))
            category = str(item.get("category", "community_curated"))
            if src and tgt:
                entries.append(
                    TermEntry(
                        pair=pair_key,
                        source=src,
                        target=tgt,
                        tier=2,
                        origin=f"community_curated:{category}",
                        category=category,
                        include_in_runtime=True,
                    )
                )
    return entries


def extract_user_overrides_json(override_path: Path) -> list[TermEntry]:
    """Load optional manual/curated overrides JSON (`{"pairs": {"Japanese->Korean": [{"source":..., "target":...}]}}`)."""
    data = json.loads(override_path.read_text(encoding="utf-8"))
    entries: list[TermEntry] = []
    for pair_key, items in data.get("pairs", {}).items():
        for item in items:
            src = normalize_space(str(item.get("source", "")))
            tgt = normalize_space(str(item.get("target", "")))
            if src and tgt:
                entries.append(
                    TermEntry(
                        pair=pair_key,
                        source=src,
                        target=tgt,
                        tier=0,
                        origin="user_override",
                        category="curated",
                        include_in_runtime=True,
                    )
                )
    return entries


# ---------------------------------------------------------------------------
# Merging, conflict resolution, and serialization
# ---------------------------------------------------------------------------

def collapse_same_origin_homonyms(
    entries: Iterable[TermEntry],
    drop_counts: Counter[str],
    majority_threshold: float = 0.75,
) -> list[TermEntry]:
    """Resolve 1:N homonyms within the same (pair, tier, origin, source).

    Example: PlaceName.csv maps 'Airship Landing' to 7 different city-specific landings.
    If no single target accounts for >= majority_threshold of occurrences, drop it from
    runtime (or exclude entirely for datamining) rather than picking an arbitrary city.
    """
    grouped: dict[tuple[str, int, str, str], list[TermEntry]] = defaultdict(list)
    for entry in entries:
        key_src = entry.source.casefold() if not entry.pair.startswith("Japanese->") else entry.source
        grouped[(entry.pair, entry.tier, entry.origin, key_src)].append(entry)

    resolved: list[TermEntry] = []
    for (_, tier, origin, _), group in grouped.items():
        target_counts = Counter(e.target for e in group)
        if len(target_counts) == 1:
            resolved.append(group[0])
            continue
        top_target, top_count = target_counts.most_common(1)[0]
        if top_count / len(group) >= majority_threshold:
            chosen = next(e for e in group if e.target == top_target)
            resolved.append(chosen)
        else:
            drop_counts[f"ambiguous_homonym:{origin}"] += 1
    return resolved


def merge_entries(
    entries: Iterable[TermEntry],
    drop_counts: Counter[str] | None = None,
) -> tuple[dict[tuple[str, str], TermEntry], list[ConflictRecord]]:
    """Merge entries by (pair, casefold_or_exact_source) keeping lowest tier; log target conflicts."""
    if drop_counts is None:
        drop_counts = Counter()
    cleaned_entries = collapse_same_origin_homonyms(entries, drop_counts)
    winners: dict[tuple[str, str], TermEntry] = {}
    conflicts: list[ConflictRecord] = []
    seen_conflicts: set[tuple[str, str, str, str]] = set()

    for entry in cleaned_entries:
        # Case-insensitive key for Latin sources, exact for Japanese/Korean
        key_src = entry.source.casefold() if not entry.pair.startswith("Japanese->") else entry.source
        key = (entry.pair, key_src)
        existing = winners.get(key)
        if existing is None:
            winners[key] = entry
            continue

        if existing.target == entry.target:
            # Keep the higher-priority tier / runtime flag if same target
            if entry.tier < existing.tier:
                winners[key] = entry
            elif entry.include_in_runtime and not existing.include_in_runtime:
                winners[key] = TermEntry(
                    pair=existing.pair,
                    source=existing.source,
                    target=existing.target,
                    tier=existing.tier,
                    origin=existing.origin,
                    category=existing.category,
                    include_in_runtime=True,
                )
            continue

        # Target mismatch: lower tier number wins
        if entry.tier < existing.tier:
            winner, loser = entry, existing
            winners[key] = entry
        else:
            winner, loser = existing, entry

        ckey = (winner.pair, winner.source, winner.target, loser.target)
        if ckey not in seen_conflicts:
            seen_conflicts.add(ckey)
            conflicts.append(
                ConflictRecord(
                    pair=winner.pair,
                    source=winner.source,
                    winner_target=winner.target,
                    winner_tier=winner.tier,
                    winner_origin=winner.origin,
                    loser_target=loser.target,
                    loser_tier=loser.tier,
                    loser_origin=loser.origin,
                )
            )

    return winners, conflicts


def build_glossary_payload(
    winners: dict[tuple[str, str], TermEntry],
    *,
    version: str,
    runtime_only: bool,
    include_metadata: bool = False,
) -> dict:
    pairs_out: dict[str, list[dict]] = defaultdict(list)
    for entry in winners.values():
        if runtime_only and not entry.include_in_runtime:
            continue
        item = {"source": entry.source, "target": entry.target}
        if include_metadata:
            item["tier"] = entry.tier
            item["category"] = entry.category
            item["origin"] = entry.origin
        pairs_out[entry.pair].append(item)

    # Sort deterministically: longest source first (matches longest-match priority), then lexicographic
    sorted_pairs: dict[str, list[dict]] = {}
    for pair_key in sorted(pairs_out.keys()):
        sorted_pairs[pair_key] = sorted(
            pairs_out[pair_key],
            key=lambda x: (-len(x["source"]), x["source"]),
        )

    return {
        "version": version,
        "pairs": sorted_pairs,
    }


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Build FFXIV translation glossaries for MiLMMT.")
    parser.add_argument("--datamining-dir", type=Path, help="Path to ffxiv-datamining repository root.")
    parser.add_argument(
        "--community-curated-json",
        type=Path,
        help="Recommended Tier-2 curated community glossary JSON (replaces raw Drive auto-extraction).",
    )
    parser.add_argument("--koala-xlsx", type=Path, help="(Draft/phrases) Exported Koala FF14 Japanese note .xlsx file.")
    parser.add_argument("--jclient-xlsx", type=Path, help="(Draft) Exported J클라 게임 용어 정리 .xlsx file.")
    parser.add_argument("--ff14-terms-md", type=Path, help="(Draft) Exported '파판 용어' .md file.")
    parser.add_argument("--overrides-json", type=Path, help="Optional Tier-0 curated overrides .json file.")
    parser.add_argument(
        "--source-langs",
        nargs="+",
        default=list(DEFAULT_SOURCE_LANGS),
        choices=list(LANG_CODE_TO_NAME.keys()),
        help="Source language codes to extract from ffxiv-datamining.",
    )
    parser.add_argument(
        "--target-lang",
        default=DEFAULT_TARGET_LANG,
        choices=list(LANG_CODE_TO_NAME.keys()),
        help="Target language code.",
    )
    parser.add_argument("--version", default=dt.date.today().strftime("%Y-%m-%d.1"), help="Glossary version string.")
    parser.add_argument("--out-dir", type=Path, required=True, help="Output directory for JSON files.")
    args = parser.parse_args(argv)

    drop_counts: Counter[str] = Counter()
    all_entries: list[TermEntry] = []
    phrase_pairs: list[dict] = []

    if args.overrides_json and args.overrides_json.exists():
        all_entries.extend(extract_user_overrides_json(args.overrides_json))
    if args.datamining_dir:
        all_entries.extend(
            extract_datamining_terms(
                args.datamining_dir,
                source_langs=args.source_langs,
                target_lang=args.target_lang,
                drop_counts=drop_counts,
            )
        )
    if args.community_curated_json and args.community_curated_json.exists():
        all_entries.extend(extract_curated_community_json(args.community_curated_json))
    else:
        if args.koala_xlsx and args.koala_xlsx.exists():
            all_entries.extend(extract_koala_xlsx(args.koala_xlsx, drop_counts, phrase_pairs=phrase_pairs))
        if args.jclient_xlsx and args.jclient_xlsx.exists():
            all_entries.extend(extract_jclient_terms_xlsx(args.jclient_xlsx, drop_counts))
        if args.ff14_terms_md and args.ff14_terms_md.exists():
            all_entries.extend(extract_ff14_terms_md(args.ff14_terms_md, drop_counts))

    winners, conflicts = merge_entries(all_entries, drop_counts=drop_counts)

    # Deduplicate exported community phrase pairs
    seen_phrases: set[tuple[str, str]] = set()
    unique_phrases: list[dict] = []
    for p in phrase_pairs:
        pk = (p["source_text"], p["target_text"])
        if pk not in seen_phrases:
            seen_phrases.add(pk)
            unique_phrases.append(p)

    runtime_payload = build_glossary_payload(winners, version=args.version, runtime_only=True, include_metadata=False)
    full_payload = build_glossary_payload(winners, version=args.version, runtime_only=False, include_metadata=True)

    by_origin = Counter(e.origin for e in winners.values())
    by_tier = Counter(f"tier_{e.tier}" for e in winners.values())
    by_pair_runtime = {k: len(v) for k, v in runtime_payload["pairs"].items()}
    by_pair_full = {k: len(v) for k, v in full_payload["pairs"].items()}

    report = {
        "version": args.version,
        "raw_candidates": len(all_entries),
        "unique_terms_full": len(winners),
        "unique_terms_runtime": sum(by_pair_runtime.values()),
        "community_phrase_pairs": len(unique_phrases),
        "by_pair_runtime": by_pair_runtime,
        "by_pair_full": by_pair_full,
        "by_tier_full": dict(sorted(by_tier.items())),
        "by_origin_full": dict(by_origin.most_common()),
        "drop_counts": dict(drop_counts.most_common()),
        "conflict_count": len(conflicts),
        "conflicts_sample_top_100": [
            {
                "pair": c.pair,
                "source": c.source,
                "winner": {"target": c.winner_target, "tier": c.winner_tier, "origin": c.winner_origin},
                "loser": {"target": c.loser_target, "tier": c.loser_tier, "origin": c.loser_origin},
            }
            for c in sorted(conflicts, key=lambda x: (x.winner_tier, x.loser_tier, x.pair, x.source))[:100]
        ],
    }

    args.out_dir.mkdir(parents=True, exist_ok=True)
    (args.out_dir / "glossary.runtime.json").write_text(
        json.dumps(runtime_payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (args.out_dir / "glossary.full.json").write_text(
        json.dumps(full_payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (args.out_dir / "glossary.report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    if unique_phrases:
        (args.out_dir / "community_phrases.jsonl").write_text(
            "\n".join(json.dumps(p, ensure_ascii=False) for p in unique_phrases) + "\n",
            encoding="utf-8",
        )

    print(
        f"Built glossary {args.version}: runtime={report['unique_terms_runtime']:,}, "
        f"full={report['unique_terms_full']:,}, phrases={report['community_phrase_pairs']:,}, "
        f"conflicts={report['conflict_count']:,}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
