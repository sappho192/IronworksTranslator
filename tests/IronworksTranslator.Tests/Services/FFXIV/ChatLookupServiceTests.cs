using IronworksTranslator.Services.FFXIV;

namespace IronworksTranslator.Tests.Services.FFXIV;

public class ChatLookupServiceTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void HasAnyDialogueCapability_StartsForEachIndependentSource(
        bool standardTalk,
        bool battleTalk)
    {
        Assert.True(
            ChatLookupService.HasAnyDialogueCapability(
                standardTalk,
                battleTalk));
    }

    [Fact]
    public void HasAnyDialogueCapability_StopsWhenNoSourceIsReady()
    {
        Assert.False(ChatLookupService.HasAnyDialogueCapability(false, false));
    }
}
