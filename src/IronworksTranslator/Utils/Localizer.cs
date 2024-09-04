using Lepo.i18n;
using System.Globalization;

namespace IronworksTranslator.Utils
{
#pragma warning disable CS8602, CS8603, CS8604
    public class Localizer
    {
        public static string GetString(string key)
        {
            var localizationProvider = LocalizationProviderFactory.GetInstance();
            var currentCulture = localizationProvider.GetCulture();
            var localizationSet = localizationProvider.GetLocalizationSet(currentCulture.ToString());
            return localizationSet[key];
        }

        public static void ChangeLanguage(string languageCode)
        {
            var localizationProvider = LocalizationProviderFactory.GetInstance();
            localizationProvider.SetCulture(new(languageCode));
        }

        public static CultureInfo GetCulture()
        {
            var localizationProvider = LocalizationProviderFactory.GetInstance();
            return localizationProvider.GetCulture();
        }

        public static string GetSpecificString(string key, string languageCode)
        {
            var localizationProvider = LocalizationProviderFactory.GetInstance();
            var localizationSet = localizationProvider.GetLocalizationSet(languageCode);
            return localizationSet[key];
        }
    }
#pragma warning restore CS8602, CS8603, CS8604
}
