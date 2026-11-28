using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Tests.Utils;

public class MiLMMTGlossaryMatcherTests
{
    private const string SampleGlossaryJson = """
        {
          "version": "2026-11-27.1",
          "pairs": {
            "Japanese->Korean": [
              { "source": "ステラ", "target": "스텔라" },
              { "source": "レストエリア", "target": "휴식 지역" },
              { "source": "踊り子", "target": "무도가" },
              { "source": "ゴールド", "target": "골드" },
              { "source": "ゴールドソーサー", "target": "골드 소서" },
              { "source": "攻略手帳", "target": "공략 수첩" }
            ],
            "English->Korean": [
              { "source": "art", "target": "예술" },
              { "source": "Limsa Lominsa", "target": "림사 로민사" },
              { "source": "Limsa Lominsa Upper Decks", "target": "림사 로민사 상층 갑판" },
              { "source": "Mizzenmast", "target": "미젠마스트" }
            ]
          }
        }
        """;

    [Fact]
    public void Match_EnforcesKatakanaBoundary_ToPreventSubstringFalseHits()
    {
        var matcher = MiLMMTGlossaryMatcher.FromJson(SampleGlossaryJson);

        var falseHitMatches = matcher.Match(
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "カステラと牛乳を持ってきます");
        Assert.Empty(falseHitMatches);

        var exactMatches = matcher.Match(
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "レストエリアに入った！ステラも来た！");
        Assert.Equal(
            [
                new MiLMMTGlossaryTerm("レストエリア", "휴식 지역"),
                new MiLMMTGlossaryTerm("ステラ", "스텔라"),
            ],
            exactMatches);
    }

    [Fact]
    public void Match_EnforcesLatinWordBoundary_AndCaseInsensitiveLongestMatch()
    {
        var matcher = MiLMMTGlossaryMatcher.FromJson(SampleGlossaryJson);

        var partyMatches = matcher.Match(
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
            "Let's start the party now!");
        Assert.Empty(partyMatches);

        var longestMatches = matcher.Match(
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
            "Welcome to limsa lominsa upper decks—the MIZZENMAST!");
        Assert.Equal(
            [
                new MiLMMTGlossaryTerm("Limsa Lominsa Upper Decks", "림사 로민사 상층 갑판"),
                new MiLMMTGlossaryTerm("Mizzenmast", "미젠마스트"),
            ],
            longestMatches);
    }

    [Fact]
    public void Match_PrefersLongestNonOverlappingTerm_AndPreservesLeftToRightOrder()
    {
        var matcher = MiLMMTGlossaryMatcher.FromJson(SampleGlossaryJson);

        var matches = matcher.Match(
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "踊り子でゴールドソーサーの攻略手帳をクリアした");
        Assert.Equal(
            [
                new MiLMMTGlossaryTerm("踊り子", "무도가"),
                new MiLMMTGlossaryTerm("ゴールドソーサー", "골드 소서"),
                new MiLMMTGlossaryTerm("攻略手帳", "공략 수첩"),
            ],
            matches);
    }

    [Fact]
    public void Match_RespectsMaxTermsLimit()
    {
        var matcher = MiLMMTGlossaryMatcher.FromJson(SampleGlossaryJson);

        var matches = matcher.Match(
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "踊り子でゴールドソーサーの攻略手帳とレストエリアを確認",
            maxTerms: 2);

        Assert.Equal(2, matches.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("""{"version":"1.0"}""")]
    public void TryFromJson_ReturnsFalseForInvalidPayloads(string payload)
    {
        var ok = MiLMMTGlossaryMatcher.TryFromJson(payload, out var matcher);

        Assert.False(ok);
        Assert.Same(MiLMMTGlossaryMatcher.Empty, matcher);
    }

    [Fact]
    public void TryResolveNpcName_MatchesOfficialNamesWithCaseAndApostropheNormalization()
    {
        const string jsonWithNpcs = """
            {
              "version": "2026-11-27.3",
              "pairs": {
                "English->Korean": [
                  { "source": "Y'shtola", "target": "야슈톨라" }
                ]
              },
              "npc_names": {
                "English->Korean": {
                  "Urianger": "위리앙제",
                  "Y'shtola": "야슈톨라",
                  "Cid": "시드"
                },
                "Japanese->Korean": {
                  "ウリエンジェ": "위리앙제"
                }
              }
            }
            """;

        var matcher = MiLMMTGlossaryMatcher.FromJson(jsonWithNpcs);

        Assert.Equal(4, matcher.NpcNameCount);
        Assert.True(matcher.TryResolveNpcName(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Urianger", out var urianger));
        Assert.Equal("위리앙제", urianger);

        Assert.True(matcher.TryResolveNpcName(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "  urianger  ", out var uriangerLower));
        Assert.Equal("위리앙제", uriangerLower);

        Assert.True(matcher.TryResolveNpcName(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Y\u2019shtola", out var yshtola));
        Assert.Equal("야슈톨라", yshtola);

        Assert.True(matcher.TryResolveNpcName(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Cid", out var cid));
        Assert.Equal("시드", cid);

        Assert.True(matcher.TryResolveNpcName(TranslationLanguageCode.Japanese, TranslationLanguageCode.Korean, "ウリエンジェ", out var jaUrianger));
        Assert.Equal("위리앙제", jaUrianger);

        Assert.False(matcher.TryResolveNpcName(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Unknown Adventurer", out _));

        // Substring glossary matching also handles curly apostrophes.
        var matches = matcher.Match(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Ask Y\u2019shtola about the aether.");
        Assert.Equal([new MiLMMTGlossaryTerm("Y'shtola", "야슈톨라")], matches);
    }

    [Fact]
    public void FromJson_WithoutNpcNames_RemainsBackwardsCompatible()
    {
        var matcher = MiLMMTGlossaryMatcher.FromJson(SampleGlossaryJson);

        Assert.Equal(0, matcher.NpcNameCount);
        Assert.Equal(0, matcher.NpcStyleCount);
        Assert.False(matcher.TryResolveNpcName(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Urianger", out _));
        Assert.False(matcher.TryResolveNpcStyle(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Y'shtola", out _));
    }

    [Fact]
    public void TryResolveNpcStyle_MatchesSpeechStylesWithCaseAndApostropheNormalization()
    {
        const string jsonWithStyles = """
            {
              "version": "2026-11-27.4",
              "pairs": {},
              "npc_styles": {
                "English->Korean": {
                  "Y'shtola": "해요체",
                  "Urianger": "하십시오체",
                  "Tataru": "애교 존댓말",
                  "Gosetsu": "하오체"
                },
                "German->Korean": {
                  "Y'shtola": "해요체"
                }
              }
            }
            """;

        var matcher = MiLMMTGlossaryMatcher.FromJson(jsonWithStyles);

        Assert.Equal(5, matcher.NpcStyleCount);
        Assert.True(matcher.TryResolveNpcStyle(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Y\u2019shtola", out var yshtolaStyle));
        Assert.Equal("해요체", yshtolaStyle);
        Assert.True(matcher.TryResolveNpcStyle(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "  urianger ", out var uriangerStyle));
        Assert.Equal("하십시오체", uriangerStyle);
        Assert.True(matcher.TryResolveNpcStyle(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Tataru", out var tataruStyle));
        Assert.Equal("애교 존댓말", tataruStyle);
        Assert.False(matcher.TryResolveNpcStyle(TranslationLanguageCode.English, TranslationLanguageCode.Korean, "Unknown NPC", out _));
    }

    [Fact]
    public void Match_NormalizesFullwidthColonForJapaneseDutyNames()
    {
        const string jsonWithColon = """
            {
              "version": "2026-11-28.1",
              "pairs": {
                "Japanese->Korean": [
                  { "source": "至天の座アルカディア:ヘビー級", "target": "지천의 좌 아르카디아: 헤비급" }
                ]
              }
            }
            """;

        var matcher = MiLMMTGlossaryMatcher.FromJson(jsonWithColon);
        var matches = matcher.Match(
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "至天の座アルカディア：ヘビー級に行きます！");

        Assert.Equal(
            [new MiLMMTGlossaryTerm("至天の座アルカディア:ヘビー級", "지천의 좌 아르카디아: 헤비급")],
            matches);
    }

    [Fact]
    public void Match_EnforcesKoreanHangulAndParticleBoundaries_ForReverseGlossary()
    {
        const string reverseJson = """
            {
              "version": "2026-11-28.2",
              "pairs": {
                "Korean->Japanese": [
                  { "source": "에테라이트", "target": "エーテライト" },
                  { "source": "무도가", "target": "踊り子" },
                  { "source": "림사 로민사", "target": "リムサ・ロミンサ" },
                  { "source": "파티", "target": "パーティ" },
                  { "source": "환영", "target": "幻影" },
                  { "source": "골드 소서", "target": "ゴールドソーサー" }
                ]
              }
            }
            """;

        var matcher = MiLMMTGlossaryMatcher.FromJson(reverseJson);

        // Exact terms and Korean particles/copula match cleanly
        var validMatches = matcher.Match(
            TranslationLanguageCode.Korean,
            TranslationLanguageCode.Japanese,
            "림사 로민사에 있는 에테라이트에서 무도가입니다!");
        Assert.Equal(
            [
                new MiLMMTGlossaryTerm("림사 로민사", "リムサ・ロミンサ"),
                new MiLMMTGlossaryTerm("에테라이트", "エーテライト"),
                new MiLMMTGlossaryTerm("무도가", "踊り子"),
            ],
            validMatches);

        // Compound nouns / verbs / embedded substrings are rejected
        var falseHits = matcher.Match(
            TranslationLanguageCode.Korean,
            TranslationLanguageCode.Japanese,
            "환영합니다! 파티원 모집 중, 골드 소서러는 제외, 대무도가도 제외.");
        Assert.Empty(falseHits);

        // Standalone particle after term passes
        var partyParticle = matcher.Match(
            TranslationLanguageCode.Korean,
            TranslationLanguageCode.Japanese,
            "파티로 갈게요!");
        Assert.Equal(
            [new MiLMMTGlossaryTerm("파티", "パーティ")],
            partyParticle);
    }

    [Fact]
    public void Match_AllowsEnglishRegularPluralsOnFourPlusCharTerms()
    {
        const string englishJson = """
            {
              "version": "2026-11-28.2",
              "pairs": {
                "English->Korean": [
                  { "source": "Aetheryte", "target": "에테라이트" },
                  { "source": "alchemist", "target": "연금술사" },
                  { "source": "Radz-at-Han", "target": "라자한" },
                  { "source": "art", "target": "예술" }
                ]
              }
            }
            """;

        var matcher = MiLMMTGlossaryMatcher.FromJson(englishJson);

        var matches = matcher.Match(
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
            "Our first test pair of aetherytes was built with alchemists from Radz-at-Han.");
        Assert.Equal(
            [
                new MiLMMTGlossaryTerm("Aetheryte", "에테라이트"),
                new MiLMMTGlossaryTerm("alchemist", "연금술사"),
                new MiLMMTGlossaryTerm("Radz-at-Han", "라자한"),
            ],
            matches);

        // Must NOT match embedded compounds or 3-char plurals
        var noFalseHits = matcher.Match(
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
            "Look at those arts and aetheryteships.");
        Assert.Empty(noFalseHits);
    }
}
