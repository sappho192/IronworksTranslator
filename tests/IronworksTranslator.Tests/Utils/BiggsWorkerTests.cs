using IronworksTranslator.Models.Cloudflare;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Cloudflare;
using IronworksTranslator.Utils.Translator;
using System.Net;
using System.Text.Json;

namespace IronworksTranslator.Tests.Utils;

public class BiggsWorkerTests
{
    [Fact]
    public async Task SubmitReportAsync_ResolvesEndpointOnceAndPostsV2Payload()
    {
        var requestedUrls = new List<string>();
        string? capturedBody = null;

        using var handler = new DelegateHttpMessageHandler(async (request, cancellationToken) =>
        {
            var url = request.RequestUri!.ToString();
            requestedUrls.Add(url);

            if (url == "https://pointer.test/database.json")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"endpoints":[{"name":"current","url":"https://worker.test","protocol":"https","port":443}]}
                        """),
                };
            }

            if (url == "https://worker.test/v2/reports")
            {
                capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"ok":true,"id":"rep_1"}"""),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var worker = new BiggsWorker(httpClient, "https://pointer.test/database.json");
        var payload = CreateSamplePayload();

        var first = await worker.SubmitReportAsync(payload);
        var second = await worker.SubmitReportAsync(payload);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(
            ["https://pointer.test/database.json", "https://worker.test/v2/reports", "https://worker.test/v2/reports"],
            requestedUrls);
        Assert.NotNull(capturedBody);

        using var doc = JsonDocument.Parse(capturedBody);
        Assert.Equal(2, doc.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal("chat", doc.RootElement.GetProperty("translation_kind").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("npc_speaker").ValueKind);
        Assert.Equal("MiLMMT", doc.RootElement.GetProperty("engine").GetString());
        Assert.Equal("papago", doc.RootElement.GetProperty("draft_source").GetString());
        Assert.True(doc.RootElement.GetProperty("draft_edited").GetBoolean());
    }

    [Fact]
    public async Task SubmitReportAsync_ReResolvesRotatedEndpointAfterFailureAndRetriesOnce()
    {
        var requestedUrls = new List<string>();
        var pointerReads = 0;

        using var handler = new DelegateHttpMessageHandler((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            requestedUrls.Add(url);

            if (url == "https://pointer.test/database.json")
            {
                var workerUrl = ++pointerReads == 1 ? "https://old-worker.test" : "https://new-worker.test";
                return Task.FromResult(PointerResponse(workerUrl));
            }

            if (url == "https://old-worker.test/v2/reports")
            {
                throw new HttpRequestException("Old worker is gone.");
            }

            return Task.FromResult(url == "https://new-worker.test/v2/reports"
                ? new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("""{"ok":true}""") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        using var httpClient = new HttpClient(handler);
        using var worker = new BiggsWorker(httpClient, "https://pointer.test/database.json");

        var first = await worker.SubmitReportAsync(CreateSamplePayload(), TestContext.Current.CancellationToken);
        var second = await worker.SubmitReportAsync(CreateSamplePayload(), TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(
            [
                "https://pointer.test/database.json",
                "https://old-worker.test/v2/reports",
                "https://pointer.test/database.json",
                "https://new-worker.test/v2/reports",
                "https://new-worker.test/v2/reports",
            ],
            requestedUrls);
    }

    [Fact]
    public async Task SubmitReportAsync_DoesNotRepostWhenPointerStillNamesFailedEndpoint()
    {
        var requestedUrls = new List<string>();

        using var handler = new DelegateHttpMessageHandler((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            requestedUrls.Add(url);

            return Task.FromResult(url == "https://pointer.test/database.json"
                ? PointerResponse("https://worker.test")
                : new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"ok":false}""") });
        });

        using var httpClient = new HttpClient(handler);
        using var worker = new BiggsWorker(httpClient, "https://pointer.test/database.json");

        var result = await worker.SubmitReportAsync(CreateSamplePayload(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("HTTP 400", result.ErrorMessage);
        Assert.Equal(
            [
                "https://pointer.test/database.json",
                "https://worker.test/v2/reports",
                "https://pointer.test/database.json",
            ],
            requestedUrls);
    }

    [Fact]
    public async Task SubmitReportAsync_ReturnsFailedResultWhenEndpointDiscoveryFails()
    {
        using var handler = new DelegateHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var httpClient = new HttpClient(handler);
        using var worker = new BiggsWorker(httpClient, "https://pointer.test/database.json");

        var result = await worker.SubmitReportAsync(CreateSamplePayload());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task SubmitReportAsync_HandlesCloudflareHtmlErrorWithoutThrowing()
    {
        const string html = """
            <html>
              <head><title>Error 1027</title></head>
              <body><p class="error-message">Daily limit exceeded</p></body>
            </html>
            """;

        using var handler = new DelegateHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.ToString() == "https://pointer.test/database.json")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"endpoints":[{"name":"current","url":"https://worker.test","protocol":"https","port":443}]}
                        """),
                });
            }

            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(html),
            };
            response.Headers.Add("CF-RAY", "test-ray");
            response.Content.Headers.ContentType = new("text/html");
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler);
        using var worker = new BiggsWorker(httpClient, "https://pointer.test/database.json");

        var result = await worker.SubmitReportAsync(CreateSamplePayload());

        Assert.False(result.IsSuccess);
        Assert.Contains("1027", result.ErrorMessage);
    }

    private static HttpResponseMessage PointerResponse(string workerUrl) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"endpoints":[{"name":"current","url":"{{workerUrl}}","protocol":"https","port":443}]}"""),
        };

    private static BiggsReportRequest CreateSamplePayload()
    {
        return BiggsReportRequest.Create(
            appVersion: "2.3.0",
            translationKind: MiLMMTTranslationKind.Chat,
            originalNpcSpeaker: "ShouldBeStripped",
            sourceLanguage: TranslationLanguageCode.Japanese,
            targetLanguage: TranslationLanguageCode.Korean,
            sourceText: "暁の血盟の皆",
            engine: TranslatorEngine.MiLMMT,
            modelId: "MiLMMT-46-1B-v1.0.Q4_K_M.gguf",
            modelSha256: "abc123",
            originalTranslation: "새벽의 혈맹 모두",
            suggestedTranslation: "새벽의 혈맹 동료들",
            draftSource: "papago",
            draftEdited: true,
            glossaryTerms: [new BiggsGlossaryTerm { Source = "暁の血盟", Target = "새벽의 혈맹" }],
            comment: "테스트");
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
