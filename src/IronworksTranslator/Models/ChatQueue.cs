using Sharlayan.Core;
using System.Collections.Concurrent;

namespace IronworksTranslator.Models
{
    public class ChatQueue
    {
        // Chat messages with bounded capacity to prevent unbounded memory growth
        public static BlockingCollection<ChatLogItem> q = new (new ConcurrentQueue<ChatLogItem>(), boundedCapacity: 1000);

        // Dialogue messages with bounded capacity
        public static ConcurrentQueue<DialogueEntry> rq = new();
        private const int MaxDialogueQueueSize = 100;

        // Thread-safe access to the bounded dialogue queue
        private static readonly object _dialogueLock = new();

        public static void EnqueueDialogue(DialogueEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            lock (_dialogueLock)
            {
                while (rq.Count >= MaxDialogueQueueSize && rq.TryDequeue(out _))
                {
                }

                rq.Enqueue(entry);
            }
        }

        public static bool TryDequeueDialogue(out DialogueEntry? entry)
        {
            lock (_dialogueLock)
            {
                return rq.TryDequeue(out entry);
            }
        }
    }
}
