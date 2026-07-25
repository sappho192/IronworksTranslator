namespace IronworksTranslator.Models
{
    public sealed class DialogueEntry
    {
        public DialogueEntry(string? speaker, string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            Speaker = speaker ?? string.Empty;
            Text = text;
        }

        public string Speaker { get; }

        public string Text { get; }
    }
}
