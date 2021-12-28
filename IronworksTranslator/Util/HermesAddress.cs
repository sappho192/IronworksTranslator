using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net;

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
            using (WebClient client = new WebClient())
            {
                var latest = client.DownloadString(url);
                var json = JsonConvert.DeserializeObject<HermesAddress>(latest);
                return json;
            }
        }
    }
}
