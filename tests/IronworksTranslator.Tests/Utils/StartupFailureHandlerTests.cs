using IronworksTranslator.Utils;

namespace IronworksTranslator.Tests.Utils;

public class StartupFailureHandlerTests
{
    [Fact]
    public async Task HandleAsync_NotifiesThenCleansUpThenShutsDown()
    {
        var events = new List<string>();
        var failures = new List<(Exception Exception, StartupFailureStage Stage)>();

        await StartupFailureHandler.HandleAsync(
            () => events.Add("notify"),
            () =>
            {
                events.Add("cleanup");
                return Task.CompletedTask;
            },
            exitCode => events.Add($"shutdown:{exitCode}"),
            (exception, stage) => failures.Add((exception, stage)));

        Assert.Equal(
            ["notify", "cleanup", $"shutdown:{StartupFailureHandler.ExitCode}"],
            events);
        Assert.Empty(failures);
    }

    [Fact]
    public async Task HandleAsync_StillCleansUpAndShutsDownWhenNotificationFails()
    {
        var events = new List<string>();
        var failures = new List<(Exception Exception, StartupFailureStage Stage)>();

        await StartupFailureHandler.HandleAsync(
            () => throw new InvalidOperationException("popup failed"),
            () =>
            {
                events.Add("cleanup");
                return Task.CompletedTask;
            },
            exitCode => events.Add($"shutdown:{exitCode}"),
            (exception, stage) => failures.Add((exception, stage)));

        Assert.Equal(
            ["cleanup", $"shutdown:{StartupFailureHandler.ExitCode}"],
            events);
        var failure = Assert.Single(failures);
        Assert.Equal(StartupFailureStage.UserNotification, failure.Stage);
        Assert.Equal("popup failed", failure.Exception.Message);
    }

    [Fact]
    public async Task HandleAsync_StillShutsDownWhenCleanupFails()
    {
        var events = new List<string>();
        var failures = new List<(Exception Exception, StartupFailureStage Stage)>();

        await StartupFailureHandler.HandleAsync(
            () => events.Add("notify"),
            () => throw new InvalidOperationException("cleanup failed"),
            exitCode => events.Add($"shutdown:{exitCode}"),
            (exception, stage) => failures.Add((exception, stage)));

        Assert.Equal(
            ["notify", $"shutdown:{StartupFailureHandler.ExitCode}"],
            events);
        var failure = Assert.Single(failures);
        Assert.Equal(StartupFailureStage.Cleanup, failure.Stage);
        Assert.Equal("cleanup failed", failure.Exception.Message);
    }
}
