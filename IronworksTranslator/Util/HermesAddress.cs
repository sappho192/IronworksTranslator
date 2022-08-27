using IronworksTranslator.Core;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Windows;

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
            if(IronworksSettings.Instance.Translator.UseInternalAddress)
            {
                Directory.CreateDirectory("settings");
                string addressFilePath = "./settings/address.json";
                if (File.Exists(addressFilePath))
                {// Read settings
                    using StreamReader reader = File.OpenText(addressFilePath);
                    var latest = reader.ReadToEnd();
                    var json = JsonConvert.DeserializeObject<HermesAddress>(latest);
                    Log.Debug("Internal address.json loaded");
                    return json;
                } 
                else
                {
                    MessageBox.Show($"settings/address.json이 없습니다. 자체주소 설정을 무시하겠습니다.");
                    Log.Debug("UseInternalAddress is true but address.json does not exist. Loading address.json from web.");
                    return DownloadAddress(out _, out _);
                }
            } else
            {
                return DownloadAddress(out _, out _);
            }
        }

        private static HermesAddress DownloadAddress(out HttpClient httpClient, out StreamReader reader)
        {
            // Replaced WebClient, which is obsolete from .NET 5, to HttpClient
            httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = httpClient.Send(request);
            reader = new StreamReader(response.Content.ReadAsStream());
            var latest = reader.ReadToEnd();

            var json = JsonConvert.DeserializeObject<HermesAddress>(latest);
            Log.Debug("address.json loaded from web");
            return json;
        }
    }
}
