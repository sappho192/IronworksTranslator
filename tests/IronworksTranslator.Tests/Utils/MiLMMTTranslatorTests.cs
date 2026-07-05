using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Tests.Utils;

public class MiLMMTTranslatorTests
{
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
