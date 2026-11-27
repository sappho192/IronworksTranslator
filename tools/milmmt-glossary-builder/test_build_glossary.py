import json
import tempfile
import unittest
from collections import Counter
from pathlib import Path

from build_glossary import (
    SheetSpec,
    TermEntry,
    build_glossary_payload,
    clean_community_gloss,
    clean_exd_text,
    extract_curated_community_json,
    extract_datamining_terms,
    extract_ff14_terms_md,
    merge_entries,
    split_ja_variants,
    validate_pair,
)


class BuildGlossaryTests(unittest.TestCase):
    def test_clean_exd_text_strips_brackets_and_rejects_se_tags_and_dummies(self):
        self.assertEqual(clean_exd_text("【言葉】"), ("言葉", None))
        self.assertEqual(clean_exd_text("*The Merchant's Tale*"), ("The Merchant's Tale", None))
        self.assertEqual(clean_exd_text("踊り子"), ("踊り子", None))
        self.assertEqual(clean_exd_text("<If(PlayerParameter(4))>A<Else/>B</If>")[1], "has_se_tag")
        self.assertEqual(clean_exd_text("テスト用トークテキスト2")[1], "dummy_pattern")
        self.assertEqual(clean_exd_text("라인1\n라인2")[1], "multiline")
        self.assertEqual(clean_exd_text("12345")[1], "symbols_or_digits")

    def test_validate_pair_filters_untranslated_and_script_mismatch(self):
        self.assertIsNone(validate_pair("ja", "ko", "踊り子", "무도가"))
        self.assertIsNone(validate_pair("en", "ko", "Limsa Lominsa", "림사 로민사"))
        # Untranslated EN cell containing Japanese (observed in real PlaceName.csv#503)
        self.assertEqual(validate_pair("en", "ko", "ゴールドソーサー", "골드 소서"), "latin_source_has_cjk")
        # Target contains Japanese kana or no Hangul
        self.assertEqual(validate_pair("ja", "ko", "攻略手帳", "攻略帳"), "target_no_hangul")
        self.assertEqual(validate_pair("ja", "ko", "攻略手帳", "공략す첩"), "target_has_kana")
        # Generic stopword filtering
        self.assertEqual(
            validate_pair("ja", "ko", "戻る", "돌아가기", filter_generic_stopwords=True),
            "generic_stopword",
        )
        # Sentence punctuation disallowed for Addon.csv
        self.assertEqual(
            validate_pair("ja", "ko", "選択してください。", "선택해 주세요.", disallow_sentence_punct=True),
            "sentence_punct",
        )

    def test_community_parsers_and_variant_splitting(self):
        self.assertEqual(clean_community_gloss("(2상을 전제) 파밍 (주의)"), "파밍")
        self.assertEqual(split_ja_variants("誤爆(ごばく)/ごば"), ["誤爆", "ごば"])
        self.assertEqual(split_ja_variants("？時間 / 早期解散"), ["早期解散"])

        with tempfile.TemporaryDirectory() as tmp:
            md_path = Path(tmp) / "terms.md"
            md_path.write_text(
                "괜찮습니다 どま　どまい\n"
                "준확 レディチェ\n"
                "파밍 消化 (2상을 전제하는 단어)\n"
                "~트쫑 ~滅解散\n"
                "파티 PT\n",
                encoding="utf-8",
            )
            drops: Counter[str] = Counter()
            entries = extract_ff14_terms_md(md_path, drops)
            got = {(e.source, e.target) for e in entries}
            self.assertIn(("どま", "괜찮습니다"), got)
            self.assertIn(("どまい", "괜찮습니다"), got)
            self.assertIn(("レディチェ", "준확"), got)
            self.assertIn(("消化", "파밍"), got)
            self.assertIn(("PT", "파티"), got)
            # Placeholder tilde variant excluded
            self.assertNotIn(("~滅解散", "~트쫑"), got)

    def test_merge_entries_tier_priority_and_conflict_logging(self):
        entries = [
            TermEntry("Japanese->Korean", "トライヨラ", "토라이 요라", 2, "drive:koala:🔵용어", "community_term", True),
            TermEntry("Japanese->Korean", "トライヨラ", "툴라이욜라", 1, "datamining:PlaceName.csv", "place_name", True),
            TermEntry("Japanese->Korean", "レストエリア", "휴식 지역", 0, "user_override", "curated", True),
            TermEntry("Japanese->Korean", "レストエリア", "휴식지", 3, "datamining:Addon.csv", "addon_ui", True),
            TermEntry("English->Korean", "Limsa Lominsa", "림사 로민사", 1, "datamining:PlaceName.csv", "place_name", True),
            TermEntry("English->Korean", "limsa lominsa", "림사 로민사", 4, "datamining:Quest.csv", "quest", False),
        ]
        winners, conflicts = merge_entries(entries)
        self.assertEqual(winners[("Japanese->Korean", "トライヨラ")].target, "툴라이욜라")
        self.assertEqual(winners[("Japanese->Korean", "レストエリア")].target, "휴식 지역")
        self.assertEqual(len(conflicts), 2)

        payload = build_glossary_payload(winners, version="2026-11-27.1", runtime_only=True)
        ja_ko = payload["pairs"]["Japanese->Korean"]
        # Longest source first
        self.assertEqual(ja_ko[0]["source"], "レストエリア")
        self.assertEqual(ja_ko[1]["source"], "トライヨラ")

    def test_same_origin_homonyms_are_dropped_when_ambiguous(self):
        entries = [
            TermEntry("English->Korean", "Airship Landing", "그리다니아 비공정 승강장", 1, "datamining:PlaceName.csv", "place_name", True),
            TermEntry("English->Korean", "Airship Landing", "울다하 비공정 승강장", 1, "datamining:PlaceName.csv", "place_name", True),
            TermEntry("English->Korean", "Airship Landing", "림사 로민사 비공정 승강장", 1, "datamining:PlaceName.csv", "place_name", True),
            TermEntry("English->Korean", "Limsa Lominsa", "림사 로민사", 1, "datamining:PlaceName.csv", "place_name", True),
        ]
        drops: Counter[str] = Counter()
        winners, _ = merge_entries(entries, drop_counts=drops)
        self.assertNotIn(("English->Korean", "airship landing"), winners)
        self.assertIn(("English->Korean", "limsa lominsa"), winners)
        self.assertEqual(drops["ambiguous_homonym:datamining:PlaceName.csv"], 1)

    def test_extract_datamining_terms_handles_nested_ko_submodule(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for rel, content in [
                ("csv/ja/ClassJob.csv", "#,Name\n0,\n38,踊り子\n"),
                ("csv/en/ClassJob.csv", "#,Name\n0,\n38,dancer\n"),
                ("csv/ko/csv/ClassJob.csv", "#,Name\n0,\n38,무도가\n"),
            ]:
                p = root / rel
                p.parent.mkdir(parents=True, exist_ok=True)
                p.write_text(content, encoding="utf-8")

            drops: Counter[str] = Counter()
            entries = extract_datamining_terms(
                root,
                source_langs=("ja", "en"),
                target_lang="ko",
                drop_counts=drops,
                sheets=(SheetSpec("ClassJob.csv", "Name", 1, "class_job", True),),
            )
            got = {(e.pair, e.source, e.target) for e in entries}
            self.assertEqual(
                got,
                {
                    ("Japanese->Korean", "踊り子", "무도가"),
                    ("English->Korean", "dancer", "무도가"),
                },
            )

    def test_community_curated_json_has_no_duplicates_or_banned_noise(self):
        curated_path = Path(__file__).parent / "community_curated.json"
        entries = extract_curated_community_json(curated_path)
        self.assertGreaterEqual(len(entries), 200)
        sources = [e.source for e in entries]
        self.assertEqual(len(sources), len(set(sources)), "Duplicate sources found in community_curated.json")
        banned = {"捨てる", "落ちる", "ドマ式麻雀", "D1", "D2", "H1", "H2", "IN", "EW", "様子見"}
        self.assertTrue(banned.isdisjoint(sources))


if __name__ == "__main__":
    unittest.main()
