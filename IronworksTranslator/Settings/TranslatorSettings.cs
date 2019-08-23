using IronworksTranslator.Core;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace IronworksTranslator.Settings
{
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class TranslatorSettings
    {
        public TranslatorSettings()
        {
            DefaultTranslatorEngine = TranslatorEngine.Papago;
            ActiveTranslatorEngines = new HashSet<TranslatorEngine>
            {
                TranslatorEngine.Papago
            };
        }

        
        [JsonProperty]
        public TranslatorEngine DefaultTranslatorEngine { get; set; }
        [JsonProperty]
        public HashSet<TranslatorEngine> ActiveTranslatorEngines { get; }
    }
}
