using IronworksTranslator.Utils.UI;

namespace IronworksTranslator.Tests.Utils;

public class DialogueTextFormatterTests
{
    [Fact]
    public void Format_WithSpeaker_UsesSpeakerColonText()
    {
        Assert.Equal(
            "Alphinaud: Welcome back.",
            DialogueTextFormatter.Format("Alphinaud", "Welcome back."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_WithoutSpeaker_ReturnsTranslatedText(string? speaker)
    {
        Assert.Equal("Welcome back.", DialogueTextFormatter.Format(speaker, "Welcome back."));
    }

    [Fact]
    public void Format_PreservesUnicodeSpeakerAndTranslatedText()
    {
        Assert.Equal(
            "アルフィノ: 어서 와! 👋",
            DialogueTextFormatter.Format("アルフィノ", "어서 와! 👋"));
    }
}
