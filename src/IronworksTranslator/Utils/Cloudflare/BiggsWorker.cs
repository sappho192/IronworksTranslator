using IronworksTranslator.Models.Cloudflare;
using IronworksTranslator.Utils.Aspect;
using Serilog;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace IronworksTranslator.Utils.Cloudflare
{
    public class BiggsWorker : IDisposable
    {
        public const string DefaultPointerUrl = "https://hermes.sapphosound.com/database.json";

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly CloudflareWorkerHttpClient _cloudflareClient;
        private readonly string _pointerUrl;
        private readonly SemaphoreSlim _endpointLock = new(1, 1);
        private BiggsEndPoint? _endpoint;
        private bool _disposed;

        public BiggsWorker() : this(null, DefaultPointerUrl)
        {
        }

        public BiggsWorker(HttpClient? httpClient, string? pointerUrl = null)
        {
            if (httpClient == null)
            {
                _httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(15),
                };
                _ownsHttpClient = true;
            }
            else
            {
                _httpClient = httpClient;
                _ownsHttpClient = false;
            }

            _cloudflareClient = new CloudflareWorkerHttpClient(_httpClient);
            _pointerUrl = string.IsNullOrWhiteSpace(pointerUrl) ? DefaultPointerUrl : pointerUrl;
        }

        [TraceMethod]
        public async Task<BiggsSubmitResult> SubmitReportAsync(
            BiggsReportRequest body,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoint = await ResolveEndpointAsync(cancellationToken);
                if (endpoint == null || string.IsNullOrWhiteSpace(endpoint.url))
                {
                    return BiggsSubmitResult.Failed("Failed to resolve report endpoint.");
                }

                var result = await PostReportAsync(endpoint, body, cancellationToken);
                if (result.IsSuccess)
                {
                    return result;
                }

                // The cached endpoint lives for the whole app session, so a rotated pointer would
                // otherwise strand running clients on the old worker. Drop it and re-read the pointer;
                // retry once only when it now names a different worker, so a request the same
                // worker rejected is never posted twice.
                InvalidateEndpoint(endpoint);
                var refreshed = await ResolveEndpointAsync(cancellationToken);
                if (refreshed == null
                    || string.IsNullOrWhiteSpace(refreshed.url)
                    || IsSameEndpoint(refreshed, endpoint))
                {
                    return result;
                }

                Log.Information("Report endpoint changed after a failed submission; retrying on the new endpoint.");
                return await PostReportAsync(refreshed, body, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return BiggsSubmitResult.Failed("Cancelled");
            }
            catch (Exception ex)
            {
                // e.g. an HttpClient timeout while reading the pointer.
                Log.Error(ex, "BiggsWorker report submission failed.");
                return BiggsSubmitResult.Failed(ex.Message);
            }
        }

        private async Task<BiggsSubmitResult> PostReportAsync(
            BiggsEndPoint endpoint,
            BiggsReportRequest body,
            CancellationToken cancellationToken)
        {
            try
            {
                var requestUrl = BuildReportUrl(endpoint);
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(body),
                        Encoding.UTF8,
                        "application/json"),
                };

                using var response = await _cloudflareClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return BiggsSubmitResult.Ok();
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Error("BiggsWorker report submission failed with {StatusCode}: {Body}", response.StatusCode, responseBody);
                return BiggsSubmitResult.Failed($"HTTP {(int)response.StatusCode}");
            }
            catch (CloudflareWorkerException ex)
            {
                LogCloudflareWorkerError(ex);
                return BiggsSubmitResult.Failed($"Cloudflare Worker Error {ex.ErrorCode}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                Log.Error(ex, "BiggsWorker report submission failed.");
                return BiggsSubmitResult.Failed(ex.Message);
            }
        }

        private static string BuildReportUrl(BiggsEndPoint endpoint) =>
            $"{endpoint.url.TrimEnd('/')}/v2/reports";

        private static bool IsSameEndpoint(BiggsEndPoint left, BiggsEndPoint right) =>
            string.Equals(BuildReportUrl(left), BuildReportUrl(right), StringComparison.OrdinalIgnoreCase);

        private void InvalidateEndpoint(BiggsEndPoint failed)
        {
            // Only clear the cache if no concurrent submission has already replaced it.
            Interlocked.CompareExchange(ref _endpoint, null, failed);
        }

        private async Task<BiggsEndPoint?> ResolveEndpointAsync(CancellationToken cancellationToken)
        {
            if (_endpoint != null)
            {
                return _endpoint;
            }

            await _endpointLock.WaitAsync(cancellationToken);
            try
            {
                if (_endpoint != null)
                {
                    return _endpoint;
                }

                using var response = await _httpClient.GetAsync(_pointerUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Failed to fetch endpoint list from {PointerUrl}: {StatusCode}", _pointerUrl, response.StatusCode);
                    return null;
                }

                var responseStr = await response.Content.ReadAsStringAsync(cancellationToken);
                var endpointList = JsonSerializer.Deserialize<BiggsEndPointList>(responseStr);
                var current = endpointList?.endpoints?.FirstOrDefault(
                    endpoint => string.Equals(endpoint.name, "current", StringComparison.OrdinalIgnoreCase));
                if (current == null || string.IsNullOrWhiteSpace(current.url))
                {
                    Log.Error("Endpoint list from {PointerUrl} did not contain a valid 'current' endpoint.", _pointerUrl);
                    return null;
                }

                _endpoint = current;
                return _endpoint;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error(ex, "Failed to resolve BiggsWorker endpoint from {PointerUrl}.", _pointerUrl);
                return null;
            }
            finally
            {
                _endpointLock.Release();
            }
        }

        private static void LogCloudflareWorkerError(CloudflareWorkerException ex)
        {
            Log.Error("Cloudflare Worker Error: Code {ErrorCode}, Message: {ErrorMessage}", ex.ErrorCode, ex.ErrorMessage);

            // from https://developers.cloudflare.com/workers/observability/errors/#_top
            var detail = ex.ErrorCode switch
            {
                1101 => "Worker threw a JavaScript exception.",
                1102 => "Worker exceeded CPU time limit.",
                1103 => "The owner of this worker needs to contact Cloudflare Support.",
                1015 => "Worker hit the burst rate limit.",
                1019 => "Worker hit loop limit.",
                1021 => "Worker has requested a host it cannot access.",
                1022 => "Cloudflare has failed to route the request to the Worker.",
                1024 => "Worker cannot make a subrequest to a Cloudflare-owned IP address.",
                1027 => "Worker exceeded free tier daily request limit.",
                1042 => "Worker tried to fetch from another Worker on the same zone, which is unsupported.",
                _ => "Unknown Cloudflare Worker error.",
            };
            Log.Error(detail);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _endpointLock.Dispose();
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }

            _disposed = true;
        }
    }
}
