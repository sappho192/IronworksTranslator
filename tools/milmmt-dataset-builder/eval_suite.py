#!/usr/bin/env python3
"""Comprehensive behavioral & regression suite for MiLMMT GGUF models.

Tests 4 gates beyond hold-out eval.jsonl:
  1. Distractor Rejection (25 clean sentences + 2 unrelated FFXIV glossary terms each)
  2. Counterfactual Glossary Swapping (8 FFXIV sentences x 2 swapped glossary targets = 16 prompts)
  3. General Non-Glossary Multilingual Regression (24 clean sentences across JA/EN/DE/FR->KO, KO->JA/EN)
  4. Short-Input Repetition Loop Check (6 short chat triggers)

Uses IronworksTranslator's exact Step 0 decoding parameters:
  temperature=0.0, top_k=1, repeat_penalty=1.0, frequency_penalty=0.30, repeat_last_n=16.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import time
import urllib.request
from pathlib import Path
from typing import Any

from build_sft_dataset import (
    KOREAN_SPEECH_STYLES,
    classify_korean_sentence_style,
    render_prompt,
)
from eval_gguf import RE_KANA, RE_LOOP, query_completion

RE_HANGUL = re.compile(r"[\uAC00-\uD7A3]")
RE_LATIN = re.compile(r"[A-Za-z]")
STYLE_LEAK_TOKENS = ("Style:", "하십시오체", "해요체", "애교 존댓말", "해라체", "하게체", "하오체", "고어체")
UNPROMPTED_BLEED_STYLES = frozenset({"애교 존댓말", "고어체", "하오체"})

STYLE_CONTROL_CASES: list[dict[str, Any]] = [
    {"id": "st_01", "sl": "English", "dl": "Korean", "src": "We must depart at once.", "gl": []},
    {"id": "st_02", "sl": "English", "dl": "Korean", "src": "Thank you for coming all this way.", "gl": []},
    {"id": "st_03", "sl": "English", "dl": "Korean", "src": "Are you ready for what lies ahead?", "gl": []},
    {"id": "st_04", "sl": "English", "dl": "Korean", "src": "Wait here until I return with the others.", "gl": []},
    {"id": "st_05", "sl": "English", "dl": "Korean", "src": "I have never seen anything quite like this before.", "gl": []},
    {"id": "st_06", "sl": "English", "dl": "Korean", "src": "Do not worry, we will find a way through this together.", "gl": []},
    {"id": "st_07", "sl": "English", "dl": "Korean", "src": "Something feels strangely quiet around here.", "gl": []},
    # Joint Style + Glossary composition cases
    {
        "id": "st_08_gl",
        "sl": "English",
        "dl": "Korean",
        "src": "Please return to the Waking Sands as soon as possible.",
        "gl": [("Waking Sands", "모래의 집")],
    },
    {
        "id": "st_09_gl",
        "sl": "English",
        "dl": "Korean",
        "src": "The ship to Limsa Lominsa will leave shortly.",
        "gl": [("Limsa Lominsa", "림사 로민사")],
    },
    {
        "id": "st_10_gl",
        "sl": "English",
        "dl": "Korean",
        "src": "Meet me by the Aetheryte plaza when you are finished.",
        "gl": [("Aetheryte", "에테라이트")],
    },
]

DISTRACTOR_CASES: list[dict[str, Any]] = [
    {"sl": "Japanese", "dl": "Korean", "src": "今日は天気がとてもいいですね。", "gl": [("踊り子", "무도가"), ("レストエリア", "휴식 지역")]},
    {"sl": "Japanese", "dl": "Korean", "src": "明日の会議は午前10時から始まります。", "gl": [("ゴールドソーサー", "골드 소서"), ("テレポ", "텔레포")]},
    {"sl": "Japanese", "dl": "Korean", "src": "駅前のカフェでコーヒーを飲みました。", "gl": [("ミ젠마스트", "미젠마스트"), ("ギル", "길")]},
    {"sl": "Japanese", "dl": "Korean", "src": "この本はとても面白くて一気に読んでしまいました。", "gl": [("蛮神", "야만신"), ("エーテライト", "에테라이트")]},
    {"sl": "Japanese", "dl": "Korean", "src": "週末に友達と映画を見に行く予定です。", "gl": [("極蛮神", "극만신"), ("零式", "영식")]},
    {"sl": "Japanese", "dl": "Korean", "src": "電車が少し遅れているみたいです。", "gl": [("タンク", "탱커"), ("ヒーラー", "힐러")]},
    {"sl": "Japanese", "dl": "Korean", "src": "新しいパソコンを買ったので設定しています。", "gl": [("リムサ・ロミンサ", "림사 로민사"), ("グリダニア", "그리다니아")]},
    {"sl": "Japanese", "dl": "Korean", "src": "そろそろ晩ご飯を作る時間ですね。", "gl": [("チョコボ", "초코보"), ("モーグリ", "모그리")]},
    {"sl": "Japanese", "dl": "Korean", "src": "お忙しいところありがとうございます。", "gl": [("マウント", "탈것"), ("ミニオン", "꼬마 친구")]},
    {"sl": "Japanese", "dl": "Korean", "src": "すみません、もう一度お願いできますか？", "gl": [("リミットブレイク", "리미트 브레이크"), ("スタンス", "스탠스")]},
    {"sl": "Japanese", "dl": "Korean", "src": "来週から旅行に行くのが楽しみです。", "gl": [("コンテンツサポーター", "콘텐츠 서포터"), ("パッチ", "패치")]},
    {"sl": "Japanese", "dl": "Korean", "src": "雨が降ってきたので傘をさしました。", "gl": [("ウルダハ", "울다하"), ("イシュガルド", "이슈가르드")]},
    {"sl": "Japanese", "dl": "Korean", "src": "このレストランのパスタは本当においしいです。", "gl": [("パーティ募集", "파티 모집"), ("マクロ", "매크로")]},
    {"sl": "English", "dl": "Korean", "src": "Please remember to send the report before Friday afternoon.", "gl": [("Limsa Lominsa", "림사 로민사"), ("Mizzenmast", "미젠마스트")]},
    {"sl": "English", "dl": "Korean", "src": "The train arrived five minutes earlier than scheduled.", "gl": [("Aetheryte", "에테라이트"), ("Chocobo", "초코보")]},
    {"sl": "English", "dl": "Korean", "src": "We decided to postpone the picnic because of the heavy rain.", "gl": [("Gold Saucer", "골드 소서"), ("Duty Support", "콘텐츠 서포터")]},
    {"sl": "English", "dl": "Korean", "src": "Can you recommend a quiet place to study nearby?", "gl": [("Eorzea", "에오르제아"), ("Primal", "야만신")]},
    {"sl": "English", "dl": "Korean", "src": "I forgot my umbrella on the subway this morning.", "gl": [("Free Company", "자유부대"), ("Linkshell", "링크셸")]},
    {"sl": "English", "dl": "Korean", "src": "Thank you for taking the time to explain the details clearly.", "gl": [("Glamour", "투영"), ("Retainer", "집사")]},
    {"sl": "English", "dl": "Korean", "src": "The library closes at nine o'clock on weekdays.", "gl": [("Ishgard", "이슈가르드"), ("Gridania", "그리다니아")]},
    {"sl": "German", "dl": "Korean", "src": "Das Wetter ist heute sehr angenehm.", "gl": [("Limsa Lominsa", "림사 로민사"), ("Chocobo", "초코보")]},
    {"sl": "French", "dl": "Korean", "src": "Le train partira dans dix minutes.", "gl": [("Éorzéa", "에오르제아"), ("Chocobo", "초코보")]},
    {"sl": "Korean", "dl": "Japanese", "src": "오늘 저녁에 같이 식사하실래요?", "gl": [("무도가", "踊り子"), ("골드 소서", "ゴールドソーサー")]},
    {"sl": "Korean", "dl": "English", "src": "내일 오전에 자료를 정리해서 보내드리겠습니다.", "gl": [("에테라이트", "Aetheryte"), ("자유부대", "Free Company")]},
    {"sl": "Japanese", "dl": "Korean", "src": "今日は本当に疲れたので早く寝ます。", "gl": [("シャキる", "매칭되다"), ("ワイプ", "전멸")]},
]

COUNTERFACTUAL_CASES: list[dict[str, Any]] = [
    {
        "id": "cf_mizzenmast",
        "sl": "English",
        "dl": "Korean",
        "src": "Welcome to the heart of Limsa Lominsa—the Mizzenmast!",
        "a": [("Limsa Lominsa", "림사 로민사"), ("Mizzenmast", "미젠마스트")],
        "b": [("Limsa Lominsa", "림사 로민사"), ("Mizzenmast", "뒷돛대")],
        "expect_a": "미젠마스트",
        "expect_b": "뒷돛대",
    },
    {
        "id": "cf_dancer",
        "sl": "Japanese",
        "dl": "Korean",
        "src": "ギアセット20「踊り子」に装備変更しました。",
        "a": [("踊り子", "무도가")],
        "b": [("踊り子", "댄서")],
        "expect_a": "무도가",
        "expect_b": "댄서",
    },
    {
        "id": "cf_duty_support",
        "sl": "Japanese",
        "dl": "Korean",
        "src": "コンテンツサポーターを使ってダンジョンに入ります。",
        "a": [("コンテンツサポーター", "콘텐츠 서포터")],
        "b": [("コンテンツサポーター", "임무 지원")],
        "expect_a": "콘텐츠 서포터",
        "expect_b": "임무 지원",
    },
    {
        "id": "cf_sanctuary",
        "sl": "Japanese",
        "dl": "Korean",
        "src": "レストエリアに入った！",
        "a": [("レストエリア", "휴식 지역")],
        "b": [("レストエリア", "안식처")],
        "expect_a": "휴식 지역",
        "expect_b": "안식처",
    },
    {
        "id": "cf_gold_saucer",
        "sl": "Japanese",
        "dl": "Korean",
        "src": "ゴールドソーサーでミニゲームを遊びましょう。",
        "a": [("ゴールドソーサー", "골드 소서")],
        "b": [("ゴールドソーサー", "황금 접시")],
        "expect_a": "골드 소서",
        "expect_b": "황금 접시",
    },
    {
        "id": "cf_aetheryte",
        "sl": "English",
        "dl": "Korean",
        "src": "Meet me at the main Aetheryte plaza in five minutes.",
        "a": [("Aetheryte", "에테라이트")],
        "b": [("Aetheryte", "수정탑")],
        "expect_a": "에테라이트",
        "expect_b": "수정탑",
    },
    {
        "id": "cf_wipe",
        "sl": "Japanese",
        "dl": "Korean",
        "src": "次はワイプしないように気をつけましょう。",
        "a": [("ワイプ", "전멸")],
        "b": [("ワイプ", "와이프")],
        "expect_a": "전멸",
        "expect_b": "와이프",
    },
    {
        "id": "cf_stance",
        "sl": "English",
        "dl": "Korean",
        "src": "Tank, please turn on your stance before pulling.",
        "a": [("stance", "스탠스")],
        "b": [("stance", "어글기")],
        "expect_a": "스탠스",
        "expect_b": "어글기",
    },
]

GENERAL_CASES: list[dict[str, str]] = [
    {"sl": "Japanese", "dl": "Korean", "src": "おはようございます、今日もよろしくお願いします。"},
    {"sl": "Japanese", "dl": "Korean", "src": "少し席を外します、5分ほどで戻ります。"},
    {"sl": "Japanese", "dl": "Korean", "src": "ごめんなさい、私のミスでした！"},
    {"sl": "Japanese", "dl": "Korean", "src": "そのアイデアはとても良いと思います。"},
    {"sl": "Japanese", "dl": "Korean", "src": "昨日見たドラマの最終回が予想外の展開だった。"},
    {"sl": "Japanese", "dl": "Korean", "src": "ファイルを開こうとするとエラーが表示されます。"},
    {"sl": "Japanese", "dl": "Korean", "src": "荷物は明日のお昼ごろ届く予定です。"},
    {"sl": "Japanese", "dl": "Korean", "src": "お先に失礼します、また明日！"},
    {"sl": "English", "dl": "Korean", "src": "Sorry for the late reply, I just saw your message."},
    {"sl": "English", "dl": "Korean", "src": "Could you share the link to the document when you have a moment?"},
    {"sl": "English", "dl": "Korean", "src": "The server will undergo scheduled maintenance for two hours tonight."},
    {"sl": "English", "dl": "Korean", "src": "That makes complete sense, let's go with option B."},
    {"sl": "English", "dl": "Korean", "src": "I'll be right back, someone is at the door."},
    {"sl": "English", "dl": "Korean", "src": "Nice work everyone, that went much smoother than expected!"},
    {"sl": "German", "dl": "Korean", "src": "Vielen Dank für Ihre schnelle Hilfe!"},
    {"sl": "German", "dl": "Korean", "src": "Wir treffen uns morgen um acht Uhr am Bahnhof."},
    {"sl": "German", "dl": "Korean", "src": "Entschuldigung, ich habe die Nachricht zu spät gesehen."},
    {"sl": "French", "dl": "Korean", "src": "Merci beaucoup pour votre patience et votre aide."},
    {"sl": "French", "dl": "Korean", "src": "Je reviens dans quelques minutes, ne bougez pas."},
    {"sl": "French", "dl": "Korean", "src": "Il fait vraiment froid dehors aujourd'hui."},
    {"sl": "Korean", "dl": "Japanese", "src": "잠시만 기다려 주세요, 곧 준비됩니다."},
    {"sl": "Korean", "dl": "Japanese", "src": "오늘 정말 수고 많으셨습니다! 다음에 또 만나요."},
    {"sl": "Korean", "dl": "English", "src": "죄송하지만 방금 말씀하신 내용을 다시 한 번 설명해 주실 수 있나요?"},
    {"sl": "Korean", "dl": "English", "src": "저는 준비 끝났습니다. 바로 시작해도 괜찮아요."},
]

SHORT_LOOP_CASES: list[dict[str, str]] = [
    {"sl": "Japanese", "dl": "Korean", "src": "はい"},
    {"sl": "Japanese", "dl": "Korean", "src": "おつ"},
    {"sl": "Japanese", "dl": "Korean", "src": "よろ"},
    {"sl": "Japanese", "dl": "Korean", "src": "www"},
    {"sl": "English", "dl": "Korean", "src": "gg"},
    {"sl": "English", "dl": "Korean", "src": "tyfp"},
]


def has_term(text: str, term: str) -> bool:
    return term.replace(" ", "").lower() in text.replace(" ", "").lower()


def check_drift(dl: str, out: str) -> bool:
    if dl == "Korean":
        return bool(RE_KANA.search(out)) or not bool(RE_HANGUL.search(out))
    if dl == "Japanese":
        return bool(RE_HANGUL.search(out))
    if dl == "English":
        return bool(RE_HANGUL.search(out)) or not bool(RE_LATIN.search(out))
    return False


def main() -> int:
    parser = argparse.ArgumentParser(description="Run 4-gate behavioral & regression suite on a MiLMMT GGUF.")
    parser.add_argument("--server-bin", type=Path, default=Path(r"D:\REPO\llama.cpp\build\bin\Release\llama-server.exe"))
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--port", type=int, default=18935)
    parser.add_argument("--eval-style", action="store_true", help="Run Gate 5: Counterfactual Style Control (10x8=80 prompts) and unprompted bleed check.")
    parser.add_argument("--out-json", type=Path, required=True)
    args = parser.parse_args()

    srv = subprocess.Popen(
        [str(args.server_bin), "-m", str(args.model), "--host", "127.0.0.1", "--port", str(args.port), "-c", "2048", "-ngl", "99", "--log-disable"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        for _ in range(80):
            try:
                with urllib.request.urlopen(f"http://127.0.0.1:{args.port}/health", timeout=1) as resp:
                    if resp.status == 200:
                        break
            except Exception:
                time.sleep(0.25)

        # 1. Distractor rejection
        dist_rows = []
        dist_false = 0
        dist_loops = 0
        dist_drifts = 0
        for c in DISTRACTOR_CASES:
            p = render_prompt(c["sl"], c["dl"], c["src"], c["gl"])
            out, toks = query_completion(args.port, p, 96)
            leaked = [tgt for _, tgt in c["gl"] if has_term(out, tgt)]
            loop = bool(RE_LOOP.search(out))
            drift = check_drift(c["dl"], out)
            if leaked:
                dist_false += 1
            if loop:
                dist_loops += 1
            if drift:
                dist_drifts += 1
            dist_rows.append({"src": c["src"], "gl": c["gl"], "out": out, "tokens": toks, "leaked": leaked, "loop": loop, "drift": drift})

        # 2. Counterfactual swapping
        cf_rows = []
        cf_hits = 0
        cf_total = 0
        cf_both_ok = 0
        for c in COUNTERFACTUAL_CASES:
            pa = render_prompt(c["sl"], c["dl"], c["src"], c["a"])
            pb = render_prompt(c["sl"], c["dl"], c["src"], c["b"])
            oa, ta = query_completion(args.port, pa, 96)
            ob, tb = query_completion(args.port, pb, 96)
            ha = has_term(oa, c["expect_a"]) and not has_term(oa, c["expect_b"])
            hb = has_term(ob, c["expect_b"]) and not has_term(ob, c["expect_a"])
            cf_total += 2
            cf_hits += int(ha) + int(hb)
            cf_both_ok += int(ha and hb)
            cf_rows.append({
                "id": c["id"],
                "src": c["src"],
                "out_a": oa,
                "expect_a": c["expect_a"],
                "ok_a": ha,
                "out_b": ob,
                "expect_b": c["expect_b"],
                "ok_b": hb,
            })

        # 3. General non-glossary regression
        gen_rows = []
        gen_loops = 0
        gen_drifts = 0
        gen_empty = 0
        gen_trunc = 0
        for c in GENERAL_CASES:
            p = render_prompt(c["sl"], c["dl"], c["src"], [])
            out, toks = query_completion(args.port, p, 96)
            loop = bool(RE_LOOP.search(out))
            drift = check_drift(c["dl"], out)
            empty = len(out) == 0
            trunc = toks >= 96
            gen_loops += int(loop)
            gen_drifts += int(drift)
            gen_empty += int(empty)
            gen_trunc += int(trunc)
            gen_rows.append({"dir": f"{c['sl']}->{c['dl']}", "src": c["src"], "out": out, "tokens": toks, "loop": loop, "drift": drift, "trunc": trunc})

        # 4. Short-input loop check
        short_rows = []
        short_loops = 0
        short_trunc = 0
        for c in SHORT_LOOP_CASES:
            p = render_prompt(c["sl"], c["dl"], c["src"], [])
            out, toks = query_completion(args.port, p, 64)
            loop = bool(RE_LOOP.search(out))
            trunc = toks >= 64
            short_loops += int(loop)
            short_trunc += int(trunc)
            short_rows.append({"src": c["src"], "out": out, "tokens": toks, "loop": loop, "trunc": trunc})

        # 5. Optional Counterfactual Style Control (10 sentences x 8 Korean styles = 80 prompts)
        style_rows = []
        style_summary: dict[str, Any] = {}
        if args.eval_style:
            st_hits = 0
            st_total = 0
            st_leaks = 0
            st_loops = 0
            st_drifts = 0
            st_gl_hits = 0
            st_gl_total = 0
            per_style_hits: dict[str, int] = {st: 0 for st in KOREAN_SPEECH_STYLES}
            per_style_total: dict[str, int] = {st: 0 for st in KOREAN_SPEECH_STYLES}

            for c in STYLE_CONTROL_CASES:
                for st in KOREAN_SPEECH_STYLES:
                    p = render_prompt(c["sl"], c["dl"], c["src"], c["gl"], style=st)
                    out, toks = query_completion(args.port, p, 96)
                    detected = classify_korean_sentence_style(out)
                    ok_style = detected == st
                    leak = any(tok in out for tok in STYLE_LEAK_TOKENS)
                    loop = bool(RE_LOOP.search(out))
                    drift = check_drift(c["dl"], out)

                    st_total += 1
                    per_style_total[st] += 1
                    if ok_style:
                        st_hits += 1
                        per_style_hits[st] += 1
                    st_leaks += int(leak)
                    st_loops += int(loop)
                    st_drifts += int(drift)

                    gl_ok = True
                    for _, tgt in c["gl"]:
                        st_gl_total += 1
                        if has_term(out, tgt):
                            st_gl_hits += 1
                        else:
                            gl_ok = False

                    style_rows.append({
                        "id": c["id"],
                        "src": c["src"],
                        "requested_style": st,
                        "detected_style": detected,
                        "ok_style": ok_style,
                        "gl": c["gl"],
                        "ok_gl": gl_ok,
                        "leak": leak,
                        "loop": loop,
                        "drift": drift,
                        "out": out,
                        "tokens": toks,
                    })

            gen_ko_bleed = sum(
                1
                for r in gen_rows
                if r["dir"].endswith("->Korean")
                and classify_korean_sentence_style(r["out"]) in UNPROMPTED_BLEED_STYLES
            )
            style_summary = {
                "style_control_hits": f"{st_hits}/{st_total}",
                "style_control_rate": round(st_hits / max(st_total, 1), 4),
                "style_per_register": {
                    st: f"{per_style_hits[st]}/{per_style_total[st]}"
                    for st in KOREAN_SPEECH_STYLES
                },
                "style_joint_glossary_hits": f"{st_gl_hits}/{st_gl_total}",
                "style_tag_leaks": st_leaks,
                "style_loops": st_loops,
                "style_drifts": st_drifts,
                "general_unprompted_style_bleed": gen_ko_bleed,
            }

        report = {
            "model": args.model.name,
            "summary": {
                "distractor_false_insertions": f"{dist_false}/{len(DISTRACTOR_CASES)}",
                "distractor_false_rate": round(dist_false / len(DISTRACTOR_CASES), 4),
                "distractor_loops": dist_loops,
                "distractor_drifts": dist_drifts,
                "counterfactual_prompt_hits": f"{cf_hits}/{cf_total}",
                "counterfactual_pair_swaps": f"{cf_both_ok}/{len(COUNTERFACTUAL_CASES)}",
                "general_total": len(GENERAL_CASES),
                "general_loops": gen_loops,
                "general_drifts": gen_drifts,
                "general_empty": gen_empty,
                "general_truncated": gen_trunc,
                "short_total": len(SHORT_LOOP_CASES),
                "short_loops": short_loops,
                "short_truncated": short_trunc,
                **style_summary,
            },
            "distractor": dist_rows,
            "counterfactual": cf_rows,
            "general": gen_rows,
            "short_loop": short_rows,
            **({"style_control": style_rows} if args.eval_style else {}),
        }
        args.out_json.parent.mkdir(parents=True, exist_ok=True)
        args.out_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(json.dumps({"model": report["model"], **report["summary"]}, ensure_ascii=False, indent=2))
        return 0
    finally:
        srv.terminate()
        try:
            srv.wait(timeout=5)
        except Exception:
            srv.kill()


if __name__ == "__main__":
    raise SystemExit(main())
