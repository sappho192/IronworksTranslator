#!/usr/bin/env python3
"""Gate 6 Evaluator: Counterfactual Context Disambiguation (16 prompts / 8 pairs)
+ Distractor Context Rejection (15 prompts) + Context Tag-Leak & Stability checks.

Uses Step 0 greedy decoding identical to IronworksTranslator runtime defaults.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.request
from pathlib import Path
from typing import Any

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
from eval_gguf import RE_KANA, RE_LOOP  # noqa: E402

_NO_PROXY_OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))
CONTEXT_LEAK_TOKENS = ("Context:", "Previous:")


def resolve_default_server_bin() -> Path:
    env_bin = os.environ.get("LLAMA_SERVER_BIN")
    if env_bin:
        return Path(env_bin)
    sibling = SCRIPT_DIR.parents[2] / "llama.cpp" / "build" / "bin" / "Release" / "llama-server.exe"
    if sibling.exists():
        return sibling
    which_bin = shutil.which("llama-server")
    if which_bin:
        return Path(which_bin)
    return sibling


def query_completion(port: int, prompt: str, max_tokens: int) -> tuple[str, int]:
    body = json.dumps(
        {
            "prompt": prompt,
            "n_predict": max_tokens,
            "temperature": 0.0,
            "top_k": 1,
            "top_p": 1.0,
            "min_p": 0.0,
            "repeat_penalty": 1.0,
            "frequency_penalty": 0.30,
            "presence_penalty": 0.0,
            "repeat_last_n": 16,
            "seed": 1,
            "stop": ["<end_of_turn>", "<eos>", "</s>"],
            "cache_prompt": False,
        }
    ).encode("utf-8")
    req = urllib.request.Request(
        f"http://127.0.0.1:{port}/completion",
        data=body,
        headers={"Content-Type": "application/json"},
    )
    with _NO_PROXY_OPENER.open(req, timeout=30) as resp:
        data = json.loads(resp.read().decode("utf-8"))
    return str(data.get("content", "")).strip(), int(data.get("tokens_predicted", 0))


# 8 unambiguous counterfactual context pairs (16 prompts) where ONLY the preceding Context resolves the lexical sense
CONTEXT_DISAMBIG_CASES: list[dict[str, Any]] = [
    {
        "id": "ctx_en_bank",
        "sl": "English",
        "dl": "Korean",
        "src": "Let's meet by the bank.",
        "ctx_a": "The river is calm and quiet tonight.",
        "expect_a": ["강가", "강둑", "강변", "둑", "하천"],
        "ctx_b": "I need to withdraw some money first.",
        "expect_b": ["은행"],
    },
    {
        "id": "ctx_en_bat",
        "sl": "English",
        "dl": "Korean",
        "src": "Watch out for the bat!",
        "ctx_a": "This dark cave is full of flying monsters.",
        "expect_a": ["박쥐"],
        "ctx_b": "He is about to swing at the baseball.",
        "expect_b": ["방망이", "배트"],
    },
    {
        "id": "ctx_en_spring",
        "sl": "English",
        "dl": "Korean",
        "src": "The spring is right over there.",
        "ctx_a": "I'm thirsty, let's drink some fresh water.",
        "expect_a": ["샘", "온천", "약수", "우물", "냇물"],
        "ctx_b": "This clockwork toy broke a metal coil.",
        "expect_b": ["스프링", "용수철", "태엽"],
    },
    {
        "id": "ctx_en_light",
        "sl": "English",
        "dl": "Korean",
        "src": "Is it light enough?",
        "ctx_a": "This heavy armor is hard to carry.",
        "expect_a": ["가벼", "가볍"],
        "ctx_b": "It's too dark inside this room.",
        "expect_b": ["밝", "빛"],
    },
    {
        "id": "ctx_en_glasses",
        "sl": "English",
        "dl": "Korean",
        "src": "Where did I put my glasses?",
        "ctx_a": "Everything looks blurry without them.",
        "expect_a": ["안경"],
        "ctx_b": "I just poured juice for everyone.",
        "expect_b": ["잔", "컵", "유리잔"],
    },
    {
        "id": "ctx_en_match",
        "sl": "English",
        "dl": "Korean",
        "src": "Do you have a match?",
        "ctx_a": "We need to light the campfire before dark.",
        "expect_a": ["성냥", "불"],
        "ctx_b": "Our team is playing in the tournament today.",
        "expect_b": ["경기", "시합", "매치"],
    },
    {
        "id": "ctx_ja_atsui",
        "sl": "Japanese",
        "dl": "Korean",
        "src": "すごくあついね。",
        "ctx_a": "出来立てのスープを飲んでみて。",
        "expect_a": ["뜨거", "뜨겁"],
        "ctx_b": "今日は真夏の太陽が強いね。",
        "expect_b": ["더워", "덥"],
    },
    {
        "id": "ctx_ja_kakeru",
        "sl": "Japanese",
        "dl": "Korean",
        "src": "早くかけて！",
        "ctx_a": "急ぎの電話をしなきゃいけないんだ。",
        "expect_a": ["전화", "걸"],
        "ctx_b": "寒いからコートを肩に羽織って。",
        "expect_b": ["걸치", "걸쳐", "입", "덮", "두르"],
    },
]

# 15 distractor context cases: vivid FFXIV prior line must NOT bleed into an unrelated sentence
DISTRACTOR_CONTEXT_CASES: list[dict[str, Any]] = [
    {"sl": "Japanese", "dl": "Korean", "ctx": "リムサ・ロミンサのエーテライト広場で待っています。", "src": "今日は天気がいいですね。", "forbid": ["림사", "에테라이트", "광장"]},
    {"sl": "Japanese", "dl": "Korean", "ctx": "蛮神バハムートが再び目覚めたぞ！", "src": "明日の会議は10時からです。", "forbid": ["바하무트", "야만신"]},
    {"sl": "Japanese", "dl": "Korean", "ctx": "チョコボに乗ってグリダニアへ向かおう。", "src": "コーヒーを一杯ください。", "forbid": ["초코보", "그리다니아"]},
    {"sl": "Japanese", "dl": "Korean", "ctx": "ゴールドソーサーでミニゲームをしよう！", "src": "雨が降ってきたので傘をさします。", "forbid": ["골드", "소서", "미니게임"]},
    {"sl": "Japanese", "dl": "Korean", "ctx": "次はワイプしないようにタンクが誘導して。", "src": "この本はとても面白かったです。", "forbid": ["전멸", "와이프", "탱커"]},
    {"sl": "Japanese", "dl": "Korean", "ctx": "踊り子のギアセットに着替えました。", "src": "電車が少し遅れています。", "forbid": ["무도가", "기어"]},
    {"sl": "Japanese", "dl": "Korean", "ctx": "イシュガルドの雪は本当に冷たいな。", "src": "晩ご飯を作る時間ですね。", "forbid": ["이슈가르드", "눈"]},
    {"sl": "Japanese", "dl": "Korean", "ctx": "モーグリたちが森で騒いでいるぞ。", "src": "週末に友達と映画を見に行きます。", "forbid": ["모그리", "숲"]},
    {"sl": "English", "dl": "Korean", "ctx": "Meet me at the Waking Sands in Vesper Bay.", "src": "Please send the report before Friday.", "forbid": ["모래의 집", "베스퍼", "모래"]},
    {"sl": "English", "dl": "Korean", "ctx": "The primal Ifrit has been summoned again!", "src": "The train arrived five minutes early.", "forbid": ["이프리트", "야만신", "소환"]},
    {"sl": "English", "dl": "Korean", "ctx": "Our Free Company house is in Mist.", "src": "I forgot my umbrella on the subway.", "forbid": ["자유부대", "안갯빛"]},
    {"sl": "English", "dl": "Korean", "ctx": "Tank, please turn on your stance before pulling.", "src": "The library closes at nine o'clock.", "forbid": ["탱커", "스탠스", "풀링"]},
    {"sl": "English", "dl": "Korean", "ctx": "Let's ride our chocobos across Thavnair.", "src": "Thank you for explaining the details clearly.", "forbid": ["초코보", "사베네어"]},
    {"sl": "English", "dl": "Korean", "ctx": "Urianger awaits us in Old Sharlayan.", "src": "Could you share the link to the document?", "forbid": ["위리앙제", "샬레이안"]},
    {"sl": "English", "dl": "Korean", "ctx": "Tataru has prepared new outfits for the Scions.", "src": "I'll be right back, someone is at the door.", "forbid": ["타타루", "새벽", "의상"]},
]


def render_ctx(sl: str, dl: str, src: str, ctx: str) -> str:
    return f"Context:\nPrevious: {ctx}\n\nTranslate this from {sl} to {dl}:\n{sl}: {src}\n{dl}:"


def main() -> int:
    parser = argparse.ArgumentParser(description="Gate 6 Context disambiguation and distractor-bleed evaluator.")
    parser.add_argument("--model", type=Path, required=True, help="Quantized GGUF model path.")
    parser.add_argument("--out-json", type=Path, required=True, help="Output JSON report path.")
    parser.add_argument("--port", type=int, default=18965)
    parser.add_argument("--server-bin", type=Path, default=resolve_default_server_bin())
    args = parser.parse_args()

    srv = subprocess.Popen(
        [
            str(args.server_bin),
            "-m",
            str(args.model),
            "--host",
            "127.0.0.1",
            "--port",
            str(args.port),
            "-c",
            "2048",
            "-ngl",
            "99",
            "--log-disable",
        ],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        for _ in range(80):
            try:
                with _NO_PROXY_OPENER.open(f"http://127.0.0.1:{args.port}/health", timeout=1) as resp:
                    if resp.status == 200:
                        break
            except Exception:
                time.sleep(0.25)

        disambig_rows = []
        prompt_hits = 0
        pair_swaps = 0
        leaks = 0
        loops = 0
        drifts = 0

        for c in CONTEXT_DISAMBIG_CASES:
            out_a, _ = query_completion(args.port, render_ctx(c["sl"], c["dl"], c["src"], c["ctx_a"]), 80)
            out_b, _ = query_completion(args.port, render_ctx(c["sl"], c["dl"], c["src"], c["ctx_b"]), 80)
            hit_a = any(k in out_a for k in c["expect_a"])
            hit_b = any(k in out_b for k in c["expect_b"])
            if hit_a:
                prompt_hits += 1
            if hit_b:
                prompt_hits += 1
            if hit_a and hit_b:
                pair_swaps += 1
            for o in (out_a, out_b):
                if any(tok in o for tok in CONTEXT_LEAK_TOKENS):
                    leaks += 1
                if RE_LOOP.search(o):
                    loops += 1
                if RE_KANA.search(o):
                    drifts += 1
            disambig_rows.append(
                {"id": c["id"], "src": c["src"], "out_a": out_a, "hit_a": hit_a, "out_b": out_b, "hit_b": hit_b}
            )

        dist_rows = []
        dist_bleeds = 0
        for c in DISTRACTOR_CONTEXT_CASES:
            out, _ = query_completion(args.port, render_ctx(c["sl"], c["dl"], c["src"], c["ctx"]), 80)
            bleed = any(f in out for f in c["forbid"])
            if bleed:
                dist_bleeds += 1
            if any(tok in out for tok in CONTEXT_LEAK_TOKENS):
                leaks += 1
            if RE_LOOP.search(out):
                loops += 1
            if RE_KANA.search(out):
                drifts += 1
            dist_rows.append({"src": c["src"], "ctx": c["ctx"], "out": out, "bleed": bleed})

        summary = {
            "model": args.model.name,
            "context_disambig_prompts": f"{prompt_hits}/{len(CONTEXT_DISAMBIG_CASES) * 2}",
            "context_disambig_pairs": f"{pair_swaps}/{len(CONTEXT_DISAMBIG_CASES)}",
            "distractor_context_bleeds": f"{dist_bleeds}/{len(DISTRACTOR_CONTEXT_CASES)}",
            "context_tag_leaks": leaks,
            "loops": loops,
            "drifts": drifts,
        }
        payload = {"summary": summary, "disambiguation": disambig_rows, "distractor_context": dist_rows}
        args.out_json.parent.mkdir(parents=True, exist_ok=True)
        args.out_json.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(summary, ensure_ascii=False))
        return 0
    finally:
        srv.terminate()
        try:
            srv.wait(timeout=5)
        except Exception:
            srv.kill()


if __name__ == "__main__":
    raise SystemExit(main())
