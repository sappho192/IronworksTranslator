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
}
