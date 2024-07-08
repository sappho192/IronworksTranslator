using Lepo.i18n;

namespace IronworksTranslator.Utils
{
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

        public static string GetSpecificString(string key, string languageCode)
        {
            var localizationProvider = LocalizationProviderFactory.GetInstance();
            var localizationSet = localizationProvider.GetLocalizationSet(languageCode);
            return localizationSet[key];
        }
    }
}
