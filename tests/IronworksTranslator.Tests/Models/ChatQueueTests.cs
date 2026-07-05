using System.Collections.Concurrent;
using IronworksTranslator.Models;
using Sharlayan.Core;

namespace IronworksTranslator.Tests.Models;

public class ChatQueueTests
{
    public ChatQueueTests()
    {
        ChatQueue.q = new BlockingCollection<ChatLogItem>(new ConcurrentQueue<ChatLogItem>(), boundedCapacity: 1000);
        ChatQueue.rq = new ConcurrentQueue<string>();
        ChatQueue.LastMsg = string.Empty;
    }

    [Fact]
    public void EnqueueDialogueIfNew_AddsFirstMessageAndUpdatesLastMessage()
    {
        var added = ChatQueue.EnqueueDialogueIfNew("Dialogue window");

        Assert.True(added);
        Assert.Equal("Dialogue window", ChatQueue.LastMsg);
        Assert.True(ChatQueue.rq.TryDequeue(out var queued));
        Assert.Equal("Dialogue window", queued);
    }

    [Fact]
    public void EnqueueDialogueIfNew_SkipsDuplicateLastMessage()
    {
        ChatQueue.EnqueueDialogue("Same");

        var added = ChatQueue.EnqueueDialogueIfNew("Same");

        Assert.False(added);
        Assert.Single(ChatQueue.rq);
    }

    [Fact]
    public void EnqueueDialogue_TrimsOldEntriesWhenQueueIsFull()
    {
        for (var i = 0; i < 105; i++)
        {
            ChatQueue.EnqueueDialogue($"Message {i}");
        }

        Assert.Equal(100, ChatQueue.rq.Count);
        Assert.True(ChatQueue.rq.TryPeek(out var firstRemaining));
        Assert.Equal("Message 5", firstRemaining);
        Assert.Equal("Message 104", ChatQueue.LastMsg);
    }
}
