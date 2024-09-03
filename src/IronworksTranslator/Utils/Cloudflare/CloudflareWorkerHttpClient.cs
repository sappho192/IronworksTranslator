using System.Net.Http;

namespace IronworksTranslator.Utils.Cloudflare
{
    public class CloudflareWorkerHttpClient
    {
        private readonly HttpClient _httpClient;

        public CloudflareWorkerHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode && IsCloudflareWorkerError(response))
            {
                throw new CloudflareWorkerException(
                    GetErrorCodeFromResponse(response).GetAwaiter().GetResult(),
                    GetErrorMessageFromResponse(response).GetAwaiter().GetResult());
            }

            return response;
        }

        private bool IsCloudflareWorkerError(HttpResponseMessage response)
        {
            // Check if the response contains Cloudflare-specific headers
            return response.Headers.Contains("CF-RAY") &&
                   response.Content.Headers.ContentType.MediaType == "text/html";
        }

        private async Task<int> GetErrorCodeFromResponse(HttpResponseMessage response)
        {
            // Parse the HTML content to extract the error code
            var htmlContent = await response.Content.ReadAsStringAsync();
            var errorCodeMatch = System.Text.RegularExpressions.Regex.Match(htmlContent, @"<title>Error (\d+)</title>");

            if (errorCodeMatch.Success && int.TryParse(errorCodeMatch.Groups[1].Value, out var errorCode))
            {
                return errorCode;
            }

            throw new Exception("Unable to parse error code from Cloudflare Worker response");
        }

        private async Task<string> GetErrorMessageFromResponse(HttpResponseMessage response)
        {
            // Parse the HTML content to extract the error message
            var htmlContent = await response.Content.ReadAsStringAsync();
            var errorMessageMatch = System.Text.RegularExpressions.Regex.Match(htmlContent, @"<p class=""error-message"">(.*)</p>");

            if (errorMessageMatch.Success)
            {
                return errorMessageMatch.Groups[1].Value.Trim();
            }

            throw new Exception("Unable to parse error message from Cloudflare Worker response");
        }
    }
}
