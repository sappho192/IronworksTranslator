#!/usr/bin/env python3
"""8-way Korean speech-style classifier and NPC register inference for IronworksTranslator v0.2.

Zero external dependencies (Python 3.10+ standard library only).
Shared by:
  - tools/milmmt-glossary-builder/build_glossary.py (runtime `npc_styles` extraction)
  - tools/milmmt-dataset-builder/build_sft_dataset.py (clean target-sentence `Style:` labeling)
  - tools/milmmt-dataset-builder/eval_suite.py (counterfactual style-control scoring)
"""

from __future__ import annotations

import re
from collections import Counter

KOREAN_SPEECH_STYLES: tuple[str, ...] = (
    "하십시오체",
    "해요체",
    "애교 존댓말",
    "반말",
    "해라체",
    "하게체",
    "하오체",
    "고어체",
)

KOREAN_SPEECH_STYLE_SET: frozenset[str] = frozenset(KOREAN_SPEECH_STYLES)

RE_INLINE_SPEAKER = re.compile(r"^\(-[^)]+-\)")
RE_STRIP_TAGS = re.compile(r"<[^>]{1,120}>")
RE_CLEAN_END = re.compile(r"[\s\.!\?~…\"'”’\)\]♪♡♥★☆]+$")

RE_CUTE_POLITE = re.compile(
    r"(?:습니당|입니당|합니당|봅니당|갑니당|옵니당|줍니당|드립니당|겠습니당|었습니당|았습니당"
    r"|봅시당|합시당|갑시당"
    r"|에용|예용|세용|어용|아용|해용|네용|군용|까용|나용|가용|돼용|줘용|봐용|와용|래용|대용|죵|구용|지용|걸용|거용)$"
)
RE_ARCHAIC = re.compile(
    r"(?:사옵니다|하옵니다|되옵니다|드리옵니다|나이다|옵소서|하소서|사옵니까|하옵니까|나이까|소이까|소이다"
    r"|이니라|느니라|니라|노라|로다|도다|리라|거늘|지어다|느냐|더냐|로구나|이로구나|하노라|하리라|으리라|이리라)$"
)
RE_HAO_STRONG = re.compile(
    r"(?:구려|으시오|(?<!십)시오|소서|하오|이오|겠소|았소|었소|였소|으오|는구려|로구려|도구려|소이다|리다)$"
)
RE_HAO_NOUN_EXCLUSION = re.compile(
    r"(?:장소|미소|요소|수소|축소|취소|기소|평소|채소|청소|호소|해소|주소|사무소|진료소|발전소|연구소|정류소|교도소|소요|각오|증오|혐오|정오|영웅호)$"
)
RE_HAPSYO = re.compile(r"(?:습니다|입니다|습니까|입니까|십니까|십시오|십시다|겠습니다만|습니다만|입니다만)$")
RE_HAEYO = re.compile(
    r"(?:이에요|예요|세요|셔요|군요|네요|래요|대요|가요|나요|까요|어요|아요|해요|워요|여요|돼요|줘요|봐요|와요|거예요|건가요|인가요|죠|요)$"
)
RE_HAEYO_NOUN_EXCLUSION = re.compile(
    r"(?:필요|중요|주요|개요|요약|일요|월요|화요|수요|목요|금요|토요|동요|가요)$"
)
RE_HAGE = re.compile(
    r"(?:걸세|일세|할세|을세|게나|하게|보게|오게|가게|주게|말게|있게|겠는가|는가|던가|인가|텐가|시게"
    r"|겠네|았네|었네|였네|하네|이네|되네|보네|오네|가네|주네|마네|다네|라네)$"
)
RE_HAERA = re.compile(
    r"(?:한다|된다|있다|없다|같다|싶다|않다|겠다|았다|었다|였다|인다|이다|란다|단다"
    r"|군|구만|구먼|더라|하마|주마|가마|보마|오마"
    r"|해라|어라|아라|거라|마라|하라|보라|가라|오라|두라|말라|으라|로군)$"
)
RE_HAERA_NOUN_EXCLUSION = re.compile(r"(?:바다|마다|사다|보다)$")
RE_BANMAL = re.compile(
    r"(?:잖아|거든|는데|던데|을걸|할게|을게|할래|을래|거야|이야|구나|하자|가자|보자"
    r"|을까|할까|볼까|갈까|올까|줄까|될까|일까|있나|없나|했나|갔나|왔나|됐나|겠나|봤나"
    r"|어|아|야|해|돼|줘|봐|와|지|네|니|냐|래|대|걸|게|자)$"
)


def classify_korean_sentence_style(sentence: str) -> str | None:
    """Classify a Korean sentence into one of KOREAN_SPEECH_STYLES, or None if ambiguous/fragment."""
    if not sentence:
        return None
    s = RE_INLINE_SPEAKER.sub("", sentence.replace("\r\n", " ").replace("\n", " ")).strip()
    s = RE_STRIP_TAGS.sub("", s).strip()
    s = RE_CLEAN_END.sub("", s)
    if len(s) < 3:
        return None

    # 1. 애교 존댓말 (Tataru-style polite with ㅇ coda: ~습니당, ~에용, ~까용)
    if RE_CUTE_POLITE.search(s):
        return "애교 존댓말"

    # 2. 고어체 (Royal / ancient / solemn archaic endings; checked before 하십시오체 for ~옵니다/~옵니까)
    if RE_ARCHAIC.search(s):
        return "고어체"

    # 3. 하십시오체 (Formal deferential: ~습니다, ~ㅂ니다, ~습니까, ~십시오; checked before 하오체 so ~십시오 is never 하오체)
    if RE_HAPSYO.search(s):
        return "하십시오체"
    if s.endswith(("니다", "니까")) and len(s) >= 3 and "\uac00" <= s[-3] <= "\ud7a3":
        if (ord(s[-3]) - 0xAC00) % 28 == 17:  # ㅂ coda (합니다/갑니다/드립니다/십니까)
            return "하십시오체"
    if s.endswith(("니다만",)) and len(s) >= 4 and "\uac00" <= s[-4] <= "\ud7a3":
        if (ord(s[-4]) - 0xAC00) % 28 == 17:
            return "하십시오체"

    # 4. 하오체 (Historical samurai / courteous archaic: ~하오, ~소, ~구려, ~으시오/~시오)
    if RE_HAO_STRONG.search(s):
        return "하오체"
    if s.endswith(("소", "오")) and not RE_HAO_NOUN_EXCLUSION.search(s):
        return "하오체"

    # 5. 해요체 (Polite soft: ~해요, ~군요, ~예요, ~세요, ~죠)
    if RE_HAEYO.search(s) and not RE_HAEYO_NOUN_EXCLUSION.search(s):
        return "해요체"

    # 6. 하게체 (Dignified comrade / senior: ~걸세, ~일세, ~는가, ~하게)
    if RE_HAGE.search(s):
        return "하게체"
    if s.endswith("세") and len(s) >= 2 and "\uac00" <= s[-2] <= "\ud7a3":
        if (ord(s[-2]) - 0xAC00) % 28 == 8:  # ㄹ coda (할세/될세/갈세/볼세)
            return "하게체"

    # 7. 해라체 (Blunt / authoritative / literary plain: ~다, ~군, ~구만, ~라, ~마)
    if RE_HAERA.search(s):
        return "해라체"
    if s.endswith("다") and len(s) >= 2 and "\uac00" <= s[-2] <= "\ud7a3":
        if not RE_HAERA_NOUN_EXCLUSION.search(s):
            return "해라체"

    # 8. 반말 (Friendly casual haeche: ~어, ~아, ~야, ~지, ~거야, ~잖아, ~거든)
    if s.endswith(("것이니", "이니", "터이니")):
        return None
    if RE_BANMAL.search(s):
        return "반말"

    return None


def infer_npc_dominant_style(
    counts: Counter[str],
    *,
    min_classified: int = 10,
) -> str | None:
    """Infer an NPC's single representative runtime speech style from classified Korean line counts.

    Returns None when evidence is too sparse or genuinely mixed between polite and plain registers.
    """
    total = sum(counts.get(st, 0) for st in KOREAN_SPEECH_STYLES)
    if total < min_classified:
        return None

    r = {st: counts.get(st, 0) / total for st in KOREAN_SPEECH_STYLES}
    polite_total = r["하십시오체"] + r["해요체"] + r["애교 존댓말"]
    plain_total = r["반말"] + r["해라체"] + r["하게체"]

    # 1. Distinctive cute polite (Tataru)
    if r["애교 존댓말"] >= 0.35:
        return "애교 존댓말"

    # 2. Distinctive historical / archaic registers (naturally intermixed with plain/formal lines)
    if r["하오체"] >= 0.25:
        return "하오체"
    if r["고어체"] >= 0.14 and (r["고어체"] + plain_total + r["하오체"]) >= 0.65:
        return "고어체"

    # 3. Polite registers (하십시오체 vs 해요체)
    if polite_total >= 0.70:
        if r["하십시오체"] >= 0.45 and r["하십시오체"] >= r["해요체"]:
            return "하십시오체"
        return "해요체"

    # 4. Plain registers (하게체 vs 해라체 vs 반말)
    if plain_total >= 0.70:
        if r["하게체"] >= 0.12:
            return "하게체"
        if r["해라체"] >= 0.28:
            return "해라체"
        return "반말"

    return None
