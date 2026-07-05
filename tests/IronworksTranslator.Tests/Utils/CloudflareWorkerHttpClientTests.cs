using System.Net;
using IronworksTranslator.Utils.Cloudflare;

namespace IronworksTranslator.Tests.Utils;

public class CloudflareWorkerHttpClientTests
{
    [Fact]
    public async Task SendAsync_ThrowsCloudflareWorkerExceptionForCloudflareHtmlError()
    {
        const string html = """
            <html>
              <head><title>Error 1101</title></head>
              <body><p class="error-message">Worker threw exception</p></body>
            </html>
            """;
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(html)
        };
        response.Headers.Add("CF-RAY", "test-ray");
        response.Content.Headers.ContentType = new("text/html");
        var client = new CloudflareWorkerHttpClient(new HttpClient(new StubHttpMessageHandler(response)));

        var exception = await Assert.ThrowsAsync<CloudflareWorkerException>(
            () => client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test")));

        Assert.Equal(1101, exception.ErrorCode);
        Assert.Equal("Worker threw exception", exception.ErrorMessage);
    }

    [Fact]
    public async Task SendAsync_ReturnsNonCloudflareErrorResponse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("plain error")
        };
        var client = new CloudflareWorkerHttpClient(new HttpClient(new StubHttpMessageHandler(response)));

        var result = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.test"));

        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }
}
