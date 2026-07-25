namespace IronworksTranslator.Utils.Translator
{
    internal sealed class BoundedTranslationCache<TKey>
        where TKey : notnull
    {
        private readonly object syncRoot = new();
        private readonly int capacity;
        private readonly TimeSpan timeToLive;
        private readonly Func<DateTimeOffset> utcNow;
        private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> entries = [];
        private readonly LinkedList<CacheEntry> lru = [];

        public BoundedTranslationCache(
            int capacity,
            TimeSpan timeToLive,
            Func<DateTimeOffset>? utcNow = null)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (timeToLive <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeToLive));
            }

            this.capacity = capacity;
            this.timeToLive = timeToLive;
            this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public bool TryGet(TKey key, out string value)
        {
            lock (syncRoot)
            {
                if (!entries.TryGetValue(key, out var node))
                {
                    value = string.Empty;
                    return false;
                }

                if (utcNow() - node.Value.CreatedAtUtc > timeToLive)
                {
                    Remove(node);
                    value = string.Empty;
                    return false;
                }

                lru.Remove(node);
                lru.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
        }

        public void Set(TKey key, string value)
        {
            lock (syncRoot)
            {
                if (entries.TryGetValue(key, out var existing))
                {
                    existing.Value = new CacheEntry(key, value, utcNow());
                    lru.Remove(existing);
                    lru.AddFirst(existing);
                    return;
                }

                var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, value, utcNow()));
                lru.AddFirst(node);
                entries.Add(key, node);

                while (entries.Count > capacity && lru.Last is { } leastRecentlyUsed)
                {
                    Remove(leastRecentlyUsed);
                }
            }
        }

        private void Remove(LinkedListNode<CacheEntry> node)
        {
            lru.Remove(node);
            entries.Remove(node.Value.Key);
        }

        private sealed record CacheEntry(TKey Key, string Value, DateTimeOffset CreatedAtUtc);
    }
}
