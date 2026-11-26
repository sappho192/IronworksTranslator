using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Services;
using System.IO;

namespace IronworksTranslator.Tests.Services;

public class StartupMiLMMTUpgradePromptTests
{
    private static readonly MiLMMTModelProfile Target1BQ8 =
        MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLMMT_1B, MiLMMTQuantization.Q8_0);
    private static readonly MiLMMTModelProfile Legacy1BQ8 =
        MiLMMTModelProfiles.Retired.Single(p =>
            p.AllowTemporaryFallback && p.Size == MiLMMTModelSize.MiLMMT_1B && p.Quantization == MiLMMTQuantization.Q8_0);

    [Theory]
    [InlineData(TranslatorEngine.Papago)]
    [InlineData(TranslatorEngine.DeepL_API)]
    public void Evaluate_IgnoresNonMiLMMTEngines(TranslatorEngine engine)
    {
        var plan = StartupMiLMMTUpgradePrompt.Evaluate(
            engine,
            Target1BQ8,
            p => p == Legacy1BQ8);

        Assert.Equal(StartupMiLMMTUpgradeAction.None, plan.Action);
    }

    [Fact]
    public void Evaluate_ReturnsNoneWhenTargetV10IsAlreadyAvailable()
    {
        var plan = StartupMiLMMTUpgradePrompt.Evaluate(
            TranslatorEngine.MiLMMT,
            Target1BQ8,
            p => p == Target1BQ8 || p == Legacy1BQ8);

        Assert.Equal(StartupMiLMMTUpgradeAction.None, plan.Action);
    }

    [Fact]
    public void Evaluate_ReturnsUpgradeFromLegacyWhenOnlyV01Exists()
    {
        var plan = StartupMiLMMTUpgradePrompt.Evaluate(
            TranslatorEngine.MiLMMT,
            Target1BQ8,
            p => p == Legacy1BQ8);

        Assert.Equal(StartupMiLMMTUpgradeAction.UpgradeFromLegacyFallback, plan.Action);
        Assert.Equal(Target1BQ8, plan.TargetProfile);
        Assert.Equal([Legacy1BQ8], plan.LegacyFallbackProfiles);
    }

    [Fact]
    public void Evaluate_ReturnsDownloadMissingModelWhenNoModelExists()
    {
        var plan = StartupMiLMMTUpgradePrompt.Evaluate(
            TranslatorEngine.MiLMMT,
            Target1BQ8,
            _ => false);

        Assert.Equal(StartupMiLMMTUpgradeAction.DownloadMissingModel, plan.Action);
        Assert.Equal(Target1BQ8, plan.TargetProfile);
        Assert.Empty(plan.LegacyFallbackProfiles);
    }

    [Fact]
    public async Task RunIfNeededAsync_UpgradeAcceptedAndCleanupAccepted_DownloadsAndDeletesLegacy()
    {
        var prompt = new StartupMiLMMTUpgradePrompt();
        var downloaded = new List<MiLMMTModelProfile>();
        var deleted = new List<MiLMMTModelProfile>();
        var fellBackToPapago = false;
        long reportedBytes = 0;

        await prompt.RunIfNeededAsync(
            TranslatorEngine.MiLMMT,
            Target1BQ8,
            p => p == Legacy1BQ8 || downloaded.Contains(p),
            (_, _) => Task.FromResult(true),
            _ => throw new InvalidOperationException("Unexpected missing-model prompt."),
            profile =>
            {
                downloaded.Add(profile);
                return true;
            },
            (_, totalBytes) =>
            {
                reportedBytes = totalBytes;
                return Task.FromResult(true);
            },
            profiles =>
            {
                deleted.AddRange(profiles);
                return Task.CompletedTask;
            },
            () => fellBackToPapago = true,
            p => p.FileSize);

        Assert.Equal([Target1BQ8], downloaded);
        Assert.Equal(Legacy1BQ8.FileSize, reportedBytes);
        Assert.Equal([Legacy1BQ8], deleted);
        Assert.False(fellBackToPapago);
    }

    [Fact]
    public async Task RunIfNeededAsync_UpgradePostponed_KeepsV01FallbackWithoutSwitchingEngine()
    {
        var prompt = new StartupMiLMMTUpgradePrompt();
        var downloadCalled = false;
        var cleanupCalled = false;
        var fellBackToPapago = false;

        await prompt.RunIfNeededAsync(
            TranslatorEngine.MiLMMT,
            Target1BQ8,
            p => p == Legacy1BQ8,
            (_, _) => Task.FromResult(false),
            _ => throw new InvalidOperationException("Unexpected missing-model prompt."),
            _ =>
            {
                downloadCalled = true;
                return true;
            },
            (_, _) =>
            {
                cleanupCalled = true;
                return Task.FromResult(true);
            },
            _ => Task.CompletedTask,
            () => fellBackToPapago = true);

        Assert.False(downloadCalled);
        Assert.False(cleanupCalled);
        Assert.False(fellBackToPapago);
    }

    [Fact]
    public async Task RunIfNeededAsync_UpgradeDownloadFails_SkipsCleanupAndKeepsV01Fallback()
    {
        var prompt = new StartupMiLMMTUpgradePrompt();
        var cleanupCalled = false;
        var fellBackToPapago = false;

        await prompt.RunIfNeededAsync(
            TranslatorEngine.MiLMMT,
            Target1BQ8,
            p => p == Legacy1BQ8,
            (_, _) => Task.FromResult(true),
            _ => throw new InvalidOperationException("Unexpected missing-model prompt."),
            _ => false,
            (_, _) =>
            {
                cleanupCalled = true;
                return Task.FromResult(true);
            },
            _ => Task.CompletedTask,
            () => fellBackToPapago = true);

        Assert.False(cleanupCalled);
        Assert.False(fellBackToPapago);
    }

    [Fact]
    public async Task RunIfNeededAsync_NoModelAtAllAndDeclined_FallsBackToPapago()
    {
        var prompt = new StartupMiLMMTUpgradePrompt();
        var fellBackToPapago = false;

        await prompt.RunIfNeededAsync(
            TranslatorEngine.MiLMMT,
            Target1BQ8,
            _ => false,
            (_, _) => throw new InvalidOperationException("Unexpected legacy upgrade prompt."),
            _ => Task.FromResult(false),
            _ => true,
            (_, _) => Task.FromResult(false),
            _ => Task.CompletedTask,
            () => fellBackToPapago = true);

        Assert.True(fellBackToPapago);
    }

    [Fact]
    public async Task DeleteModelFileAsync_UnloadsBeforeDeletingFileAndTempFileAndEmptyDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"iw-milmmt-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var profile = new MiLMMTModelProfile(
                MiLMMTModelSize.MiLMMT_1B,
                MiLMMTQuantization.Q8_0,
                "test/repo",
                "model.gguf",
                4,
                "0000",
                1.0,
                [TranslationLanguageCode.Korean],
                "note",
                DirectoryNameOverride: tempRoot);

            File.WriteAllText(profile.FilePath, "data");
            File.WriteAllText($"{profile.FilePath}.download", "part");

            var events = new List<string>();
            await StartupMiLMMTUpgradePrompt.DeleteModelFileAsync(
                profile,
                path =>
                {
                    Assert.True(File.Exists(path));
                    events.Add($"unload:{path}");
                    return Task.CompletedTask;
                });

            Assert.Equal([$"unload:{profile.FilePath}"], events);
            Assert.False(File.Exists(profile.FilePath));
            Assert.False(File.Exists($"{profile.FilePath}.download"));
            Assert.False(Directory.Exists(tempRoot));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
