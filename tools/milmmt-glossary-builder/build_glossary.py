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

from korean_style import (
    KOREAN_SPEECH_STYLES,
    KOREAN_SPEECH_STYLE_SET,
    classify_korean_sentence_style,
    infer_npc_dominant_style,
)

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
GENERIC_KO_STOPWORDS = frozenset({
    "기술", "주택", "자택", "변신", "차원", "환영", "응원", "뒷문", "정제", "무효",
    "미끼", "표식", "시작", "종료", "확인", "취소", "변경", "설정", "삭제", "추가",
    "저장", "검색", "이동", "실행", "선택", "표시", "이전", "다음", "닫기", "열기",
    "공격", "방어", "대상", "연속", "범위", "기준", "필요", "가능", "조정", "모집",
    "완료", "실패", "성공", "대기", "준비", "진행", "일반", "보통", "어려움", "극한",
    "전투", "휴식", "대화", "교환", "거래", "상점", "보상", "경험", "시간", "장소",
    "이름", "목록", "정보", "상태", "효과", "강화", "약화", "회복", "부활", "사망",
    "승리", "패배", "입장", "퇴장", "탈퇴", "가입", "초대", "추방", "차단", "해제",
    "수락", "거절", "동의", "반대", "질문", "답변", "설명", "안내", "경고", "주의",
    "위험", "안전", "무기", "방어구", "장비", "도구", "재료", "음식", "약품", "물약",
    "날씨", "맑음", "흐림", "안개", "바람", "번개", "눈보라", "폭풍", "쾌청",
})
REVERSE_CORE_CATEGORIES = frozenset({
    "curated", "class_job", "grand_company", "race", "tribe", "deity", "place_name", "duty",
})
REVERSE_EXTENDED_CATEGORIES = frozenset({
    "status", "trait", "ui_command", "general_action",
})
RE_REVERSE_NOISY_TARGET = re.compile(r"(?:コンテンツ名は|「[^」]+」)")
# Latin-script UI/status/weather/completion categories where bare single words are overwhelmingly
# ordinary prose words (e.g. "Excellent" -> "최고품질", "Ready" -> "훈련 가능", "Fever" -> "정신 바이러스")
# and hijack normal NPC dialogue. Multi-word terms remain in runtime; curated single-word FFXIV terms
# live in community_curated.json.
LATIN_NOISY_SINGLE_WORD_CATEGORIES = frozenset({
    "addon_ui",
    "completion",
    "status",
    "trait",
    "ui_command",
    "general_action",
    "weather",
})

# Common English dictionary words that exist as FFXIV PlaceName rows (or article-stripped "The ..."
# aliases) and cause severe false-positive overrides in ordinary English dialogue (e.g. "stern faces"
# matching "Stern => 위층", "in silence" matching "Silence => 고요한 얼음 동굴").
PLACE_NAME_EN_STOPWORDS = frozenset({
    "abacus", "amity", "approach", "armory", "arsenal", "articulation", "asylum", "atelier",
    "atrium", "auction", "backroom", "ballroom", "barber", "baron", "barracks", "barrel",
    "basement", "bastion", "bedrock", "beehive", "belfry", "bellows", "beyond", "blanch",
    "bonfire", "brick", "bridge", "bridges", "bruise", "bunker", "buttress", "canopy",
    "capitol", "cellar", "cenotaph", "chancel", "chantry", "chisel", "choler", "cleric",
    "clutch", "coliseum", "confessional", "contempt", "corridor", "crater", "creation",
    "crick", "daggers", "darker", "depths", "derrick", "descent", "dilemma", "dungeon",
    "dungeons", "echelon", "eddies", "emergent", "enlightenment", "entropy", "exit",
    "flagship", "flurry", "foyer", "fringes", "fumarole", "furnace", "gallery", "gangway",
    "gauntlet", "goblet", "guidance", "gullet", "halo", "hangar", "harmony", "helix",
    "hemlock", "horizon", "insides", "insight", "knot", "knowable", "ladder", "ladle",
    "landing", "library", "lonely", "lunar", "mainstay", "manifest", "mattock", "mirror",
    "mist", "mosaic", "mourn", "myth", "oblivion", "orchard", "outsides", "parapet",
    "passage", "pendants", "pillars", "pitch", "playroom", "plenty", "porter", "proof",
    "pylons", "raid", "repairs", "repurposement", "reunion", "ribbons", "rooftop", "rookery",
    "rostrum", "rotunda", "ruiner", "scabbard", "scales", "schism", "scree", "seedbed",
    "sentinel", "serenity", "shaft", "shallow", "shallows", "shelter", "shop", "silence",
    "snitch", "solace", "sorrow", "spence", "stacks", "stern", "sullen", "tangle",
    "tempest", "terminus", "throttle", "trader", "transcendence", "trenches", "tribunal",
    "twine", "twinning", "twitch", "ultimacy", "unmaking", "vanguard", "vortex", "warmth",
    "warrens", "watcher", "weed", "wishbone", "wright", "zenith", "ziggurat", "zymology",
})

# Common words that are valid NPC speaker names (kept in exact `npc_names`),
# but should not trigger substring `Glossary:` prompt injection inside normal prose.
NPC_SUBSTRING_EN_STOPWORDS = frozenset({
    "amber", "angel", "april", "august", "baker", "black", "blaze", "brave",
    "brook", "brother", "brown", "butler", "captain", "chief", "child", "clerk",
    "crown", "devil", "doctor", "dream", "dreamer", "elder", "father", "first",
    "fisher", "flame", "forest", "frost", "ghost", "golden", "grace", "grand",
    "great", "green", "guard", "guide", "happy", "heart", "hunter", "junior",
    "keeper", "knight", "leader", "light", "local", "lucky", "maiden", "major",
    "master", "merry", "miner", "minor", "mother", "night", "noble", "officer",
    "order", "pearl", "prince", "queen", "quiet", "raven", "river", "royal",
    "runner", "saint", "scout", "second", "seeker", "senior", "shadow", "sharp",
    "short", "silver", "singer", "sister", "small", "smith", "soldier", "spark",
    "speaker", "spirit", "steel", "stone", "storm", "stranger", "sunny", "sweet",
    "swift", "third", "tower", "traveler", "visitor", "voice", "waiter", "walker",
    "wanderer", "watcher", "white", "worker", "writer", "youth",
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

    # Identify proper-noun ENpcResident row IDs using English EXD metadata (Article == 1 and capitalized).
    proper_npc_row_ids: set[str] = set()
    en_npc_csv = resolve_lang_csv_dir(datamining_root, "en") / "ENpcResident.csv"
    if en_npc_csv.exists():
        en_npc_singular = load_csv_column(en_npc_csv, "Singular")
        en_npc_article = load_csv_column(en_npc_csv, "Article")
        proper_npc_row_ids = {
            rid
            for rid, article in en_npc_article.items()
            if article == "1"
            and rid in en_npc_singular
            and en_npc_singular[rid].strip()
            and en_npc_singular[rid].strip()[0].isupper()
        }

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

                include_runtime = spec.include_in_runtime
                entry_tier = spec.tier
                if src_lang in ("en", "de", "fr") and include_runtime:
                    is_single_word = (" " not in src_clean and "-" not in src_clean)
                    if is_single_word and spec.category in LATIN_NOISY_SINGLE_WORD_CATEGORIES:
                        include_runtime = False
                        drop_counts[f"runtime_latin_single:{spec.category}"] += 1
                    elif src_lang == "en" and spec.category == "place_name" and src_clean.casefold() in PLACE_NAME_EN_STOPWORDS:
                        include_runtime = False
                        drop_counts["runtime_place_name_en_stopword"] += 1
                if spec.filename == "ENpcResident.csv" and row_id in proper_npc_row_ids:
                    if src_lang == "ja":
                        if len(src_clean) >= 3:
                            include_runtime = True
                            entry_tier = 3
                    elif src_lang in ("en", "de", "fr"):
                        has_compound_marker = any(ch in src_clean for ch in ("'", "-", " "))
                        if (len(src_clean) >= 5 or has_compound_marker) and src_clean.casefold() not in NPC_SUBSTRING_EN_STOPWORDS:
                            include_runtime = True
                            entry_tier = 3

                entries.append(
                    TermEntry(
                        pair=pair_key,
                        source=src_clean,
                        target=tgt_clean,
                        tier=entry_tier,
                        origin=origin,
                        category=spec.category,
                        include_in_runtime=include_runtime,
                    )
                )
                # For English proper-noun places starting with "The " (e.g. "The Mizzenmast"),
                # also emit an article-free alias at slightly lower priority (tier + 1) when long enough.
                if src_lang == "en" and spec.filename == "PlaceName.csv":
                    m_the = RE_LEADING_THE.match(src_clean)
                    if m_the:
                        bare = m_the.group(1).strip()
                        bare_low = bare.casefold()
                        if (
                            len(bare) >= 6
                            and bare_low not in GENERIC_EN_STOPWORDS
                            and bare_low not in PLACE_NAME_EN_STOPWORDS
                        ):
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


def extract_npc_names(
    datamining_root: Path,
    source_langs: Sequence[str],
    target_lang: str,
    drop_counts: Counter[str],
) -> dict[str, dict[str, str]]:
    """Extract exact-match NPC/BattleTalk speaker name lookup maps (`ENpcResident` > `BNpcName`)."""
    lang_dirs = {
        lang: resolve_lang_csv_dir(datamining_root, lang)
        for lang in (*source_langs, target_lang)
    }
    raw_entries: list[TermEntry] = []
    npc_sheets = (
        ("ENpcResident.csv", 1, "npc_name"),
        ("BNpcName.csv", 2, "bnpc_name"),
    )

    for filename, tier, category in npc_sheets:
        target_csv = lang_dirs[target_lang] / filename
        if not target_csv.exists():
            continue
        target_map = load_csv_column(target_csv, "Singular")
        if not target_map:
            continue

        for src_lang in source_langs:
            src_csv = lang_dirs[src_lang] / filename
            if not src_csv.exists():
                continue
            src_map = load_csv_column(src_csv, "Singular")
            pair_key = f"{LANG_CODE_TO_NAME[src_lang]}->{LANG_CODE_TO_NAME[target_lang]}"
            origin = f"npc_names:{filename}"

            for row_id, raw_src in src_map.items():
                raw_tgt = target_map.get(row_id)
                if not raw_tgt:
                    continue
                src_clean, src_drop = clean_exd_text(raw_src)
                if src_drop:
                    continue
                tgt_clean, tgt_drop = clean_exd_text(raw_tgt)
                if tgt_drop:
                    continue
                reason = validate_pair(
                    src_lang,
                    target_lang,
                    src_clean,
                    tgt_clean,
                    max_source_len=48,
                    max_target_len=48,
                    min_ja_len=1,
                    min_latin_len=2,
                    disallow_sentence_punct=False,
                    filter_generic_stopwords=False,
                )
                if reason:
                    continue
                raw_entries.append(
                    TermEntry(
                        pair=pair_key,
                        source=src_clean,
                        target=tgt_clean,
                        tier=tier,
                        origin=origin,
                        category=category,
                        include_in_runtime=True,
                    )
                )

    winners, _ = merge_entries(raw_entries, drop_counts=drop_counts)
    grouped: dict[str, list[tuple[str, str]]] = defaultdict(list)
    for entry in winners.values():
        grouped[entry.pair].append((entry.source, entry.target))

    result: dict[str, dict[str, str]] = {}
    for pair_key in sorted(grouped.keys()):
        result[pair_key] = dict(sorted(grouped[pair_key], key=lambda item: item[0]))
    return result


RE_NPC_TOKEN_NORM = re.compile(r"[^A-Za-z0-9]")
RE_QUEST_ALPHA_SEG = re.compile(r"^([A-Z]{3,})\d*$")
RE_QUEST_SKIP_SEG = re.compile(r"^(?:Q|A|B|S|M|F|P|C|T)\d+$|^(?:SCENE|CUT|VOICE)\d*$")
QUEST_SPEAKER_SKIP_WORDS = frozenset({
    "TALK", "NONE", "MSG", "POP", "LOG", "END", "START", "YES", "NO",
    "SELECT", "CHOICE", "SYSTEM", "ACTOR",
})


def _normalize_npc_token(name: str) -> str:
    return RE_NPC_TOKEN_NORM.sub("", name).upper()


def _extract_quest_speaker_token(key: str, known_tokens: set[str]) -> str | None:
    if not key.startswith("TEXT_"):
        return None
    if any(x in key for x in ("_SEQ_", "_TODO_", "_Q1_", "_Q2_", "_Q3_", "_A1_", "_A2_", "_A3_", "_B1_", "_B2_")):
        return None
    parts = key.split("_")
    if len(parts) < 4:
        return None
    alphas: list[str] = []
    for p in parts[3:]:
        if p.isdigit() or RE_QUEST_SKIP_SEG.match(p) or p in QUEST_SPEAKER_SKIP_WORDS:
            continue
        m = RE_QUEST_ALPHA_SEG.match(p)
        if m:
            alphas.append(m.group(1))
    if not alphas:
        return None
    joined = "".join(alphas)
    if joined in known_tokens:
        return joined
    for cand in (alphas[-1], alphas[0]):
        if cand in known_tokens:
            return cand
    return None


def load_npc_style_overrides(override_path: Path | None) -> dict[str, str]:
    """Load curated English NPC name -> canonical Korean speech style overrides."""
    if not override_path or not override_path.exists():
        return {}
    data = json.loads(override_path.read_text(encoding="utf-8"))
    raw = data.get("styles", data) if isinstance(data, dict) else {}
    out: dict[str, str] = {}
    for npc_name, style in raw.items():
        if not isinstance(npc_name, str) or not isinstance(style, str):
            continue
        style_clean = style.strip()
        if style_clean not in KOREAN_SPEECH_STYLE_SET:
            raise ValueError(f"Unknown Korean speech style '{style_clean}' for NPC '{npc_name}'")
        out[normalize_space(npc_name)] = style_clean
    return out


def extract_npc_styles(
    datamining_root: Path,
    source_langs: Sequence[str],
    target_lang: str,
    *,
    overrides_path: Path | None = None,
    min_classified_lines: int = 10,
) -> tuple[dict[str, dict[str, str]], dict[str, int]]:
    """Infer per-NPC Korean speech-style lookup maps from official KR dialogue + curated overrides.

    Only proper-noun named NPCs (`ENpcResident` `Article == 1` + capitalized English `Singular`,
    plus curated overrides) are included so generic role names ('guard', 'merchant') never get
    an arbitrary single persona.
    """
    if target_lang != "ko":
        return {}, {}

    ko_dir = resolve_lang_csv_dir(datamining_root, "ko")
    en_dir = resolve_lang_csv_dir(datamining_root, "en")
    en_npc_csv = en_dir / "ENpcResident.csv"
    ko_npc_csv = ko_dir / "ENpcResident.csv"
    if not (en_npc_csv.exists() and ko_npc_csv.exists()):
        return {}, {}

    en_singular = load_csv_column(en_npc_csv, "Singular")
    en_article = load_csv_column(en_npc_csv, "Article")
    ko_singular = load_csv_column(ko_npc_csv, "Singular")

    overrides = load_npc_style_overrides(overrides_path)
    override_cf = {k.casefold(): v for k, v in overrides.items()}

    # Collect proper-noun NPC row IDs grouped by English name
    row_ids_by_en_name: dict[str, list[str]] = defaultdict(list)
    proper_en_names: set[str] = set()
    for rid, raw_en in en_singular.items():
        if rid not in ko_singular:
            continue
        en_clean, drop = clean_exd_text(raw_en)
        if drop or not en_clean:
            continue
        is_proper = en_article.get(rid) == "1" and en_clean[0].isupper()
        is_overridden = en_clean.casefold() in override_cf
        if is_proper or is_overridden:
            row_ids_by_en_name[en_clean].append(rid)
            proper_en_names.add(en_clean)

    # Map normalized quest token -> canonical English name
    token_to_en_name: dict[str, str] = {}
    for en_name in sorted(proper_en_names, key=lambda n: (n.casefold() not in override_cf, len(n), n)):
        tok = _normalize_npc_token(en_name)
        if len(tok) >= 3 and tok not in token_to_en_name:
            token_to_en_name[tok] = en_name
    known_tokens = set(token_to_en_name.keys())

    # 1. Scan KR quest / cutscene / dungeon / raid / custom / content / opening CSVs
    counts_by_en: dict[str, Counter[str]] = defaultdict(Counter)
    for subdir in ("quest", "cut_scene", "custom", "content", "dungeon", "raid", "opening"):
        sub_path = ko_dir / subdir
        if not sub_path.is_dir():
            continue
        for csv_path in sorted(sub_path.rglob("*.csv")):
            with csv_path.open("r", encoding="utf-8", errors="replace", newline="") as handle:
                reader = csv.reader(handle)
                next(reader, None)
                for row in reader:
                    if len(row) < 3 or not row[2]:
                        continue
                    tok = _extract_quest_speaker_token(row[1], known_tokens)
                    if not tok:
                        continue
                    text = row[2]
                    if "<" in text or "Player" in text:
                        continue
                    st = classify_korean_sentence_style(text)
                    if st:
                        counts_by_en[token_to_en_name[tok]][st] += 1

    # 2. Scan ENpcBase -> DefaultTalk linkage
    default_talk_csv = ko_dir / "DefaultTalk.csv"
    enpc_base_csv = ko_dir / "ENpcBase.csv"
    if default_talk_csv.exists() and enpc_base_csv.exists():
        dt_styles: dict[str, list[str]] = {}
        with default_talk_csv.open("r", encoding="utf-8", errors="replace", newline="") as handle:
            reader = csv.reader(handle)
            hdr = next(reader, [])
            text_cols = [i for i, h in enumerate(hdr) if h.startswith("Text[")]
            for row in reader:
                if not row or not row[0].isdigit():
                    continue
                found: list[str] = []
                for ci in text_cols:
                    if ci < len(row) and row[ci] and "<" not in row[ci] and "Player" not in row[ci]:
                        st = classify_korean_sentence_style(row[ci])
                        if st:
                            found.append(st)
                if found:
                    dt_styles[row[0]] = found

        rid_to_proper_en: dict[str, str] = {}
        for en_name, rids in row_ids_by_en_name.items():
            for rid in rids:
                rid_to_proper_en[rid] = en_name

        with enpc_base_csv.open("r", encoding="utf-8", errors="replace", newline="") as handle:
            reader = csv.reader(handle)
            hdr = next(reader, [])
            data_cols = [i for i, h in enumerate(hdr) if h.startswith("ENpcData[")]
            for row in reader:
                if not row or row[0] not in rid_to_proper_en:
                    continue
                en_name = rid_to_proper_en[row[0]]
                for ci in data_cols:
                    if ci < len(row) and row[ci] in dt_styles:
                        for st in dt_styles[row[ci]]:
                            counts_by_en[en_name][st] += 1

    # 3. Infer dominant style per English proper-noun NPC and apply curated overrides
    resolved_en: dict[str, tuple[str, int, bool]] = {}  # en_name -> (style, evidence_count, is_override)
    for en_name in proper_en_names:
        cnt = counts_by_en.get(en_name)
        evidence = sum(cnt.values()) if cnt else 0
        if en_name.casefold() in override_cf:
            resolved_en[en_name] = (override_cf[en_name.casefold()], max(evidence, 1), True)
            continue
        if cnt:
            inferred = infer_npc_dominant_style(cnt, min_classified=min_classified_lines)
            if inferred:
                resolved_en[en_name] = (inferred, evidence, False)

    # Also keep curated overrides even if English name was not found in ENpcResident (defensive)
    for ov_name, ov_style in overrides.items():
        if not any(k.casefold() == ov_name.casefold() for k in resolved_en):
            resolved_en[ov_name] = (ov_style, 1, True)

    # 4. Project resolved styles across requested source languages via ENpcResident row IDs
    lang_singular_maps: dict[str, dict[str, str]] = {}
    for src_lang in source_langs:
        if src_lang == "en":
            lang_singular_maps[src_lang] = en_singular
        else:
            p = resolve_lang_csv_dir(datamining_root, src_lang) / "ENpcResident.csv"
            lang_singular_maps[src_lang] = load_csv_column(p, "Singular") if p.exists() else {}

    npc_styles: dict[str, dict[str, str]] = {}
    for src_lang in source_langs:
        pair_key = f"{LANG_CODE_TO_NAME[src_lang]}->{LANG_CODE_TO_NAME[target_lang]}"
        src_map = lang_singular_maps[src_lang]
        # Track best (is_override, evidence) per normalized source speaker name
        best_for_src: dict[str, tuple[str, str, bool, int]] = {}  # key_cf -> (display_name, style, is_ov, evidence)

        for en_name, (style, evidence, is_ov) in resolved_en.items():
            rids = row_ids_by_en_name.get(en_name, [])
            names_in_lang: set[str] = set()
            if src_lang == "en":
                names_in_lang.add(en_name)
            for rid in rids:
                raw_name = src_map.get(rid, "")
                cleaned, drop = clean_exd_text(raw_name)
                if not drop and cleaned:
                    names_in_lang.add(cleaned)
            for name in names_in_lang:
                k = name if src_lang == "ja" else name.casefold()
                prev = best_for_src.get(k)
                if prev is None or (is_ov, evidence) > (prev[2], prev[3]):
                    best_for_src[k] = (name, style, is_ov, evidence)

        if best_for_src:
            npc_styles[pair_key] = dict(
                sorted((v[0], v[1]) for v in best_for_src.values())
            )

    style_dist = Counter(v[0] for v in resolved_en.values())
    return npc_styles, {st: style_dist.get(st, 0) for st in KOREAN_SPEECH_STYLES}


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


def build_reverse_entries(
    winners: dict[tuple[str, str], TermEntry],
    *,
    reverse_target_langs: Sequence[str],
    drop_counts: Counter[str] | None = None,
) -> list[TermEntry]:
    """Derive deterministic, high-precision Korean->* reverse glossary entries from forward winners."""
    if drop_counts is None:
        drop_counts = Counter()

    allowed_targets = {LANG_CODE_TO_NAME[c] for c in reverse_target_langs if c in LANG_CODE_TO_NAME and c != "ko"}
    if not allowed_targets:
        return []

    grouped: dict[tuple[str, str], list[TermEntry]] = defaultdict(list)
    for entry in winners.values():
        if not entry.include_in_runtime or "->" not in entry.pair:
            continue
        src_name, dst_name = entry.pair.split("->", 1)
        if dst_name != "Korean" or src_name not in allowed_targets:
            continue

        ko_src = entry.target.strip()
        rev_tgt = entry.source.strip()
        if not ko_src or not rev_tgt or ko_src.casefold() == rev_tgt.casefold():
            drop_counts["reverse:empty_or_identical"] += 1
            continue
        if not RE_HANGUL.search(ko_src) or RE_KANA.search(ko_src):
            drop_counts["reverse:invalid_ko_source"] += 1
            continue
        if ko_src in GENERIC_KO_STOPWORDS:
            drop_counts["reverse:generic_ko_stopword"] += 1
            continue
        if RE_REVERSE_NOISY_TARGET.search(rev_tgt):
            drop_counts["reverse:noisy_target"] += 1
            continue

        # Allow 2-char Korean sources only for high-precision core FFXIV categories or curated Tier 0/2 terms.
        if entry.tier in (0, 2) or entry.category in REVERSE_CORE_CATEGORIES:
            min_ko_len = 2
        elif entry.category in REVERSE_EXTENDED_CATEGORIES:
            min_ko_len = 3
        else:
            drop_counts[f"reverse:excluded_category:{entry.category}"] += 1
            continue

        if len(ko_src) < min_ko_len:
            drop_counts["reverse:ko_too_short"] += 1
            continue

        grouped[(f"Korean->{src_name}", ko_src)].append(entry)

    reverse_entries: list[TermEntry] = []
    for (rev_pair, ko_src), candidates in sorted(grouped.items()):
        # Deterministically pick the cleanest canonical target:
        # lower tier -> core category -> no leading "The "/"the " -> shorter canonical form -> lexicographic.
        candidates.sort(
            key=lambda e: (
                e.tier,
                0 if e.category in REVERSE_CORE_CATEGORIES else 1,
                1 if RE_LEADING_THE.match(e.source) or e.source.startswith("the ") else 0,
                len(e.source),
                e.source,
            )
        )
        best = candidates[0]
        reverse_entries.append(
            TermEntry(
                pair=rev_pair,
                source=ko_src,
                target=best.source,
                tier=best.tier,
                origin=f"reverse:{best.origin}",
                category=best.category,
                include_in_runtime=True,
            )
        )

    return reverse_entries


def build_glossary_payload(
    winners: dict[tuple[str, str], TermEntry],
    *,
    version: str,
    runtime_only: bool,
    include_metadata: bool = False,
    npc_names: dict[str, dict[str, str]] | None = None,
    npc_styles: dict[str, dict[str, str]] | None = None,
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

    payload: dict = {
        "version": version,
        "pairs": sorted_pairs,
    }
    if npc_names:
        payload["npc_names"] = npc_names
    if npc_styles:
        payload["npc_styles"] = npc_styles
    return payload


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
        "--npc-style-overrides-json",
        type=Path,
        default=Path(__file__).parent / "npc_style_overrides.json",
        help="Curated NPC speech-style overrides JSON.",
    )
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
    parser.add_argument(
        "--reverse-target-langs",
        nargs="*",
        default=[],
        choices=["ja", "en", "de", "fr"],
        help="Optional target languages for derived Korean->* reverse runtime glossary pairs (e.g. --reverse-target-langs ja en).",
    )
    parser.add_argument("--version", default=dt.date.today().strftime("%Y-%m-%d.1"), help="Glossary version string.")
    parser.add_argument("--out-dir", type=Path, required=True, help="Output directory for JSON files.")
    args = parser.parse_args(argv)

    drop_counts: Counter[str] = Counter()
    all_entries: list[TermEntry] = []
    phrase_pairs: list[dict] = []

    if args.overrides_json and args.overrides_json.exists():
        all_entries.extend(extract_user_overrides_json(args.overrides_json))
    npc_names: dict[str, dict[str, str]] = {}
    npc_styles: dict[str, dict[str, str]] = {}
    npc_style_dist: dict[str, int] = {}
    if args.datamining_dir:
        all_entries.extend(
            extract_datamining_terms(
                args.datamining_dir,
                source_langs=args.source_langs,
                target_lang=args.target_lang,
                drop_counts=drop_counts,
            )
        )
        npc_names = extract_npc_names(
            args.datamining_dir,
            source_langs=args.source_langs,
            target_lang=args.target_lang,
            drop_counts=drop_counts,
        )
        npc_styles, npc_style_dist = extract_npc_styles(
            args.datamining_dir,
            source_langs=args.source_langs,
            target_lang=args.target_lang,
            overrides_path=args.npc_style_overrides_json,
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
    reverse_entries = build_reverse_entries(
        winners,
        reverse_target_langs=args.reverse_target_langs,
        drop_counts=drop_counts,
    )
    for rev in reverse_entries:
        winners[(rev.pair, rev.source)] = rev

    # Deduplicate exported community phrase pairs
    seen_phrases: set[tuple[str, str]] = set()
    unique_phrases: list[dict] = []
    for p in phrase_pairs:
        pk = (p["source_text"], p["target_text"])
        if pk not in seen_phrases:
            seen_phrases.add(pk)
            unique_phrases.append(p)

    runtime_payload = build_glossary_payload(
        winners,
        version=args.version,
        runtime_only=True,
        include_metadata=False,
        npc_names=npc_names,
        npc_styles=npc_styles,
    )
    full_payload = build_glossary_payload(
        winners,
        version=args.version,
        runtime_only=False,
        include_metadata=True,
        npc_names=npc_names,
        npc_styles=npc_styles,
    )

    by_origin = Counter(e.origin for e in winners.values())
    by_tier = Counter(f"tier_{e.tier}" for e in winners.values())
    by_pair_runtime = {k: len(v) for k, v in runtime_payload["pairs"].items()}
    by_pair_full = {k: len(v) for k, v in full_payload["pairs"].items()}
    by_pair_npc_names = {k: len(v) for k, v in npc_names.items()}
    by_pair_npc_styles = {k: len(v) for k, v in npc_styles.items()}

    report = {
        "version": args.version,
        "raw_candidates": len(all_entries),
        "unique_terms_full": len(winners),
        "unique_terms_runtime": sum(by_pair_runtime.values()),
        "unique_npc_names": sum(by_pair_npc_names.values()),
        "unique_npc_styles": sum(by_pair_npc_styles.values()),
        "npc_style_distribution_en": npc_style_dist,
        "community_phrase_pairs": len(unique_phrases),
        "by_pair_runtime": by_pair_runtime,
        "by_pair_npc_names": by_pair_npc_names,
        "by_pair_npc_styles": by_pair_npc_styles,
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
    (args.out_dir / "glossary.runtime.min.json").write_text(
        json.dumps(runtime_payload, ensure_ascii=False, separators=(",", ":")), encoding="utf-8"
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
        f"npc_names={report['unique_npc_names']:,}, npc_styles={report['unique_npc_styles']:,}, "
        f"full={report['unique_terms_full']:,}, phrases={report['community_phrase_pairs']:,}, "
        f"conflicts={report['conflict_count']:,}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
