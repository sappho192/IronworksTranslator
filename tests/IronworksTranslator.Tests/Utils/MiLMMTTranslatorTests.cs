using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Tests.Utils;

public class MiLMMTTranslatorTests
{
    [Fact]
    public void SupportedLanguages_IncludeMiLMMTSupportedLanguages()
    {
        var translator = new MiLMMTTranslator();
        var expected = new[]
        {
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.English,
            TranslationLanguageCode.German,
            TranslationLanguageCode.French,
            TranslationLanguageCode.Korean,
        };

        Assert.Equal(expected, translator.SupportedSourceLanguages);
        Assert.Equal(expected, translator.SupportedTargetLanguages);
    }

    [Fact]
    public void RenderPrompt_UsesSourceTargetLanguageNamesAndTrimmedText()
    {
        var prompt = MiLMMTTranslator.RenderPrompt(
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "  こんにちは  ");

        Assert.Equal(
            "Translate this from Japanese to Korean:\nJapanese: こんにちは\nKorean:",
            prompt);
    }

    [Fact]
    public void RenderPrompt_SupportsGermanAndFrenchLanguageNames()
    {
        var prompt = MiLMMTTranslator.RenderPrompt(
            TranslationLanguageCode.German,
            TranslationLanguageCode.French,
            "  Guten Morgen  ");

        Assert.Equal(
            "Translate this from German to French:\nGerman: Guten Morgen\nFrench:",
            prompt);
    }

    [Theory]
    [InlineData("translated<end_of_turn>ignored", "translated")]
    [InlineData("translated<eos>ignored", "translated")]
    [InlineData("translated</s>ignored", "translated")]
    [InlineData("translated", "translated")]
    public void StripStops_RemovesKnownStopTokens(string value, string expected)
    {
        Assert.Equal(expected, MiLMMTTranslator.StripStops(value));
    }
}
