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

        var baseProfile = MiLMMTModelProfiles.Retired.Single(p => p.FileName == "MiLMMT-46-1B-v1.0.Q8_0.gguf");
        Assert.False(baseProfile.SupportsGlossaryPrompt);

        var basePrompt = MiLMMTTranslator.BuildPromptForProfile(
            baseProfile,
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "ギアセット20「踊り子」に装備変更しました。",
            matcher);
        Assert.DoesNotContain("Glossary:", basePrompt);

        var glossaryProfile = MiLMMTModelProfiles.Get(MiLMMTModelSize.Ironworks_1B, MiLMMTQuantization.Q8_0);
        Assert.True(glossaryProfile.SupportsGlossaryPrompt);
        var glossaryPrompt = MiLMMTTranslator.BuildPromptForProfile(
            glossaryProfile,
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "ギアセット20「踊り子」に装備変更しました。",
            matcher);
        Assert.StartsWith("Glossary:\n踊り子 => 무도가\n\nTranslate this from Japanese to Korean:", glossaryPrompt);
    }

    [Fact]
    public void RenderPrompt_WithStyleHint_MatchesVerifiedV02SftFormat()
    {
        var styleOnly = MiLMMTTranslator.RenderPrompt(
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
            "We must depart at once.",
            glossaryTerms: null,
            styleHint: "하십시오체");
        Assert.Equal(
            "Style: 하십시오체\n\nTranslate this from English to Korean:\nEnglish: We must depart at once.\nKorean:",
            styleOnly);

        var styleAndGlossary = MiLMMTTranslator.RenderPrompt(
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
            "Return to the Waking Sands.",
            [new MiLMMTGlossaryTerm("Waking Sands", "모래의 집")],
            styleHint: "해요체");
        Assert.Equal(
            "Style: 해요체\nGlossary:\nWaking Sands => 모래의 집\n\nTranslate this from English to Korean:\nEnglish: Return to the Waking Sands.\nKorean:",
            styleAndGlossary);
    }

    [Fact]
    public void BuildPromptForProfile_AppliesNpcStyleOnlyForLatinToKoreanDialogueOnStyleCapableProfiles()
    {
        var matcher = MiLMMTGlossaryMatcher.FromJson("""
            {
              "version": "2026-11-27.4",
              "pairs": {
                "English->Korean": [
                  { "source": "Waking Sands", "target": "모래의 집" }
                ]
              },
              "npc_styles": {
                "English->Korean": {
                  "Y'shtola": "해요체",
                  "Tataru": "애교 존댓말"
                },
                "German->Korean": {
                  "Y'shtola": "해요체"
                },
                "Japanese->Korean": {
                  "ヤ・シュトラ": "해요체"
                }
              }
            }
            """);

        var v01Profile = MiLMMTModelProfiles.Retired.Single(p => p.FileName == "ironworks-translator-4b-v0.1.Q4_K_M.gguf");
        Assert.True(v01Profile.SupportsGlossaryPrompt);
        Assert.False(v01Profile.SupportsStylePrompt);
        var v02Profile = MiLMMTModelProfiles.Get(MiLMMTModelSize.Ironworks_4B, MiLMMTQuantization.Q4_K_M);
        Assert.True(v02Profile.SupportsGlossaryPrompt);
        Assert.True(v02Profile.SupportsStylePrompt);

        // v0.1 profile must never emit Style:
        var v01Prompt = MiLMMTTranslator.BuildPromptForProfile(
            v01Profile,
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
            "Return to the Waking Sands.",
            matcher,
            speakerName: "Y'shtola",
            requestKind: MiLMMTTranslationKind.Dialogue);
        Assert.DoesNotContain("Style:", v01Prompt);
        Assert.StartsWith("Glossary:\nWaking Sands => 모래의 집\n\n", v01Prompt);

        // v0.2 profile injects Style: + Glossary: for EN->KO Dialogue
        var v02EnPrompt = MiLMMTTranslator.BuildPromptForProfile(
            v02Profile,
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
            "Return to the Waking Sands.",
            matcher,
            speakerName: "Y'shtola",
            requestKind: MiLMMTTranslationKind.Dialogue);
        Assert.StartsWith("Style: 해요체\nGlossary:\nWaking Sands => 모래의 집\n\n", v02EnPrompt);

        // v0.2 profile injects Style: alone for DE->KO Dialogue without glossary match
        var v02DePrompt = MiLMMTTranslator.BuildPromptForProfile(
            v02Profile,
            TranslationLanguageCode.German,
            TranslationLanguageCode.Korean,
            "Wir müssen sofort aufbrechen.",
            matcher,
            speakerName: "Y'shtola",
            requestKind: MiLMMTTranslationKind.Dialogue);
        Assert.StartsWith("Style: 해요체\n\nTranslate this from German to Korean:", v02DePrompt);

        // JA->KO Dialogue must NOT inject Style: (preserves Japanese source nuance)
        var v02JaPrompt = MiLMMTTranslator.BuildPromptForProfile(
            v02Profile,
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.Korean,
            "砂の家へ戻りましょう。",
            matcher,
            speakerName: "ヤ・シュトラ",
            requestKind: MiLMMTTranslationKind.Dialogue);
        Assert.DoesNotContain("Style:", v02JaPrompt);

        // Non-Dialogue (Chat) must NOT inject Style:
        var v02ChatPrompt = MiLMMTTranslator.BuildPromptForProfile(
            v02Profile,
            TranslationLanguageCode.English,
            TranslationLanguageCode.Korean,
            "Hello everyone!",
            matcher,
            speakerName: "Tataru",
            requestKind: MiLMMTTranslationKind.Chat);
        Assert.DoesNotContain("Style:", v02ChatPrompt);
    }
}
