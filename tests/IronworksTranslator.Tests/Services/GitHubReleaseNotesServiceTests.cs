using IronworksTranslator.Services;
using IronworksTranslator.Utils;
using System.Net;
using System.Net.Http;
using System.Text;

namespace IronworksTranslator.Tests.Services;

public class GitHubReleaseNotesServiceTests
{
    [Fact]
    public async Task GetRecentAsync_BetaChannelReturnsNewestFivePrereleases()
    {
        var handler = new StubHttpMessageHandler(CreateResponse(
            """
            [
              {"tag_name":"1.3.0-beta.1","name":"Beta 1","body":"one","html_url":"https://github.com/example/1","draft":false,"prerelease":true,"published_at":"2026-07-01T00:00:00Z"},
              {"tag_name":"1.2.0","name":"Stable","body":"stable","html_url":"https://github.com/example/stable","draft":false,"prerelease":false,"published_at":"2026-07-10T00:00:00Z"},
              {"tag_name":"1.3.0-beta.6","name":"Beta 6","body":"six","html_url":"https://github.com/example/6","draft":false,"prerelease":true,"published_at":"2026-07-06T00:00:00Z"},
              {"tag_name":"1.3.0-beta.5","name":"Beta 5","body":"five","html_url":"https://github.com/example/5","draft":false,"prerelease":true,"published_at":"2026-07-05T00:00:00Z"},
              {"tag_name":"1.3.0-beta.4","name":"Beta 4","body":"four","html_url":"https://github.com/example/4","draft":false,"prerelease":true,"published_at":"2026-07-04T00:00:00Z"},
              {"tag_name":"1.3.0-beta.3","name":"Beta 3","body":"three","html_url":"https://github.com/example/3","draft":false,"prerelease":true,"published_at":"2026-07-03T00:00:00Z"},
              {"tag_name":"1.3.0-beta.2","name":"Beta 2","body":"two","html_url":"https://github.com/example/2","draft":false,"prerelease":true,"published_at":"2026-07-02T00:00:00Z"},
              {"tag_name":"1.0.1","name":"Legacy prerelease","body":"legacy","html_url":"https://github.com/example/legacy","draft":false,"prerelease":true,"published_at":"2026-07-08T00:00:00Z"},
              {"tag_name":"1.3.0-beta.7","name":"Draft","body":"draft","html_url":"https://github.com/example/draft","draft":true,"prerelease":true,"published_at":"2026-07-07T00:00:00Z"}
            ]
            """));
        var service = new GitHubReleaseNotesService(new HttpClient(handler));
        var channel = new ReleaseChannelInfo("Beta", "beta", includePrereleases: true);

        var releases = await service.GetRecentAsync(
            channel,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["1.3.0-beta.6", "1.3.0-beta.5", "1.3.0-beta.4", "1.3.0-beta.3", "1.3.0-beta.2"],
            releases.Select(release => release.TagName));
        Assert.All(releases, release => Assert.True(release.IsPrerelease));
        Assert.Equal("application/vnd.github+json", handler.Accept);
        Assert.Equal("2026-03-10", handler.ApiVersion);
        Assert.Contains("IronworksTranslator", handler.UserAgent);
    }

    [Fact]
    public async Task GetRecentAsync_StableChannelExcludesPrereleasesAndDrafts()
    {
        var handler = new StubHttpMessageHandler(CreateResponse(
            """
            [
              {"tag_name":"1.2.0","name":"1.2.0","body":null,"html_url":"https://github.com/example/stable","draft":false,"prerelease":false,"created_at":"2026-07-01T00:00:00Z"},
              {"tag_name":"1.3.0-beta.1","name":"Beta","body":"beta","html_url":"https://github.com/example/beta","draft":false,"prerelease":true,"published_at":"2026-07-02T00:00:00Z"},
              {"tag_name":"1.1.0","name":"Draft","body":"draft","html_url":"https://github.com/example/draft","draft":true,"prerelease":false,"published_at":"2026-06-01T00:00:00Z"}
            ]
            """));
        var service = new GitHubReleaseNotesService(new HttpClient(handler));
        var channel = new ReleaseChannelInfo("Stable", "win", includePrereleases: false);

        var release = Assert.Single(await service.GetRecentAsync(
            channel,
            TestContext.Current.CancellationToken));

        Assert.Equal("1.2.0", release.DisplayTitle);
        Assert.Equal(string.Empty, release.Body);
        Assert.False(release.IsPrerelease);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), release.PublishedAt);
    }

    [Fact]
    public async Task GetRecentAsync_HttpFailureIsReported()
    {
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Forbidden));
        var service = new GitHubReleaseNotesService(new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetRecentAsync(
                BuildInfo.ReleaseChannel,
                TestContext.Current.CancellationToken));
    }

    private static HttpResponseMessage CreateResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string Accept { get; private set; } = string.Empty;

        public string ApiVersion { get; private set; } = string.Empty;

        public string UserAgent { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Accept = string.Join(",", request.Headers.Accept);
            ApiVersion = string.Join(",", request.Headers.GetValues("X-GitHub-Api-Version"));
            UserAgent = string.Join(",", request.Headers.UserAgent);
            return Task.FromResult(response);
        }
    }
}
