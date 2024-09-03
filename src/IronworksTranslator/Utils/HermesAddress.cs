using IronworksTranslator.Models.Settings;
using Newtonsoft.Json;
using Serilog;
using System.IO;
using System.Net.Http;

namespace IronworksTranslator.Utils
{
    [JsonObject]
    public class HermesAddress
    {
#pragma warning disable CS8602, CS8603
        public string? Name { get; set; }
        public List<long>? Address { get; set; }
        private static readonly string url = "https://hermes.sapphosound.com/latest/address.json";

        public static HermesAddress GetLatestAddress()
        {
            if (IronworksSettings.Instance.TranslatorSettings.UseInternalAddress)
            {
                Directory.CreateDirectory("settings");
                string addressFilePath = "address.json";
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
                    MessageBox.Show(Localizer.GetString("app.exception.address.not_exist"));
                    Log.Debug("UseInternalAddress is true but address.json does not exist. Loading address.json from the web.");
                    return DownloadAddress(out _, out _);
                }
            }
            else
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
#pragma warning restore CS8602, CS8603
}
