import json
import tempfile
import unittest
from collections import Counter
from pathlib import Path

from build_glossary import (
    SheetSpec,
    TermEntry,
    build_glossary_payload,
    build_reverse_entries,
    clean_community_gloss,
    clean_exd_text,
    extract_curated_community_json,
    extract_datamining_terms,
    extract_ff14_terms_md,
    extract_npc_styles,
    load_npc_style_overrides,
    merge_entries,
    split_ja_variants,
    validate_pair,
)
from korean_style import (
    KOREAN_SPEECH_STYLE_SET,
    classify_korean_sentence_style,
    infer_npc_dominant_style,
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

    def test_extract_datamining_terms_excludes_noisy_latin_single_words_from_runtime(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for rel, content in [
                ("csv/en/Addon.csv", "#,Text\n1,Excellent\n2,Duty Support\n"),
                ("csv/ko/csv/Addon.csv", "#,Text\n1,최고품질\n2,임무 지원\n"),
                ("csv/en/PlaceName.csv", "#,Name\n1,Stern\n2,The Library\n3,The Mizzenmast\n4,Thavnair\n"),
                ("csv/ko/csv/PlaceName.csv", "#,Name\n1,위층\n2,도서실\n3,미젠마스트\n4,사베네어 섬\n"),
                ("csv/ko/csv/ClassJob.csv", "#,Name\n0,\n"),
            ]:
                p = root / rel
                p.parent.mkdir(parents=True, exist_ok=True)
                p.write_text(content, encoding="utf-8")

            drops: Counter[str] = Counter()
            entries = extract_datamining_terms(
                root,
                source_langs=("en",),
                target_lang="ko",
                drop_counts=drops,
                sheets=(
                    SheetSpec("Addon.csv", "Text", 3, "addon_ui", True, max_source_len=18, max_target_len=24, min_ja_len=3, min_latin_len=5),
                    SheetSpec("PlaceName.csv", "Name", 1, "place_name", True, min_ja_len=2, min_latin_len=4),
                ),
            )
            rt_sources = {e.source for e in entries if e.include_in_runtime}
            self.assertNotIn("Excellent", rt_sources)
            self.assertIn("Duty Support", rt_sources)
            self.assertNotIn("Stern", rt_sources)
            self.assertNotIn("Library", rt_sources)
            self.assertIn("The Library", rt_sources)
            self.assertIn("Mizzenmast", rt_sources)
            self.assertIn("Thavnair", rt_sources)

    def test_community_curated_json_has_no_duplicates_or_banned_noise(self):
        curated_path = Path(__file__).parent / "community_curated.json"
        entries = extract_curated_community_json(curated_path)
        self.assertGreaterEqual(len(entries), 200)
        sources = [e.source for e in entries]
        self.assertEqual(len(sources), len(set(sources)), "Duplicate sources found in community_curated.json")
        banned = {"捨てる", "落ちる", "ドマ式麻雀", "D1", "D2", "H1", "H2", "IN", "EW", "様子見"}
        self.assertTrue(banned.isdisjoint(sources))

    def test_korean_style_classifier_all_8_registers(self):
        self.assertEqual(classify_korean_sentence_style("북녘 땅은 잠시 평안을 얻었습니다."), "하십시오체")
        self.assertEqual(classify_korean_sentence_style("가져오신 재료를 저에게 맡겨주십시오."), "하십시오체")
        self.assertEqual(classify_korean_sentence_style("다들 조심해요! 공격이 옵니다!"), "하십시오체")
        self.assertEqual(classify_korean_sentence_style("요즘 정보를 모으고 있어요."), "해요체")
        self.assertEqual(classify_korean_sentence_style("지금 바로 돌의 집으로 와주셨으면 합니당!"), "애교 존댓말")
        self.assertEqual(classify_korean_sentence_style("빨리 가자, 늦겠어!"), "반말")
        self.assertEqual(classify_korean_sentence_style("앞으로 펼쳐질 일에 대한 준비는 되어 있나?"), "반말")
        self.assertEqual(classify_korean_sentence_style("자, 그럼 출발해볼까!"), "반말")
        self.assertEqual(classify_korean_sentence_style("그 여로에 나도 동행하도록 하지. 흥미롭군."), "해라체")
        self.assertEqual(classify_korean_sentence_style("내가 돌아올 때까지 여기서 기다려라."), "해라체")
        self.assertEqual(classify_korean_sentence_style("어서 이리로 와라."), "해라체")
        self.assertEqual(classify_korean_sentence_style("그러한 이유로 모진 태도를 보였던 걸세."), "하게체")
        self.assertEqual(classify_korean_sentence_style("그럼, 먼저 출발하겠네."), "하게체")
        self.assertEqual(classify_korean_sentence_style("이렇게 먼 곳까지 와줘서 고맙네."), "하게체")
        self.assertEqual(classify_korean_sentence_style("내가 돌아올 때까지 여기서 기다리게."), "하게체")
        self.assertEqual(classify_korean_sentence_style("다 끝나면 에테라이트 광장에서 만나게."), "하게체")
        self.assertEqual(classify_korean_sentence_style("정말로 하늘을 난단 말이오?"), "하오체")
        self.assertEqual(classify_korean_sentence_style("어서 이리로 오시오."), "하오체")
        self.assertEqual(classify_korean_sentence_style("짐은 울다하 제17대 국왕 나나모 울 나모이니라."), "고어체")
        self.assertEqual(classify_korean_sentence_style("감사히 여겨야 할 것이옵니다."), "고어체")
        # Promissive -(으)ㄹ게 remains 반말, not 하게체
        self.assertEqual(classify_korean_sentence_style("조금만 기다릴게."), "반말")
        self.assertEqual(classify_korean_sentence_style("이렇게 먼 곳까지 와줘서 고마워"), "반말")
        self.assertEqual(classify_korean_sentence_style("저기 빛이 보여."), "반말")
        # Common noun false positives rejected
        self.assertIsNone(classify_korean_sentence_style("여기가 약속 장소"))

    def test_build_reverse_entries_disambiguates_and_filters_noise(self):
        entries = [
            TermEntry("Japanese->Korean", "星導山寺院 コンテンツ名は「壊神修行 星導山寺院」", "성도산 사원", 2, "community", "community_term", True),
            TermEntry("Japanese->Korean", "壊神修行 星導山寺院", "성도산 사원", 1, "datamining:ContentFinderCondition.csv", "duty", True),
            TermEntry("Japanese->Korean", "星導山寺院", "성도산 사원", 1, "datamining:PlaceName.csv", "place_name", True),
            TermEntry("English->Korean", "The Aetherochemical Research Facility", "마과학 연구소", 1, "datamining:PlaceName.csv", "place_name", True),
            TermEntry("English->Korean", "Aetherochemical Research Facility", "마과학 연구소", 1, "datamining:ContentFinderCondition.csv", "duty", True),
            TermEntry("Japanese->Korean", "踊り子", "무도가", 1, "datamining:ClassJob.csv", "class_job", True),
            TermEntry("English->Korean", "Illusion", "환영", 1, "datamining:Status.csv", "status", True),
            TermEntry("English->Korean", "Clear Skies", "쾌청", 1, "datamining:Weather.csv", "weather", True),
            TermEntry("Japanese->Korean", "戻る", "돌아가기", 3, "datamining:Addon.csv", "addon_ui", True),
            TermEntry("Japanese->Korean", "秘策", "비책", 1, "datamining:Status.csv", "status", True),
            TermEntry("Japanese->Korean", "霊薬", "환상약", 1, "datamining:Status.csv", "status", True),
        ]
        winners, _ = merge_entries(entries)
        drops: Counter[str] = Counter()
        rev = build_reverse_entries(winners, reverse_target_langs=("ja", "en"), drop_counts=drops)
        got = {(e.pair, e.source): e.target for e in rev}

        # Cleanest shortest canonical target chosen deterministically
        self.assertEqual(got[("Korean->Japanese", "성도산 사원")], "星導山寺院")
        self.assertEqual(got[("Korean->English", "마과학 연구소")], "Aetherochemical Research Facility")
        self.assertEqual(got[("Korean->Japanese", "무도가")], "踊り子")
        # 3-char status kept, 2-char status dropped, generic Korean stopwords & weather/addon_ui excluded
        self.assertEqual(got[("Korean->Japanese", "환상약")], "霊薬")
        self.assertNotIn(("Korean->Japanese", "비책"), got)
        self.assertNotIn(("Korean->English", "환영"), got)
        self.assertNotIn(("Korean->English", "쾌청"), got)
        self.assertNotIn(("Korean->Japanese", "돌아가기"), got)

    def test_npc_style_overrides_and_extraction(self):
        overrides = load_npc_style_overrides(Path(__file__).parent / "npc_style_overrides.json")
        self.assertGreaterEqual(len(overrides), 35)
        self.assertEqual(overrides["Tataru"], "애교 존댓말")
        self.assertEqual(overrides["Y'shtola"], "해요체")
        self.assertEqual(overrides["Urianger"], "하십시오체")
        self.assertEqual(overrides["Gosetsu"], "하오체")
        self.assertEqual(overrides["Nanamo Ul Namo"], "고어체")
        for st in overrides.values():
            self.assertIn(st, KOREAN_SPEECH_STYLE_SET)

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for rel, content in [
                ("csv/en/ENpcResident.csv", "#,Singular,Article\n100,Y'shtola,1\n101,guard,0\n"),
                ("csv/ja/ENpcResident.csv", "#,Singular\n100,ヤ・シュトラ\n101,衛兵\n"),
                ("csv/ko/csv/ENpcResident.csv", "#,Singular\n100,야슈톨라\n101,위병\n"),
                ("csv/ko/csv/ClassJob.csv", "#,Name\n0,\n"),
                (
                    "csv/ko/csv/quest/000/Q001.csv",
                    "#,Key,Text\n"
                    + "\n".join(f"{i},TEXT_Q001_00001_YSHTOLA_000_{i},조심해서 돌아오세요." for i in range(12))
                    + "\n"
                    + "\n".join(f"{i+20},TEXT_Q001_00001_GUARD_000_{i},멈추십시오." for i in range(12))
                    + "\n",
                ),
            ]:
                p = root / rel
                p.parent.mkdir(parents=True, exist_ok=True)
                p.write_text(content, encoding="utf-8")

            styles, dist = extract_npc_styles(root, ("en", "ja"), "ko", overrides_path=None)
            self.assertEqual(styles["English->Korean"]["Y'shtola"], "해요체")
            self.assertEqual(styles["Japanese->Korean"]["ヤ・シュトラ"], "해요체")
            # Generic non-proper NPC ('guard', Article=0) must be excluded
            self.assertNotIn("guard", styles["English->Korean"])
            self.assertEqual(dist["해요체"], 1)


if __name__ == "__main__":
    unittest.main()
