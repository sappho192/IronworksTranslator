using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Services.FFXIV
{
    internal static partial class AutoTranslateDictionary
    {
        internal const string FallbackText = "[定型文]";

        internal static bool TryResolve(ulong key, ClientLanguage language, out string text)
        {
            var dictionary = GetDictionary(language);
            if (dictionary.TryGetValue(key, out var value))
            {
                text = value;
                return true;
            }

            text = string.Empty;
            return false;
        }

        internal static bool TryResolveWithFallback(
            ulong key,
            ClientLanguage language,
            ClientLanguage fallbackLanguage,
            out string text,
            out ClientLanguage resolvedLanguage)
        {
            if (TryResolve(key, language, out text))
            {
                resolvedLanguage = language;
                return true;
            }

            if (fallbackLanguage != language &&
                TryResolve(key, fallbackLanguage, out text))
            {
                resolvedLanguage = fallbackLanguage;
                return true;
            }

            resolvedLanguage = language;
            text = string.Empty;
            return false;
        }

        private static IReadOnlyDictionary<ulong, string> GetDictionary(ClientLanguage language)
        {
            return language switch
            {
                ClientLanguage.Japanese => JapaneseEntries,
                ClientLanguage.English => EnglishEntries,
                ClientLanguage.German => GermanEntries,
                ClientLanguage.French => FrenchEntries,
                ClientLanguage.Korean => KoreanEntries,
                _ => EnglishEntries,
            };
        }
    }
}
