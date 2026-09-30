using CommunityToolkit.Mvvm.Messaging;
using IronworksTranslator.Models.ReleaseNotes;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Services;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Tests.Services;

public class StartupReleaseNotesPromptTests : IDisposable
{
    private readonly UISettings _settings = new();
    private readonly GitHubReleaseNote _latest = new("2.2.0", "Latest", "Notes", "", DateTimeOffset.UtcNow, false);

    [Fact]
    public async Task NewVersion_ShowsLatestOnceAndRemembersAcrossRestart()
    {
        _settings.LastReleaseNotesAppVersion = "2.1.0";
        int shown = 0;
        var prompt = new StartupReleaseNotesPrompt();
        await prompt.ShowIfNeededAsync(_settings, "2.2.0", LoadLatest, release =>
        {
            Assert.Same(_latest, release);
            shown++;
            return Task.CompletedTask;
        });
        Assert.Equal("2.2.0", _settings.LastReleaseNotesAppVersion);

        var yaml = new SerializerBuilder().Build().Serialize(_settings);
        var restored = new DeserializerBuilder().Build().Deserialize<UISettings>(yaml);
        try
        {
            await new StartupReleaseNotesPrompt().ShowIfNeededAsync(restored, "2.2.0",
                () => throw new InvalidOperationException("Already seen versions must not fetch."),
                _ => { shown++; return Task.CompletedTask; });
            Assert.Equal(1, shown);

            await prompt.ShowIfNeededAsync(_settings, "2.3.0", LoadLatest,
                _ => { shown++; return Task.CompletedTask; });
            Assert.Equal(2, shown);
            Assert.Equal("2.3.0", _settings.LastReleaseNotesAppVersion);
        }
        finally { WeakReferenceMessenger.Default.UnregisterAll(restored); }
    }

    [Fact]
    public async Task OldSettingsWithoutRecord_ShowNotesOnFirstLaunch()
    {
        Assert.Equal(string.Empty, _settings.LastReleaseNotesAppVersion);
        int shown = 0;
        await new StartupReleaseNotesPrompt().ShowIfNeededAsync(_settings, "2.2.0", LoadLatest,
            _ => { shown++; return Task.CompletedTask; });
        Assert.Equal(1, shown);
    }

    [Fact]
    public async Task DialogStillOpen_IsNotRecordedAndConcurrentPromptsAreSkipped()
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompt = new StartupReleaseNotesPrompt();
        int shown = 0;
        var first = prompt.ShowIfNeededAsync(_settings, "2.2.0", LoadLatest,
            _ => { shown++; return closed.Task; });
        Assert.Equal(string.Empty, _settings.LastReleaseNotesAppVersion);
        await prompt.ShowIfNeededAsync(_settings, "2.2.0", LoadLatest,
            _ => { shown++; return Task.CompletedTask; });
        Assert.Equal(1, shown);
        closed.SetResult();
        await first;
        Assert.Equal("2.2.0", _settings.LastReleaseNotesAppVersion);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("fetch_failure")]
    [InlineData("dialog_failure")]
    public async Task FailureOrEmptyList_DoesNotRecordAndCanRetry(string failure)
    {
        _settings.LastReleaseNotesAppVersion = "2.1.0";
        var prompt = new StartupReleaseNotesPrompt();
        await prompt.ShowIfNeededAsync(_settings, "2.2.0",
            () => failure switch
            {
                "empty" => Task.FromResult<GitHubReleaseNote?>(null),
                "fetch_failure" => throw new HttpRequestException("Offline"),
                _ => LoadLatest()
            },
            _ => throw new InvalidOperationException("Dialog failed"));
        Assert.Equal("2.1.0", _settings.LastReleaseNotesAppVersion);

        int shown = 0;
        await prompt.ShowIfNeededAsync(_settings, "2.2.0", LoadLatest,
            _ => { shown++; return Task.CompletedTask; });
        Assert.Equal(1, shown);
        Assert.Equal("2.2.0", _settings.LastReleaseNotesAppVersion);
    }

    [Fact]
    public async Task BetaVersionChange_IsDetectedSeparately()
    {
        _settings.LastReleaseNotesAppVersion = "2.2.0-beta.1";
        await new StartupReleaseNotesPrompt().ShowIfNeededAsync(_settings, "2.2.0-beta.2", LoadLatest,
            _ => Task.CompletedTask);
        Assert.Equal("2.2.0-beta.2", _settings.LastReleaseNotesAppVersion);
    }

    [Fact]
    public async Task UnknownAppVersion_DoesNotShow()
    {
        await new StartupReleaseNotesPrompt().ShowIfNeededAsync(_settings, "",
            () => throw new InvalidOperationException("Unknown versions must not fetch."),
            _ => throw new InvalidOperationException("Unknown versions must not show."));
        Assert.Equal(string.Empty, _settings.LastReleaseNotesAppVersion);
    }

    private Task<GitHubReleaseNote?> LoadLatest() => Task.FromResult<GitHubReleaseNote?>(_latest);

    public void Dispose() => WeakReferenceMessenger.Default.UnregisterAll(_settings);
}
