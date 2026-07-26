using IronworksTranslator.Models.ReleaseNotes;
using IronworksTranslator.Utils;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace IronworksTranslator.Services
{
    public sealed class GitHubReleaseNotesService
    {
        private static readonly Uri ReleasesUri = new(
            "https://api.github.com/repos/sappho192/IronworksTranslator/releases?per_page=100");
        private static readonly HttpClient SharedHttpClient = CreateHttpClient();

        private readonly HttpClient _httpClient;

        public GitHubReleaseNotesService()
            : this(SharedHttpClient)
        {
        }

        internal GitHubReleaseNotesService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public Task<IReadOnlyList<GitHubReleaseNote>> GetRecentAsync(
            ReleaseChannelInfo releaseChannel,
            CancellationToken cancellationToken = default)
        {
            return GetRecentAsync(releaseChannel, 5, cancellationToken);
        }

        internal async Task<IReadOnlyList<GitHubReleaseNote>> GetRecentAsync(
            ReleaseChannelInfo releaseChannel,
            int maximumCount,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(releaseChannel);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumCount, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumCount, 5);

            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesUri);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.UserAgent.ParseAdd("IronworksTranslator/1.0");
            request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var releases = await response.Content.ReadFromJsonAsync<List<GitHubReleaseResponse>>(
                cancellationToken: cancellationToken) ?? [];
            return releases
                .Where(release =>
                    !release.Draft
                    && !string.IsNullOrWhiteSpace(release.TagName)
                    && IsReleaseForChannel(release, releaseChannel))
                .OrderByDescending(release => release.PublishedAt ?? release.CreatedAt)
                .Take(maximumCount)
                .Select(release => new GitHubReleaseNote(
                    release.TagName!.Trim(),
                    release.Name?.Trim() ?? string.Empty,
                    release.Body ?? string.Empty,
                    release.HtmlUrl ?? string.Empty,
                    release.PublishedAt ?? release.CreatedAt ?? DateTimeOffset.MinValue,
                    release.Prerelease))
                .ToArray();
        }

        private static bool IsReleaseForChannel(
            GitHubReleaseResponse release,
            ReleaseChannelInfo releaseChannel)
        {
            if (!releaseChannel.IncludePrereleases)
            {
                return !release.Prerelease;
            }

            return release.Prerelease
                && release.TagName!.Contains("-beta.", StringComparison.OrdinalIgnoreCase);
        }

        private static HttpClient CreateHttpClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10),
            };
        }

        private sealed class GitHubReleaseResponse
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; init; }

            [JsonPropertyName("name")]
            public string? Name { get; init; }

            [JsonPropertyName("body")]
            public string? Body { get; init; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; init; }

            [JsonPropertyName("draft")]
            public bool Draft { get; init; }

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; init; }

            [JsonPropertyName("created_at")]
            public DateTimeOffset? CreatedAt { get; init; }

            [JsonPropertyName("published_at")]
            public DateTimeOffset? PublishedAt { get; init; }
        }
    }
}
