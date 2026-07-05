using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Tests.Utils;

public class PapagoTranslatorTests
{
    [Fact]
    public void ExtractPapagoTargetText_UsesLexicalTextNodes()
    {
        const string html = """
            <div data-testid="target-editor">
              <span data-lexical-text="true">안녕</span>
              <span data-lexical-text="true">하세요</span>
            </div>
            """;

        var result = PapagoTranslator.ExtractPapagoTargetText(html);

        Assert.Equal("안녕하세요", result);
    }

    [Fact]
    public void ExtractPapagoTargetText_FallsBackToReadonlyTextbox()
    {
        const string html = """
            <div role="textbox" aria-readonly="true" contenteditable="false" data-lexical-editor="true">
              Hello &amp; goodbye
            </div>
            """;

        var result = PapagoTranslator.ExtractPapagoTargetText(html);

        Assert.Equal("Hello & goodbye", result);
    }

    [Fact]
    public void ExtractPapagoTargetText_ReturnsEmptyStringWhenTargetEditorDoesNotExist()
    {
        var result = PapagoTranslator.ExtractPapagoTargetText("<html><body>No target</body></html>");

        Assert.Equal(string.Empty, result);
    }
}
