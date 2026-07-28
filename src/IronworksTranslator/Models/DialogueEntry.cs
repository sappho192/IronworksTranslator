using IronworksTranslator.Models.Enums;

namespace IronworksTranslator.Models
{
    public sealed class DialogueEntry
    {
        public DialogueEntry(DialogueKind kind, string? speaker, string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            Kind = kind;
            Speaker = speaker ?? string.Empty;
            Text = text;
        }

        public DialogueKind Kind { get; }

        public string Speaker { get; }

        public string Text { get; }
    }
}
