using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Services.FFXIV;
using IronworksTranslator.ViewModels.Windows;
using System.Text;

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

    [Theory]
    [InlineData(new byte[] { 0x02, 0xCA }, 0x2CAL)]
    [InlineData(new byte[] { 0x02, 0xF0, 0xCF }, 0x2F0CFL)]
    [InlineData(new byte[] { 0x04, 0xF2, 0x01, 0x95 }, 0x4F20195L)]
    public void TryGetAutoTranslatePayloadKey_UsesBigEndianPayloads(byte[] payload, long expected)
    {
        var result = ByteArrayExtension.TryGetAutoTranslatePayloadKey(payload, out var key);

        Assert.True(result);
        Assert.Equal((ulong)expected, key);
    }

    [Theory]
    [InlineData(0x2CAUL, ClientLanguage.English, "Nice to meet you.")]
    [InlineData(0x2CAUL, ClientLanguage.Japanese, "\u306F\u3058\u3081\u307E\u3057\u3066\u3002")]
    [InlineData(0x2CAUL, ClientLanguage.Korean, "\uCC98\uC74C \uBD59\uACA0\uC2B5\uB2C8\uB2E4.")]
    [InlineData(0x2F0CFUL, ClientLanguage.Korean, "\uC7AC\uBBF8\uC788\uC5C8\uC5B4\uC694.")]
    [InlineData(0x4F20195UL, ClientLanguage.Korean, "\uC54C\uACA0\uC2B5\uB2C8\uB2E4.")]
    public void AutoTranslateDictionary_ResolvesGeneratedCompletionData(
        ulong key,
        ClientLanguage language,
        string expected)
    {
        var result = AutoTranslateDictionary.TryResolve(key, language, out var text);

        Assert.True(result);
        Assert.Equal(expected, text);
    }

    [Fact]
    public void AutoTranslateDictionary_FallsBackToSpecifiedLanguageWhenEntryIsMissing()
    {
        const ulong key = 0x14F20629UL;

        var exactResult = AutoTranslateDictionary.TryResolve(key, ClientLanguage.Korean, out _);
        var fallbackResult = AutoTranslateDictionary.TryResolveWithFallback(
            key,
            ClientLanguage.Korean,
            ClientLanguage.Japanese,
            out var text,
            out var resolvedLanguage);

        Assert.False(exactResult);
        Assert.True(fallbackResult);
        Assert.Equal("\u30B3\u30DF\u30E5\u30CB\u30C6\u30A3\u30D5\u30A1\u30A4\u30F3\u30C0\u30FC", text);
        Assert.Equal(ClientLanguage.Japanese, resolvedLanguage);
    }

    [Fact]
    public void IsPureAutoTranslateMessage_ReturnsTrueForMarkerOnlyBody()
    {
        var rawMessage = BuildSayMessage(CreateAutoTranslateBlock(0x02, 0xCA));

        var markerDecode = rawMessage.DecodeAutoTranslateMarker(
            ClientLanguage.English,
            ClientLanguage.Korean);

        Assert.True(ChatWindowViewModel.IsPureAutoTranslateMessage(markerDecode));
    }

    [Fact]
    public void IsPureAutoTranslateMessage_ReturnsFalseForMixedBody()
    {
        var rawMessage = BuildSayMessage(
            Encoding.UTF8.GetBytes("Hello "),
            CreateAutoTranslateBlock(0x02, 0xCA));

        var markerDecode = rawMessage.DecodeAutoTranslateMarker(
            ClientLanguage.English,
            ClientLanguage.Korean);

        Assert.False(ChatWindowViewModel.IsPureAutoTranslateMessage(markerDecode));
    }

    [Fact]
    public void DecodeAutoTranslate_UsesChannelLanguageFallbackForKnownKeyMissingInTargetLanguage()
    {
        var rawMessage = BuildSayMessage(CreateAutoTranslateBlock(0x14, 0xF2, 0x06, 0x29));

        var result = rawMessage.DecodeAutoTranslate(
            ClientLanguage.Korean,
            ClientLanguage.Japanese,
            ClientLanguage.Korean,
            ClientLanguage.Japanese);

        var block = Assert.Single(result.Blocks);
        Assert.Equal(
            "\u30B3\u30DF\u30E5\u30CB\u30C6\u30A3\u30D5\u30A1\u30A4\u30F3\u30C0\u30FC",
            result.DecodedChat.Line.RemoveBefore(":"));
        Assert.True(block.SourceResolved);
        Assert.True(block.TargetResolved);
        Assert.Equal(
            "\u30B3\u30DF\u30E5\u30CB\u30C6\u30A3\u30D5\u30A1\u30A4\u30F3\u30C0\u30FC",
            block.TargetText);
    }

    [Fact]
    public void DecodeAutoTranslate_UsesFallbackForUnknownKeys()
    {
        var rawMessage = BuildSayMessage(CreateAutoTranslateBlock(0xFF, 0xFF, 0xFF));

        var result = rawMessage.DecodeAutoTranslate(
            ClientLanguage.Korean,
            ClientLanguage.English,
            ClientLanguage.Korean,
            ClientLanguage.English);

        var block = Assert.Single(result.Blocks);
        Assert.Equal(AutoTranslateDictionary.FallbackText, result.DecodedChat.Line.RemoveBefore(":"));
        Assert.False(block.SourceResolved);
        Assert.False(block.TargetResolved);
    }

    [Fact]
    public void CreateResolvedAutoTranslateText_StoresOfficialTargetTextWithoutTranslation()
    {
        var text = ChatWindowViewModel.CreateResolvedAutoTranslateText(
            "Nice to meet you.",
            "\uCC98\uC74C \uBD59\uACA0\uC2B5\uB2C8\uB2E4.",
            ClientLanguage.English,
            ClientLanguage.Korean,
            "Python Volca");

        Assert.Equal("Nice to meet you.", text.OriginalText);
        Assert.Equal("\uCC98\uC74C \uBD59\uACA0\uC2B5\uB2C8\uB2E4.", text.TranslatedText);
        Assert.Equal("Python Volca", text.Author);
        Assert.Equal(TranslationLanguageCode.English, text.SourceLanguage);
        Assert.Equal(TranslationLanguageCode.Korean, text.TargetLanguage);
    }

    private static byte[] BuildSayMessage(params byte[][] bodyParts)
    {
        List<byte> bytes = [0x00, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x00, 0x00];
        bytes.AddRange(Encoding.UTF8.GetBytes(":Python Volca:"));
        foreach (var bodyPart in bodyParts)
        {
            bytes.AddRange(bodyPart);
        }

        return [.. bytes];
    }

    private static byte[] CreateAutoTranslateBlock(params byte[] payload)
    {
        List<byte> bytes = [0x02, 0x2E, (byte)(payload.Length + 1)];
        bytes.AddRange(payload);
        bytes.Add(0x03);
        return [.. bytes];
    }
}
