using IronworksTranslator.Models.Enums;
using IronworksTranslator.ViewModels.Windows;
using Sharlayan.Core;

namespace IronworksTranslator.Tests.ViewModels;

public class ChatWindowViewModelHelperTests
{
    [Theory]
    [InlineData(ChatCode.System, true)]
    [InlineData(ChatCode.Recruitment, true)]
    [InlineData(ChatCode.Say, false)]
    public void IsSystemMessage_ClassifiesSystemCodes(ChatCode code, bool expected)
    {
        Assert.Equal(expected, ChatWindowViewModel.IsSystemMessage(code));
    }

    [Theory]
    [InlineData(ChatCode.NPCDialog, true)]
    [InlineData(ChatCode.NPCAnnounce, true)]
    [InlineData(ChatCode.BossQuotes, true)]
    [InlineData(ChatCode.Say, false)]
    public void IsDialogueMessage_ClassifiesDialogueCodes(ChatCode code, bool expected)
    {
        Assert.Equal(expected, ChatWindowViewModel.IsDialogueMessage(code));
    }

    [Theory]
    [InlineData(ChatCode.Emote, true)]
    [InlineData(ChatCode.EmoteCustom, true)]
    [InlineData(ChatCode.Party, false)]
    public void IsEmoteMessage_ClassifiesEmoteCodes(ChatCode code, bool expected)
    {
        Assert.Equal(expected, ChatWindowViewModel.IsEmoteMessage(code));
    }

    [Theory]
    [InlineData("Author: body", "Author")]
    [InlineData("No delimiter", "")]
    [InlineData("", "")]
    public void GetAuthorFromLine_ReturnsTextBeforeColon(string line, string expected)
    {
        Assert.Equal(expected, ChatWindowViewModel.GetAuthorFromLine(line));
    }

    [Theory]
    [InlineData("Author: body", "body")]
    [InlineData("No delimiter", "No delimiter")]
    [InlineData("", "")]
    public void GetBodyFromLine_ReturnsTextAfterColon(string line, string expected)
    {
        Assert.Equal(expected, ChatWindowViewModel.GetBodyFromLine(line));
    }

    [Theory]
    [InlineData("Author: waves hello", "waves hello")]
    [InlineData("Author waves hello", "waves hello")]
    [InlineData("Author：waves hello", "waves hello")]
    [InlineData("Someone else waves", "Someone else waves")]
    public void StripAuthorPrefix_RemovesKnownAuthorPrefix(string value, string expected)
    {
        Assert.Equal(expected, ChatWindowViewModel.StripAuthorPrefix(value, "Author"));
    }

    [Fact]
    public void GetEmoteBody_PrefersDecodedMessageAndRemovesAuthor()
    {
        var raw = new ChatLogItem
        {
            Line = "Raw Player: raw body",
            Message = "Raw Player raw body",
            PlayerName = "Raw Player",
            PlayerCharacterName = "Raw Player"
        };
        var decoded = new ChatLogItem
        {
            Line = "Decoded Player: decoded body",
            Message = "Decoded Player decoded body",
            PlayerName = "Decoded Player",
            PlayerCharacterName = "Decoded Player"
        };

        var result = ChatWindowViewModel.GetEmoteBody(raw, decoded);

        Assert.Equal("decoded body", result);
    }

    [Fact]
    public void ResolveEmoteSourceLanguage_UsesEnglishForPureEnglishEmotes()
    {
        var result = ChatWindowViewModel.ResolveEmoteSourceLanguage(
            "waves hello",
            ClientLanguage.Japanese);

        Assert.Equal(ClientLanguage.English, result);
    }

    [Fact]
    public void ResolveEmoteSourceLanguage_KeepsConfiguredLanguageWhenSentenceContainsJapanese()
    {
        var result = ChatWindowViewModel.ResolveEmoteSourceLanguage(
            "こんにちは hello",
            ClientLanguage.Japanese);

        Assert.Equal(ClientLanguage.Japanese, result);
    }
}
