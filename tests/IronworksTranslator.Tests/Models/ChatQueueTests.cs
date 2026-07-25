using System.Collections.Concurrent;
using IronworksTranslator.Models;
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
        ChatQueue.EnqueueDialogue(new DialogueEntry("Alphinaud", "First message"));

        Assert.True(ChatQueue.TryDequeueDialogue(out var queued));
        Assert.NotNull(queued);
        Assert.Equal("Alphinaud", queued.Speaker);
        Assert.Equal("First message", queued.Text);
    }

    [Fact]
    public void EnqueueDialogue_PreservesSameTextFromDifferentSpeakers()
    {
        ChatQueue.EnqueueDialogue(new DialogueEntry("Alphinaud", "Same"));
        ChatQueue.EnqueueDialogue(new DialogueEntry("Tataru", "Same"));

        Assert.True(ChatQueue.TryDequeueDialogue(out var first));
        Assert.True(ChatQueue.TryDequeueDialogue(out var second));
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("Alphinaud", first.Speaker);
        Assert.Equal("Tataru", second.Speaker);
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
            ChatQueue.EnqueueDialogue(new DialogueEntry($"Speaker {i}", $"Message {i}"));
        }

        Assert.Equal(100, ChatQueue.rq.Count);
        Assert.True(ChatQueue.rq.TryPeek(out var firstRemaining));
        Assert.Equal("Speaker 5", firstRemaining.Speaker);
        Assert.Equal("Message 5", firstRemaining.Text);
    }
}
