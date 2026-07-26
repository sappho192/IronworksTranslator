using System.Collections.Concurrent;
using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using Sharlayan.Core;

namespace IronworksTranslator.Tests.Models;

public class ChatQueueTests
{
    public ChatQueueTests()
    {
        ChatQueue.q = new BlockingCollection<ChatLogItem>(new ConcurrentQueue<ChatLogItem>(), boundedCapacity: 1000);
        ChatQueue.rq = new ConcurrentQueue<DialogueEntry>();
    }

    [Fact]
    public void EnqueueDialogue_PreservesSpeakerAndText()
    {
        ChatQueue.EnqueueDialogue(new DialogueEntry(DialogueKind.StandardTalk, "Alphinaud", "First message"));

        Assert.True(ChatQueue.TryDequeueDialogue(out var queued));
        Assert.NotNull(queued);
        Assert.Equal("Alphinaud", queued.Speaker);
        Assert.Equal("First message", queued.Text);
        Assert.Equal(DialogueKind.StandardTalk, queued.Kind);
    }

    [Fact]
    public void EnqueueDialogue_NormalizesNullSpeaker()
    {
        ChatQueue.EnqueueDialogue(new DialogueEntry(DialogueKind.BattleTalk, null, "Narration"));

        Assert.True(ChatQueue.TryDequeueDialogue(out var queued));
        Assert.NotNull(queued);
        Assert.Equal(string.Empty, queued.Speaker);
        Assert.Equal("Narration", queued.Text);
    }

    [Fact]
    public void EnqueueDialogue_PreservesSameTextFromDifferentSpeakers()
    {
        ChatQueue.EnqueueDialogue(new DialogueEntry(DialogueKind.StandardTalk, "Alphinaud", "Same"));
        ChatQueue.EnqueueDialogue(new DialogueEntry(DialogueKind.BattleTalk, "Tataru", "Same"));

        Assert.True(ChatQueue.TryDequeueDialogue(out var first));
        Assert.True(ChatQueue.TryDequeueDialogue(out var second));
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Alphinaud", first.Speaker);
        Assert.Equal("Tataru", second.Speaker);
        Assert.Equal(DialogueKind.StandardTalk, first.Kind);
        Assert.Equal(DialogueKind.BattleTalk, second.Kind);
    }

    [Fact]
    public void EnqueueDialogue_RejectsNullEntry()
    {
        Assert.Throws<ArgumentNullException>(() => ChatQueue.EnqueueDialogue(null!));
    }

    [Fact]
    public void EnqueueDialogue_TrimsOldEntriesWhenQueueIsFull()
    {
        for (var i = 0; i < 105; i++)
        {
            ChatQueue.EnqueueDialogue(
                new DialogueEntry(DialogueKind.StandardTalk, $"Speaker {i}", $"Message {i}"));
        }

        Assert.Equal(100, ChatQueue.rq.Count);
        Assert.True(ChatQueue.rq.TryPeek(out var firstRemaining));
        Assert.Equal("Speaker 5", firstRemaining.Speaker);
        Assert.Equal("Message 5", firstRemaining.Text);
    }
}
