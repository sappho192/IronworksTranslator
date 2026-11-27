import json
import random
import tempfile
import unittest
from pathlib import Path

from build_sft_dataset import (
    EvalLeakageGuard,
    FastGlossaryMatcher,
    clean_dialogue_text,
    is_valid_parallel,
    render_prompt,
)


class BuildSftDatasetTests(unittest.TestCase):
    def test_render_prompt_matches_verified_milmmt_format_without_leading_space(self):
        plain = render_prompt("ja", "ko", "  こんにちは  ")
        self.assertEqual(
            plain,
            "Translate this from Japanese to Korean:\nJapanese: こんにちは\nKorean:",
        )
        with_gl = render_prompt("en", "ko", "Welcome to Limsa Lominsa!", [("Limsa Lominsa", "림사 로민사")])
        self.assertEqual(
            with_gl,
            "Glossary:\nLimsa Lominsa => 림사 로민사\n\n"
            "Translate this from English to Korean:\nEnglish: Welcome to Limsa Lominsa!\nKorean:",
        )

    def test_clean_dialogue_text_strips_benign_tags_and_rejects_complex_se_tags(self):
        self.assertEqual(clean_dialogue_text("<Emphasis>リムサ</Emphasis>へようこそ"), "リムサへようこそ")
        self.assertIsNone(clean_dialogue_text("<If(PlayerParameter(4))>A<Else/>B</If>"))
        self.assertIsNone(clean_dialogue_text("テスト用テキスト"))
        self.assertIsNone(clean_dialogue_text("ああああああ"))
        self.assertIsNone(clean_dialogue_text("はあくどそうな表情をした。"))
        self.assertIsNone(clean_dialogue_text("Player 씨가 모은 이빨은 'UNKNOWN개'"))

    def test_glossary_matcher_katakana_boundary_and_counterfactual_swap(self):
        with tempfile.TemporaryDirectory() as tmp:
            gl_path = Path(tmp) / "glossary.full.json"
            gl_path.write_text(
                json.dumps(
                    {
                        "version": "test",
                        "pairs": {
                            "Japanese->Korean": [
                                {"source": "踊り子", "target": "무도가", "tier": 1, "category": "class_job"},
                                {"source": "ステラ", "target": "스텔라", "tier": 4, "category": "action"},
                                {"source": "コンサポ", "target": "임무 지원", "tier": 2, "category": "system_abbrev"},
                            ]
                        },
                    },
                    ensure_ascii=False,
                ),
                encoding="utf-8",
            )
            curated_path = Path(tmp) / "curated.json"
            curated_path.write_text(
                json.dumps(
                    {
                        "pairs": {
                            "Japanese->Korean": [
                                {"source": "コンサポ", "target": "콘텐츠 서포터", "kr_official": "임무 지원"}
                            ]
                        }
                    },
                    ensure_ascii=False,
                ),
                encoding="utf-8",
            )
            matcher = FastGlossaryMatcher(gl_path)
            matcher.load_divergence_swaps(curated_path)

            # Katakana boundary check: 'ステラ' must NOT match inside 'カステラ'
            self.assertEqual(
                matcher.find_verified_matches("ja", "ko", "カステラと牛乳", "스텔라와 우유"),
                [],
            )

            # Verified match + counterfactual divergence swap (임무 지원 -> 콘텐츠 서포터)
            matches = matcher.find_verified_matches("ja", "ko", "コンサポで入ります", "임무 지원으로 들어갑니다")
            self.assertEqual(matches, [("コンサポ", "임무 지원", "system_abbrev")])
            swapped_tgt, swapped_terms, did_swap = matcher.apply_counterfactual_swap(
                "ja", "ko", "임무 지원으로 들어갑니다", matches, random.Random(1)
            )
            self.assertTrue(did_swap)
            self.assertEqual(swapped_tgt, "콘텐츠 서포터으로 들어갑니다")
            self.assertEqual(swapped_terms, [("コンサポ", "콘텐츠 서포터")])

    def test_eval_leakage_guard_blocks_exact_and_near_duplicates(self):
        with tempfile.TemporaryDirectory() as tmp:
            ev = Path(tmp) / "eval.jsonl"
            ev.write_text(
                json.dumps(
                    {
                        "source_language": "Japanese",
                        "target_language": "Korean",
                        "source_text": "ギアセット20「踊り子」に装備変更しました。",
                    },
                    ensure_ascii=False,
                )
                + "\n",
                encoding="utf-8",
            )
            guard = EvalLeakageGuard(ev, jaccard_threshold=0.70)
            self.assertTrue(guard.is_leaked("ja", "ko", "ギアセット20「踊り子」に装備変更しました。"))
            self.assertTrue(guard.is_leaked("ja", "ko", "ギアセット21「踊り子」に装備変更しました。"))
            self.assertFalse(guard.is_leaked("ja", "ko", "全く別の文章です。"))

    def test_is_valid_parallel(self):
        self.assertTrue(is_valid_parallel("ja", "ko", "こんにちは", "안녕하세요"))
        self.assertFalse(is_valid_parallel("ja", "ko", "攻略手帳", "攻略帳"))
        self.assertFalse(is_valid_parallel("en", "ko", "ゴールドソーサー", "골드 소서"))


if __name__ == "__main__":
    unittest.main()
