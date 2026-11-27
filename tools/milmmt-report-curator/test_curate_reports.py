import json
import tempfile
import unittest
from pathlib import Path

from curate_reports import (
    GlossaryLookup,
    build_sync_sql,
    detect_badges,
    export_datasets,
    extract_diff_spans,
    parse_glossary_input,
    parse_wrangler_d1_json,
    suggest_glossary_candidates,
    text_hash,
)


def make_report(**overrides):
    base = {
        "id": "11111111-1111-4111-8111-111111111111",
        "schema_version": 2,
        "client_timestamp": "2026-11-26T15:00:00Z",
        "created_at": "2026-11-26T15:00:01Z",
        "app_version": "2.3.0",
        "translation_kind": "chat",
        "npc_speaker": None,
        "source_language": "Japanese",
        "target_language": "Korean",
        "source_text": "ギアセット20「踊り子」に装備変更しました。",
        "engine": "MiLMMT",
        "model_id": "MiLMMT-46-1B-v1.0.Q8_0.gguf",
        "model_sha256": "sha_1b",
        "original_translation": "기어 세트 20 '도리코'에 장착 변경했습니다.",
        "suggested_translation": "기어 세트 20 '무도가'로 장비를 변경했습니다.",
        "draft_source": "current",
        "draft_edited": 1,
        "glossary_terms_json": "[]",
        "comment": None,
        "review_status": "pending",
    }
    base.update(overrides)
    return base


class CurateReportsTests(unittest.TestCase):
    def test_parse_wrangler_d1_json_handles_leading_npm_notices(self):
        raw = (
            "npm notice run ironworks-db-endpoint@2.0.0 npx\n"
            "npm notice run wrangler d1 execute ...\n"
            '[{"results": [{"id": "abc", "glossary_terms_json": "[{\\"source\\":\\"A\\",\\"target\\":\\"B\\"}]", "draft_edited": 1}], "success": true}]'
        )
        rows = parse_wrangler_d1_json(raw)
        self.assertEqual(len(rows), 1)
        self.assertEqual(rows[0]["glossary_terms"], [{"source": "A", "target": "B"}])

    def test_badges_and_diff_and_glossary_hints(self):
        with tempfile.TemporaryDirectory() as tmp:
            gl_path = Path(tmp) / "glossary.json"
            gl_path.write_text(
                json.dumps(
                    {
                        "version": "test",
                        "pairs": {
                            "Japanese->Korean": [
                                {"source": "踊り子", "target": "무도가", "origin": "datamining:ClassJob.csv"},
                                {"source": "ステラ", "target": "스텔라", "origin": "datamining:Action.csv"},
                            ]
                        },
                    },
                    ensure_ascii=False,
                ),
                encoding="utf-8",
            )
            lookup = GlossaryLookup(gl_path)
            r1 = make_report()
            hints = suggest_glossary_candidates(r1, lookup)
            self.assertEqual(hints[0]["source"], "踊り子")
            self.assertEqual(hints[0]["target"], "무도가")

            # Katakana boundary check: 'ステラ' must not match inside 'カステラ'
            self.assertEqual(lookup.find_matches("Japanese", "Korean", "カステラと牛乳を持ってきますね"), [])

            spans = extract_diff_spans(r1["original_translation"], r1["suggested_translation"])
            self.assertTrue(any("무도가" in new for _, new in spans))

            r_loop = make_report(
                id="22222222-2222-4222-8222-222222222222",
                original_translation="아" * 40,
                draft_source="papago",
                draft_edited=0,
            )
            from collections import Counter

            badges = detect_badges(r_loop, Counter())
            self.assertIn("REPETITION_LOOP", badges)
            self.assertIn("REFERENCE_ONLY", badges)

    def test_export_isolates_eval_separates_reference_only_and_preserves_cpo_against_papago(self):
        # Same source text reported under both Papago (newer) and MiLMMT (older)
        r_milmmt = make_report(
            id="11111111-1111-4111-8111-111111111111",
            created_at="2026-11-26T15:00:00Z",
            engine="MiLMMT",
            model_sha256="sha_1b",
        )
        r_papago = make_report(
            id="22222222-2222-4222-8222-222222222222",
            created_at="2026-11-26T15:05:00Z",
            engine="Papago",
            model_id=None,
            model_sha256=None,
            original_translation="기어 세트 20 '무희'로 장비를 변경했습니다.",
        )
        # Eval item (must never leak into train even if duplicate source exists)
        r_eval = make_report(
            id="33333333-3333-4333-8333-333333333333",
            source_text="レストエリアに入った！",
            original_translation="레스토랑에 들어갔습니다!",
            suggested_translation="휴식 지역으로 들어갔다!",
        )
        r_eval_dup_train = make_report(
            id="44444444-4444-4444-8444-444444444444",
            source_text="レストエリアに入った！",
            original_translation="레스토랑에 들어갔다!",
            suggested_translation="휴식 지역으로 들어갔다!",
        )
        # Unmodified papago draft -> reference_only only
        r_ref = make_report(
            id="55555555-5555-4555-8555-555555555555",
            source_text="マキシポーション[HQ]×1を捨てた。",
            original_translation="Maxiposity[HQ]×1을 버렸다.",
            suggested_translation="맥시 포션 [HQ]×1을 버렸다.",
            draft_source="papago",
            draft_edited=0,
        )

        curated = {
            r_milmmt["id"]: {
                "id": r_milmmt["id"],
                "review_status": "approved",
                "curated_translation": None,
                "split": "train",
                "failure_tags": ["glossary"],
                "confirmed_glossary_terms": [{"source": "踊り子", "target": "무도가"}],
                "reviewed_at": "2026-11-27T01:00:00Z",
            },
            r_papago["id"]: {
                "id": r_papago["id"],
                "review_status": "approved",
                "curated_translation": None,
                "split": "train",
                "failure_tags": ["glossary"],
                "confirmed_glossary_terms": [],
                "reviewed_at": "2026-11-27T01:01:00Z",
            },
            r_eval["id"]: {
                "id": r_eval["id"],
                "review_status": "approved",
                "curated_translation": "휴식 지역에 들어갔다!",
                "split": "eval",
                "failure_tags": ["glossary", "register"],
                "confirmed_glossary_terms": [{"source": "レストエリア", "target": "휴식 지역"}],
                "reviewed_at": "2026-11-27T01:02:00Z",
            },
            r_eval_dup_train["id"]: {
                "id": r_eval_dup_train["id"],
                "review_status": "approved",
                "curated_translation": None,
                "split": "train",
                "failure_tags": ["glossary"],
                "confirmed_glossary_terms": [],
                "reviewed_at": "2026-11-27T01:03:00Z",
            },
            r_ref["id"]: {
                "id": r_ref["id"],
                "review_status": "approved",
                "curated_translation": None,
                "split": "train",  # Even if accidentally marked train, draft_edited=0 forces reference_only!
                "failure_tags": ["glossary"],
                "confirmed_glossary_terms": [{"source": "マキシポーション", "target": "맥시 포션"}],
                "reviewed_at": "2026-11-27T01:04:00Z",
            },
        }

        res = export_datasets([r_milmmt, r_papago, r_eval, r_eval_dup_train, r_ref], curated, version="2026-11-27.1")
        # Eval contains r_eval with curated_translation override
        self.assertEqual(len(res["eval"]), 1)
        self.assertEqual(res["eval"][0]["reference_translation"], "휴식 지역에 들어갔다!")
        # SFT contains deduplicated 踊り子 sentence only (neither eval duplicate nor reference_only)
        self.assertEqual(len(res["sft_user"]), 1)
        self.assertEqual(res["sft_user"][0]["source_text"], r_milmmt["source_text"])
        # CPO still kept r_milmmt despite newer Papago row!
        self.assertEqual(len(res["cpo_user"]), 1)
        self.assertEqual(res["cpo_user"][0]["id"], r_milmmt["id"])
        # reference_only captured r_ref
        self.assertEqual(len(res["reference_only"]), 1)
        self.assertEqual(res["reference_only"][0]["id"], r_ref["id"])
        # Tier-0 overrides contains all 3 confirmed terms
        override_sources = {x["source"] for x in res["overrides"]["pairs"]["Japanese->Korean"]}
        self.assertEqual(override_sources, {"踊り子", "レストエリア", "マキシポーション"})
        self.assertEqual(res["summary"]["eval_train_overlap"], 0)

    def test_build_sync_sql_validates_uuid_and_only_emits_changes(self):
        r1 = make_report(id="11111111-1111-4111-8111-111111111111", review_status="pending")
        r2 = make_report(id="22222222-2222-4222-8222-222222222222", review_status="approved")
        curated = {
            r1["id"]: {"id": r1["id"], "review_status": "approved"},
            r2["id"]: {"id": r2["id"], "review_status": "approved"},
        }
        sql = build_sync_sql([r1, r2], curated)
        self.assertEqual(
            sql,
            ["UPDATE translation_reports SET review_status = 'approved' WHERE id = '11111111-1111-4111-8111-111111111111';"],
        )

    def test_parse_glossary_input(self):
        self.assertEqual(
            parse_glossary_input("踊り子 => 무도가; レストエリア = 휴식 지역"),
            [{"source": "踊り子", "target": "무도가"}, {"source": "レストエリア", "target": "휴식 지역"}],
        )


if __name__ == "__main__":
    unittest.main()
