using IronworksTranslator.Helpers.Extensions;

namespace IronworksTranslator.Tests.Helpers;

public class ByteArrayExtensionTests
{
    [Fact]
    public void ExtractAutoTranslate_ReturnsPayloadsFromValidBlocks()
    {
        byte[] rawMessage = [0x41, 0x02, 0x2E, 0x03, 0x02, 0xCA, 0x03, 0x42];

        var result = rawMessage.ExtractAutoTranslate();

        var block = Assert.Single(result);
        Assert.Equal([0x02, 0xCA], block);
    }

    [Theory]
    [InlineData(new byte[] { 0x02 })]
    [InlineData(new byte[] { 0x02, 0x2E })]
    [InlineData(new byte[] { 0x02, 0x2E, 0x00, 0x03 })]
    [InlineData(new byte[] { 0x02, 0x2E, 0x04, 0x02, 0xCA, 0x03 })]
    [InlineData(new byte[] { 0x02, 0x2E, 0x03, 0x02, 0xCA, 0x00 })]
    public void ExtractAutoTranslate_IgnoresMalformedBlocks(byte[] rawMessage)
    {
        var exception = Record.Exception(() => rawMessage.ExtractAutoTranslate());

        Assert.Null(exception);
        Assert.Empty(rawMessage.ExtractAutoTranslate());
    }
}
