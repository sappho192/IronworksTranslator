using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.Utils.Translators;

namespace IronworksTranslator.Tests.Utils;

public class TranslatorFallbackPolicyTests
{
    private static readonly MiLMMTModelProfile Default1BQ8 =
        MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLMMT_1B, MiLMMTQuantization.Q8_0);
    private static readonly MiLMMTModelProfile OneBQ4 =
        MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLMMT_1B, MiLMMTQuantization.Q4_K_M);
    private static readonly MiLMMTModelProfile FourBQ4 =
        MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLMMT_4B, MiLMMTQuantization.Q4_K_M);
    private static readonly MiLMMTModelProfile TwelveBQ4 =
        MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLMMT_12B, MiLMMTQuantization.Q4_K_M);

    private static Func<TranslatorEngine, bool> Unavailable(params TranslatorEngine[] engines) =>
        engine => engines.Contains(engine);

    private static Func<MiLMMTModelProfile, bool> Downloaded(params MiLMMTModelProfile[] profiles) =>
        profile => profiles.Contains(profile);

    [Fact]
    public void Tracker_MarksEngineUnavailableAfterConsecutiveFailures()
    {
        var tracker = new CloudTranslatorFailureTracker(failureThreshold: 3);

        Assert.Equal(1, tracker.RecordFailure(TranslatorEngine.Papago));
        Assert.Equal(2, tracker.RecordFailure(TranslatorEngine.Papago));
        Assert.False(tracker.IsUnavailable(TranslatorEngine.Papago));
        Assert.Equal(3, tracker.RecordFailure(TranslatorEngine.Papago));

        Assert.True(tracker.IsUnavailable(TranslatorEngine.Papago));
        Assert.False(tracker.IsUnavailable(TranslatorEngine.DeepL_API));
    }

    [Fact]
    public void Tracker_SuccessResetsConsecutiveFailures()
    {
        var tracker = new CloudTranslatorFailureTracker(failureThreshold: 2);
        tracker.RecordFailure(TranslatorEngine.Papago);
        tracker.RecordFailure(TranslatorEngine.Papago);

        tracker.RecordSuccess(TranslatorEngine.Papago);

        Assert.False(tracker.IsUnavailable(TranslatorEngine.Papago));
        Assert.Equal(1, tracker.RecordFailure(TranslatorEngine.Papago));
    }

    [Fact]
    public void Tracker_RejectsNonPositiveThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CloudTranslatorFailureTracker(0));
    }

    [Fact]
    public void Decide_PapagoDownWithoutDeepLKeysOrModel_DownloadsDefaultModel()
    {
        var decision = TranslatorFallbackPolicy.Decide(
            TranslatorEngine.Papago,
            TranslatorEngine.Papago,
            Unavailable(TranslatorEngine.Papago),
            deeplApiKeyCount: 0,
            Default1BQ8,
            Downloaded());

        Assert.Equal(TranslatorFallbackActionKind.DownloadLocalModel, decision.Kind);
        Assert.Equal(TranslatorEngine.MiLMMT, decision.Engine);
        Assert.Equal(Default1BQ8, decision.Profile);
    }

    [Fact]
    public void Decide_PapagoDownWithDownloadedModel_UsesItWithoutDownloading()
    {
        var decision = TranslatorFallbackPolicy.Decide(
            TranslatorEngine.Papago,
            TranslatorEngine.Papago,
            Unavailable(TranslatorEngine.Papago),
            deeplApiKeyCount: 0,
            Default1BQ8,
            Downloaded(Default1BQ8));

        Assert.Equal(TranslatorFallbackActionKind.UseLocalModel, decision.Kind);
        Assert.Equal(Default1BQ8, decision.Profile);
    }

    [Fact]
    public void Decide_PapagoDownWithHealthyDeepLKeys_SwitchesToDeepLFirst()
    {
        var decision = TranslatorFallbackPolicy.Decide(
            TranslatorEngine.Papago,
            TranslatorEngine.Papago,
            Unavailable(TranslatorEngine.Papago),
            deeplApiKeyCount: 1,
            Default1BQ8,
            Downloaded());

        Assert.Equal(TranslatorFallbackActionKind.SwitchCloudEngine, decision.Kind);
        Assert.Equal(TranslatorEngine.DeepL_API, decision.Engine);
        Assert.Null(decision.Profile);
    }

    [Fact]
    public void Decide_DeepLDownAfterPapagoAlsoFailed_FallsBackToLocalModel()
    {
        var decision = TranslatorFallbackPolicy.Decide(
            TranslatorEngine.DeepL_API,
            TranslatorEngine.DeepL_API,
            Unavailable(TranslatorEngine.Papago, TranslatorEngine.DeepL_API),
            deeplApiKeyCount: 1,
            Default1BQ8,
            Downloaded());

        Assert.Equal(TranslatorFallbackActionKind.DownloadLocalModel, decision.Kind);
    }

    [Fact]
    public void Decide_DeepLDownWhilePapagoHealthy_SwitchesToPapago()
    {
        var decision = TranslatorFallbackPolicy.Decide(
            TranslatorEngine.DeepL_API,
            TranslatorEngine.DeepL_API,
            Unavailable(TranslatorEngine.DeepL_API),
            deeplApiKeyCount: 1,
            Default1BQ8,
            Downloaded());

        Assert.Equal(TranslatorFallbackActionKind.SwitchCloudEngine, decision.Kind);
        Assert.Equal(TranslatorEngine.Papago, decision.Engine);
    }

    [Theory]
    [InlineData(TranslatorEngine.Papago, TranslatorEngine.MiLMMT)]
    [InlineData(TranslatorEngine.Papago, TranslatorEngine.DeepL_API)]
    [InlineData(TranslatorEngine.MiLMMT, TranslatorEngine.MiLMMT)]
    public void Decide_IgnoresFailuresOfInactiveOrLocalEngines(
        TranslatorEngine failedEngine,
        TranslatorEngine activeEngine)
    {
        var decision = TranslatorFallbackPolicy.Decide(
            failedEngine,
            activeEngine,
            Unavailable(failedEngine),
            deeplApiKeyCount: 0,
            Default1BQ8,
            Downloaded());

        Assert.Equal(TranslatorFallbackActionKind.None, decision.Kind);
    }

    [Fact]
    public void Decide_DoesNothingBeforeThresholdIsReached()
    {
        var decision = TranslatorFallbackPolicy.Decide(
            TranslatorEngine.Papago,
            TranslatorEngine.Papago,
            Unavailable(),
            deeplApiKeyCount: 0,
            Default1BQ8,
            Downloaded());

        Assert.Equal(TranslatorFallbackActionKind.None, decision.Kind);
    }

    [Fact]
    public void Decide_LargeSelectedModelMissing_DownloadsDefault1BInstead()
    {
        var decision = TranslatorFallbackPolicy.Decide(
            TranslatorEngine.Papago,
            TranslatorEngine.Papago,
            Unavailable(TranslatorEngine.Papago),
            deeplApiKeyCount: 0,
            TwelveBQ4,
            Downloaded());

        Assert.Equal(TranslatorFallbackActionKind.DownloadLocalModel, decision.Kind);
        Assert.Equal(Default1BQ8, decision.Profile);
    }

    [Fact]
    public void Decide_Selected1BQuantizationIsKeptForDownload()
    {
        var profile = TranslatorFallbackPolicy.GetDownloadProfile(OneBQ4);

        Assert.Equal(OneBQ4, profile);
    }

    [Fact]
    public void FindAvailableLocalProfile_PrefersSameSizeThenSmallestFile()
    {
        var fourBQ8 = MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLMMT_4B, MiLMMTQuantization.Q8_0);

        var sameSize = TranslatorFallbackPolicy.FindAvailableLocalProfile(
            fourBQ8,
            Downloaded(Default1BQ8, FourBQ4));
        var smallest = TranslatorFallbackPolicy.FindAvailableLocalProfile(
            TwelveBQ4,
            Downloaded(Default1BQ8, OneBQ4, FourBQ4));

        Assert.Equal(FourBQ4, sameSize);
        Assert.Equal(OneBQ4, smallest);
    }

    [Fact]
    public void OutcomeReported_ListenerFailureDoesNotBreakTranslation()
    {
        var translator = new FakeTranslator();
        translator.OutcomeReported += (_, _) => throw new InvalidOperationException("listener failure");

        var result = translator.Translate("text", TranslationLanguageCode.Japanese, TranslationLanguageCode.Korean);

        Assert.Equal("text", result);
    }

    [Fact]
    public void OutcomeReported_ReportsSuccessAndFailureWithError()
    {
        var translator = new FakeTranslator();
        var outcomes = new List<TranslatorOutcomeEventArgs>();
        translator.OutcomeReported += (_, e) => outcomes.Add(e);

        translator.Translate("ok", TranslationLanguageCode.Japanese, TranslationLanguageCode.Korean);
        translator.Translate("fail", TranslationLanguageCode.Japanese, TranslationLanguageCode.Korean);

        Assert.Collection(
            outcomes,
            first => Assert.True(first.Succeeded),
            second =>
            {
                Assert.False(second.Succeeded);
                Assert.IsType<TimeoutException>(second.Error);
            });
    }

    private sealed class FakeTranslator : TranslatorBase
    {
        public override TranslationLanguageCode[] SupportedSourceLanguages => [TranslationLanguageCode.Japanese];
        public override TranslationLanguageCode[] SupportedTargetLanguages => [TranslationLanguageCode.Korean];

        public override string Translate(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            if (input == "fail")
            {
                ReportTranslationFailed(new TimeoutException());
            }
            else
            {
                ReportTranslationSucceeded();
            }

            return input;
        }

        public override Task<string> TranslateAsync(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            return Task.FromResult(Translate(input, sourceLanguage, targetLanguage));
        }
    }
}
