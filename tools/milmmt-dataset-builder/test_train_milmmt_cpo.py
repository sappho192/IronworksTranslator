#!/usr/bin/env python3
"""Unit tests for pure data-prep helpers in train_milmmt_cpo.py (no GPU or model download required)."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from train_milmmt_cpo import (  # noqa: E402
    encode_pair,
    extract_anchor_record,
    extract_cpo_record,
    pad_sequences,
)


class _DummyTokenizer:
    eos_token_id = 1
    bos_token_id = 2

    def encode(self, text: str, add_special_tokens: bool = True) -> list[int]:
        assert not add_special_tokens, "CPO trainer must never enable special tokens"
        return [10 + (ord(ch) % 90) for ch in text]


class TrainMilmmtCpoTests(unittest.TestCase):
    def test_extract_cpo_record_accepts_prerendered_and_curate_reports_schema(self):
        # 1. Pre-rendered schema
        pre = extract_cpo_record(
            {
                "prompt": "Style: 고어체\n\nTranslate this from English to Korean:\nEnglish: Wait here.\nKorean:",
                "chosen": "여기서 기다릴지어다.",
                "rejected": "여기서 기다리시오.",
            }
        )
        self.assertIsNotNone(pre)
        assert pre is not None
        self.assertTrue(pre[0].startswith("Style: 고어체"))
        self.assertEqual(pre[1], "여기서 기다릴지어다.")
        self.assertEqual(pre[2], "여기서 기다리시오.")

        # 2. curate_reports.py `cpo_user.jsonl` schema with glossary_terms + style
        curated = extract_cpo_record(
            {
                "source_language": "en",
                "target_language": "ko",
                "source_text": "Return to the Waking Sands.",
                "chosen": "모래의 집으로 돌아가라.",
                "rejected": "모래의 집으로 돌아가 주게.",
                "style": "해라체",
                "glossary_terms": [{"source": "Waking Sands", "target": "모래의 집"}],
            }
        )
        self.assertIsNotNone(curated)
        assert curated is not None
        self.assertIn("Style: 해라체", curated[0])
        self.assertIn("Glossary:\nWaking Sands => 모래의 집", curated[0])
        self.assertTrue(curated[0].endswith("English: Return to the Waking Sands.\nKorean:"))

        # Identical chosen/rejected must be skipped
        self.assertIsNone(extract_cpo_record({"prompt": "p", "chosen": "same", "rejected": "same"}))

    def test_extract_anchor_record_accepts_prerendered_and_replay_schema(self):
        a1 = extract_anchor_record({"prompt": "Translate...", "chosen": "안녕"})
        self.assertEqual(a1, ("Translate...", "안녕"))

        a2 = extract_anchor_record(
            {
                "source_language": "Japanese",
                "target_language": "Korean",
                "source_text": "ありがとう",
                "target_text": "고마워요",
            }
        )
        self.assertEqual(a2, ("Translate this from Japanese to Korean:\nJapanese: ありがとう\nKorean:", "고마워요"))

    def test_encode_pair_masks_prompt_and_appends_eos_and_pads_to_multiple_of_8(self):
        tok = _DummyTokenizer()
        res = encode_pair(tok, "PROMPT", "OK", max_len=64)
        self.assertIsNotNone(res)
        assert res is not None
        ids, labels = res
        self.assertEqual(len(ids), 6 + 2 + 1)
        self.assertEqual(labels[:6], [-100] * 6)
        self.assertEqual(labels[-1], tok.eos_token_id)

        # Too long returns None
        self.assertIsNone(encode_pair(tok, "PROMPT", "OK", max_len=5))

        ids_b, mask_b, lab_b = pad_sequences([res, ([10, 11, 1], [-100, 11, 1])], pad_id=0)
        self.assertEqual(len(ids_b[0]) % 8, 0)
        self.assertEqual(len(ids_b[0]), len(ids_b[1]))
        self.assertEqual(mask_b[1][-1], 0)
        self.assertEqual(lab_b[1][-1], -100)


if __name__ == "__main__":
    unittest.main()
