using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace IronworksTranslator.Util
{
    [JsonObject]
    public class HermesAddress
    {
        public string Name { get; set; }
        public List<long> Address { get; set; }
        private static readonly string url = "https://raw.githubusercontent.com/sappho192/ffxiv-hermes/main/latest/address.json";

        public static HermesAddress GetLatestAddress()
        {
            // Replaced WebClient, which is obsolete from .NET 5, to HttpClient

            using var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = httpClient.Send(request);
            using var reader = new StreamReader(response.Content.ReadAsStream());
            var latest = reader.ReadToEnd();

            var json = JsonConvert.DeserializeObject<HermesAddress>(latest);
            return json;
        }
    }
}
