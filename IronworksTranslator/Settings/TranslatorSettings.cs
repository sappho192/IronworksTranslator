using IronworksTranslator.Core;
using System.Collections.Generic;

namespace IronworksTranslator.Settings
{
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

        
        TranslatorEngine DefaultTranslatorEngine { get; set; }
        HashSet<TranslatorEngine> ActiveTranslatorEngines { get; }
    }
}
