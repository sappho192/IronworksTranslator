﻿using IronworksTranslator.Models;
using Serilog;
using System.Net.Http;
using System.Text.Json;

namespace IronworksTranslator.Utils.Cloudflare
{
    public class BiggsWorker
    {
        private readonly HttpClient _httpClient;
        private readonly CloudflareWorkerHttpClient _cloudflareClient;
        private const string _endpoint = "https://biggs.sapphosound.com";

        public BiggsWorker()
        {
            _httpClient = new HttpClient();
            _cloudflareClient = new CloudflareWorkerHttpClient(_httpClient);
        }

        public async Task Insert(BiggsBody body)
        {
            try
            {
                var response = await _cloudflareClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/insert")
                {
                    Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
                });

                if (response.IsSuccessStatusCode)
                {
                    // Process successful response
                    MessageBox.Show(Localizer.GetString("chat.report.send.success"));
                }
                else
                {
                    // Handle error
                    Log.Error($"Error: {response.StatusCode}");
                    Log.Error(await response.Content.ReadAsStringAsync());
                    MessageBox.Show(Localizer.GetString("chat.report.send.fail"));
                }
            }
            catch (CloudflareWorkerException ex)
            {
                Log.Error($"Cloudflare Worker Error: Code {ex.ErrorCode}");
                Log.Error($"Error Message: {ex.ErrorMessage}");

                // from https://developers.cloudflare.com/workers/observability/errors/#_top
                switch (ex.ErrorCode)
                {
                    case 1101:
                        Log.Error("Worker threw a JavaScript exception.");
                        break;
                    case 1102:
                        Log.Error("Worker exceeded CPU time limit.");
                        break;
                    case 1103:
                        Log.Error("The owner of this worker needs to contact Cloudflare Support");
                        break;
                    case 1015:
                        Log.Error("Worker hit the burst rate limit.");
                        break;
                    case 1019:
                        Log.Error("Worker hit loop limit.");
                        break;
                    case 1021:
                        Log.Error("Worker has requested a host it cannot access.");
                        break;
                    case 1022:
                        Log.Error("Cloudflare has failed to route the request to the Worker.");
                        break;
                    case 1024:
                        Log.Error("Worker cannot make a subrequest to a Cloudflare-owned IP address.");
                        break;
                    case 1027:
                        Log.Error("Worker exceeded free tier daily request limit.");
                        break;
                    case 1042:
                        Log.Error("Worker tried to fetch from another Worker on the same zone, which is unsupported.");
                        break;
                    default:
                        Log.Error("Unknown Cloudflare Worker error");
                        break;
                }
                MessageBox.Show(Localizer.GetString("chat.report.send.fail"));
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex.Message}");
                MessageBox.Show(Localizer.GetString("chat.report.send.fail"));
            }
        }
    }
}