using System.Net.Http;
using System.Text.RegularExpressions;

namespace IronworksTranslator.Utils.Cloudflare
{
    public class CloudflareWorkerHttpClient
    {
        private readonly HttpClient _httpClient;

        public CloudflareWorkerHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode && IsCloudflareWorkerError(response))
            {
                var htmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorCode = ParseErrorCode(htmlContent);
                var errorMessage = ParseErrorMessage(htmlContent);
                throw new CloudflareWorkerException(errorCode, errorMessage);
            }

            return response;
        }

        private static bool IsCloudflareWorkerError(HttpResponseMessage response)
        {
            return response.Headers.Contains("CF-RAY")
                && string.Equals(
                    response.Content?.Headers?.ContentType?.MediaType,
                    "text/html",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseErrorCode(string htmlContent)
        {
            var errorCodeMatch = Regex.Match(htmlContent, @"<title>Error (\d+)</title>");
            if (errorCodeMatch.Success && int.TryParse(errorCodeMatch.Groups[1].Value, out var errorCode))
            {
                return errorCode;
            }

            throw new InvalidOperationException("Unable to parse error code from Cloudflare Worker response");
        }

        private static string ParseErrorMessage(string htmlContent)
        {
            var errorMessageMatch = Regex.Match(htmlContent, @"<p class=""error-message"">(.*?)</p>", RegexOptions.Singleline);
            if (errorMessageMatch.Success)
            {
                return errorMessageMatch.Groups[1].Value.Trim();
            }

            throw new InvalidOperationException("Unable to parse error message from Cloudflare Worker response");
        }
    }
}
