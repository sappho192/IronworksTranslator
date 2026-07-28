namespace IronworksTranslator.Utils.UI
{
    public static class DialogueTextFormatter
    {
        public static string Format(string? speaker, string translatedText)
        {
            ArgumentNullException.ThrowIfNull(translatedText);

            return string.IsNullOrWhiteSpace(speaker)
                ? translatedText
                : $"{speaker}: {translatedText}";
        }
    }
}
