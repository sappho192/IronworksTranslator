using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Translator;
using LLama.Sampling;

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

    [Fact]
    public void CreateInferenceParams_ConfiguresGreedyDecodingWithShortWindowFrequencyPenalty()
    {
        var inferenceParams = MiLMMTTranslator.CreateInferenceParams(64);

        Assert.Equal(64, inferenceParams.MaxTokens);
        var pipeline = Assert.IsType<DefaultSamplingPipeline>(inferenceParams.SamplingPipeline);
        Assert.Equal(0.0f, pipeline.Temperature);
        Assert.Equal(1, pipeline.TopK);
        Assert.Equal(1.0f, pipeline.RepeatPenalty);
        Assert.Equal(0.30f, pipeline.FrequencyPenalty);
        Assert.Equal(0.0f, pipeline.PresencePenalty);
        Assert.Equal(16, pipeline.PenaltyCount);
    }

    [Fact]
    public void RenderPrompt_WithGlossaryTerms_MatchesVerifiedSftFormat()
    {
        var prompt = MiLMMTTranslator.RenderPrompt(
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "  ギアセット20「踊り子」に装備変更しました。 ",
            [new MiLMMTGlossaryTerm("踊り子", "무도가")]);

        Assert.Equal(
            "Glossary:\n踊り子 => 무도가\n\nTranslate this from Japanese to Korean:\nJapanese: ギアセット20「踊り子」に装備変更しました。\nKorean:",
            prompt);
    }

    [Fact]
    public void BuildPromptForProfile_OnlyInjectsGlossaryWhenProfileSupportsGlossaryPrompt()
    {
        var matcher = MiLMMTGlossaryMatcher.FromJson("""
            {
              "version": "2026-11-27.1",
              "pairs": {
                "Japanese->Korean": [
                  { "source": "踊り子", "target": "무도가" }
                ]
              }
            }
            """);

        var baseProfile = MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLMMT_1B, MiLMMTQuantization.Q8_0);
        Assert.False(baseProfile.SupportsGlossaryPrompt);

        var basePrompt = MiLMMTTranslator.BuildPromptForProfile(
            baseProfile,
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "ギアセット20「踊り子」に装備変更しました。",
            matcher);
        Assert.DoesNotContain("Glossary:", basePrompt);

        var glossaryProfile = baseProfile with { SupportsGlossaryPrompt = true };
        var glossaryPrompt = MiLMMTTranslator.BuildPromptForProfile(
            glossaryProfile,
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "ギアセット20「踊り子」に装備変更しました。",
            matcher);
        Assert.StartsWith("Glossary:\n踊り子 => 무도가\n\nTranslate this from Japanese to Korean:", glossaryPrompt);
    }
}
