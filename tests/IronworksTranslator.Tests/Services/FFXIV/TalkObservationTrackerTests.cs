using IronworksTranslator.Services.FFXIV;
using Sharlayan.Models.ReadResults;

namespace IronworksTranslator.Tests.Services.FFXIV;

public class TalkObservationTrackerTests
{
    private readonly TalkObservationTracker _tracker = new();

    [Fact]
    public void FirstLastSnapshot_IsBaselineOnly()
    {
        Assert.False(_tracker.ShouldEnqueue(Last("Alphinaud", "Welcome back.")));
    }

    [Fact]
    public void FirstCurrentVisibleSnapshot_IsEnqueued()
    {
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
    }

    [Fact]
    public void ChangedText_IsEnqueued()
    {
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "First")));
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Second")));
    }

    [Fact]
    public void SameSpeakerAndText_IsDuplicate()
    {
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
        Assert.False(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
    }

    [Fact]
    public void SameTextWithDifferentSpeaker_IsEnqueued()
    {
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
        Assert.True(_tracker.ShouldEnqueue(Current("Tataru", "Welcome back.")));
    }

    [Fact]
    public void ClosedAndReopenedSamePair_IsEnqueuedAgain()
    {
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
        Assert.False(_tracker.ShouldEnqueue(Last("Alphinaud", "Welcome back.")));
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
    }

    [Fact]
    public void EmptyText_UpdatesBaselineWithoutEnqueue()
    {
        Assert.False(_tracker.ShouldEnqueue(Current("Alphinaud", string.Empty)));
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
    }

    [Fact]
    public void UnavailableSnapshot_DoesNotChangeBaseline()
    {
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
        Assert.False(_tracker.ShouldEnqueue(Unavailable()));
        Assert.False(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
    }

    [Fact]
    public void Reset_ReappliesInitialSnapshotPolicy()
    {
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));

        _tracker.Reset();

        Assert.False(_tracker.ShouldEnqueue(Last("Alphinaud", "Welcome back.")));
        Assert.True(_tracker.ShouldEnqueue(Current("Alphinaud", "Welcome back.")));
    }

    [Fact]
    public void UnicodeSpeakerAndText_UseOrdinalPairComparison()
    {
        Assert.True(_tracker.ShouldEnqueue(Current("알피노", "어서 와! 👋")));
        Assert.False(_tracker.ShouldEnqueue(Current("알피노", "어서 와! 👋")));
        Assert.True(_tracker.ShouldEnqueue(Current("アルフィノ", "어서 와! 👋")));
    }

    private static TalkResult Current(string speaker, string text)
    {
        return new TalkResult(true, speaker, text, TalkSource.Current, true);
    }

    private static TalkResult Last(string speaker, string text)
    {
        return new TalkResult(true, speaker, text, TalkSource.Last, false);
    }

    private static TalkResult Unavailable()
    {
        return new TalkResult(false, string.Empty, string.Empty, TalkSource.None, false);
    }
}
