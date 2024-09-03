using System.Windows.Documents;

namespace IronworksTranslator.Models
{
    public class TranslationParagraph(Paragraph paragraph, TranslationText text)
    {
        public Paragraph Paragraph { get; } = paragraph;
        public TranslationText Text { get; } = text;
    }
}
