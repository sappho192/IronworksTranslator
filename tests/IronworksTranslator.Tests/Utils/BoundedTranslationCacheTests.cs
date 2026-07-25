using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Tests.Utils;

public class BoundedTranslationCacheTests
{
    [Fact]
    public void TryGet_ExpiresEntriesAfterConfiguredLifetime()
    {
        var now = DateTimeOffset.Parse("2026-07-25T00:00:00+00:00");
        var cache = new BoundedTranslationCache<string>(
            capacity: 2,
            timeToLive: TimeSpan.FromMinutes(5),
            utcNow: () => now);
        cache.Set("key", "value");

        now = now.AddMinutes(6);

        Assert.False(cache.TryGet("key", out _));
    }

    [Fact]
    public void Set_EvictsLeastRecentlyUsedEntryAtCapacity()
    {
        var cache = new BoundedTranslationCache<int>(
            capacity: 2,
            timeToLive: TimeSpan.FromMinutes(5));
        cache.Set(1, "first");
        cache.Set(2, "second");

        Assert.True(cache.TryGet(1, out var cached));
        Assert.Equal("first", cached);

        cache.Set(3, "third");

        Assert.True(cache.TryGet(1, out _));
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(3, out _));
    }

    [Fact]
    public void GameSafeProfile_UsesLowBurstChatSettings()
    {
        var profile = MiLMMTInferenceProfile.GameSafe;

        Assert.Equal(2048, profile.ContextSize);
        Assert.Equal(512, profile.BatchSize);
        Assert.Equal(128, profile.UBatchSize);
        Assert.Equal(64, profile.GetMaxTokens(MiLMMTTranslationKind.Chat));
        Assert.Equal(256, profile.GetMaxTokens(MiLMMTTranslationKind.Dialogue));
        Assert.Equal(512, profile.GetMaxTokens(MiLMMTTranslationKind.Manual));
    }

    [Fact]
    public void NormalizeForCache_CollapsesWhitespace()
    {
        Assert.Equal(
            "hello world",
            MiLMMTTranslator.NormalizeForCache("  hello\t world\r\n"));
    }
}
