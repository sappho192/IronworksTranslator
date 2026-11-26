using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Tests.Utils;

public class DeepLAPITranslatorTests
{
    [Fact]
    public void MaskApiKey_KeepsOnlyEdgesOfKey()
    {
        var masked = DeepLAPITranslator.MaskApiKey("abcd1234-5678-90ef-ghij-klmnopqrstuv:fx");

        Assert.Equal("abcd****v:fx", masked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("12345678")]
    public void MaskApiKey_HidesShortOrMissingKeysEntirely(string? apiKey)
    {
        Assert.Equal("****", DeepLAPITranslator.MaskApiKey(apiKey));
    }
}
