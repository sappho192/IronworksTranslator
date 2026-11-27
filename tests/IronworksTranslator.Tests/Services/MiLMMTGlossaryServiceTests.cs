using IronworksTranslator.Models.Enums;
using IronworksTranslator.Services;
using IronworksTranslator.Utils.Cloudflare;
using IronworksTranslator.Utils.Translator;
using System.IO;
using System.Net;
using System.Net.Http.Headers;

namespace IronworksTranslator.Tests.Services;

public class MiLMMTGlossaryServiceTests
{
    private const string GlossaryPayloadV1 = """
        {
          "version": "2026-11-27.1",
          "pairs": {
            "Japanese->Korean": [
              { "source": "踊り子", "target": "무도가" }
            ]
          }
        }
        """;

    [Fact]
    public async Task RefreshAsync_DownloadsCachesAndUsesIfNoneMatchForNotModified()
    {
        var tempDir = CreateTempDirectory();
        var cachePath = Path.Combine(tempDir, "glossary.json");
        var metaPath = Path.Combine(tempDir, "glossary.meta.json");
        var observedIfNoneMatch = new List<string?>();

        try
        {
            using var handler = new DelegateHttpMessageHandler((request, _) =>
            {
                var url = request.RequestUri!.ToString();
                if (url == "https://pointer.test/database.json")
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"endpoints":[{"name":"current","url":"https://worker.test","protocol":"https","port":443}]}"""),
                    });
                }

                if (url == "https://worker.test/v2/glossary")
                {
                    var inm = request.Headers.IfNoneMatch.FirstOrDefault()?.ToString();
                    observedIfNoneMatch.Add(inm);
                    if (inm == "\"sha256:v1\"")
                    {
                        var notMod = new HttpResponseMessage(HttpStatusCode.NotModified);
                        notMod.Headers.ETag = new EntityTagHeaderValue("\"sha256:v1\"");
                        return Task.FromResult(notMod);
                    }

                    var ok = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(GlossaryPayloadV1),
                    };
                    ok.Headers.ETag = new EntityTagHeaderValue("\"sha256:v1\"");
                    ok.Headers.Add("X-Glossary-Version", "2026-11-27.1");
                    return Task.FromResult(ok);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            using var httpClient = new HttpClient(handler);
            using var worker = new BiggsWorker(httpClient, "https://pointer.test/database.json");
            using var service = new MiLMMTGlossaryService(worker, cachePath, metaPath);

            Assert.Equal(string.Empty, service.CurrentVersion);

            var firstOk = await service.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.True(firstOk);
            Assert.Equal("2026-11-27.1", service.CurrentVersion);
            Assert.Equal(1, service.TermCount);
            Assert.True(File.Exists(cachePath));
            Assert.True(File.Exists(metaPath));

            var matches = service.Match(
                TranslationLanguageCode.Japanese,
                TranslationLanguageCode.Korean,
                "ギアセット20「踊り子」に装備変更しました。");
            Assert.Equal([new MiLMMTGlossaryTerm("踊り子", "무도가")], matches);

            var secondOk = await service.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.True(secondOk);
            Assert.Equal([null, "\"sha256:v1\""], observedIfNoneMatch);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Constructor_LoadsOfflineDiskCache_AndPreservesItOnServerFailureOrBadJson()
    {
        var tempDir = CreateTempDirectory();
        var cachePath = Path.Combine(tempDir, "glossary.json");
        var metaPath = Path.Combine(tempDir, "glossary.meta.json");

        try
        {
            File.WriteAllText(cachePath, GlossaryPayloadV1);
            File.WriteAllText(metaPath, """{"Version":"2026-11-27.1","ETag":"\"sha256:v1\""}""");

            using var handler = new DelegateHttpMessageHandler((request, _) =>
            {
                var url = request.RequestUri!.ToString();
                if (url == "https://pointer.test/database.json")
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"endpoints":[{"name":"current","url":"https://worker.test","protocol":"https","port":443}]}"""),
                    });
                }

                // Return malformed JSON to verify cache is never clobbered.
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{corrupt-json"),
                });
            });

            using var httpClient = new HttpClient(handler);
            using var worker = new BiggsWorker(httpClient, "https://pointer.test/database.json");
            using var service = new MiLMMTGlossaryService(worker, cachePath, metaPath);

            Assert.Equal("2026-11-27.1", service.CurrentVersion);
            Assert.Equal(1, service.TermCount);

            var refreshed = await service.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.False(refreshed);
            Assert.Equal("2026-11-27.1", service.CurrentVersion);
            Assert.Equal(GlossaryPayloadV1, File.ReadAllText(cachePath));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"IronworksTranslator.GlossaryTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class DelegateHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
