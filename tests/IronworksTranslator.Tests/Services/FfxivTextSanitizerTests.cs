using IronworksTranslator.Services.FFXIV;

namespace IronworksTranslator.Tests.Services;

public class FfxivTextSanitizerTests
{
    [Fact]
    public void SanitizeDialogueText_ConvertsSeStringHyphenPayloadToAsciiHyphen()
    {
        // Observed in live EN Talk memory: Radz-at-Han encoded with SeString SoftHyphen (0x02 0x1F 0x01 0x03).
        var raw = "Claiming Thavnair as its dominion, the nation of Radz\u0002\u001F\u0001\u0003at\u0002\u001F\u0001\u0003Han hath long thrived.";

        var cleaned = FfxivTextSanitizer.SanitizeDialogueText(raw);

        Assert.Equal("Claiming Thavnair as its dominion, the nation of Radz-at-Han hath long thrived.", cleaned);
    }

    [Fact]
    public void SanitizeDialogueText_StripsInlineEmphasisPayloadsWithoutCorruptingText()
    {
        // Observed in live EN Talk memory: italic ship name wrapped in 0x02 0x1A ... 0x03.
        var raw = "I'm the hired captain of the \u0002\u001A\u0002\u0002\u0003Orion\u0002\u001A\u0002\u0001\u0003. She's a fine vessel.";

        var cleaned = FfxivTextSanitizer.SanitizeDialogueText(raw);

        Assert.Equal("I'm the hired captain of the Orion. She's a fine vessel.", cleaned);
    }

    [Fact]
    public void SanitizeDialogueText_NormalizesNewLinePayloadsGameIconsAndWhitespace()
    {
        var raw = "  Hello\u0002\u0010\u0001\u0003world! \uE03C \uE06F \uE0BB\uFFFD  ";

        var cleaned = FfxivTextSanitizer.SanitizeDialogueText(raw);

        Assert.Equal("Hello world! [HQ] ⇒", cleaned);
    }

    [Fact]
    public void SanitizeDialogueText_DropsUnclosedControlByteWithoutSwallowingText()
    {
        var raw = "\u0002unterminated dialogue text";

        var cleaned = FfxivTextSanitizer.SanitizeDialogueText(raw);

        Assert.Equal("unterminated dialogue text", cleaned);
    }

    [Fact]
    public void SanitizeDialogueText_IsIdempotent()
    {
        var raw = "Strong ties with Radz\u0002\u001F\u0001\u0003at\u0002\u001F\u0001\u0003Han and \u0002\u001A\u0002\u0002\u0003Orion\u0002\u001A\u0002\u0001\u0003.";
        var once = FfxivTextSanitizer.SanitizeDialogueText(raw);
        var twice = FfxivTextSanitizer.SanitizeDialogueText(once);

        Assert.Equal(once, twice);
    }
}
