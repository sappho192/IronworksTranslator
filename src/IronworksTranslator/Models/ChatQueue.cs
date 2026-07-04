using Sharlayan.Core;
using System.Collections.Concurrent;

namespace IronworksTranslator.Models
{
    public class ChatQueue
    {
        // Chat messages with bounded capacity to prevent unbounded memory growth
        public static BlockingCollection<ChatLogItem> q = new (new ConcurrentQueue<ChatLogItem>(), boundedCapacity: 1000);

        // Dialogue messages with bounded capacity
        public static ConcurrentQueue<string> rq = new() { };
        private const int MaxDialogueQueueSize = 100;

        // Thread-safe access to dialogue queue and lastMsg
        private static readonly object _dialogueLock = new();
        private static string _lastMsg = "";

        public static string LastMsg
        {
            get { lock (_dialogueLock) return _lastMsg; }
            set { lock (_dialogueLock) _lastMsg = value; }
        }

        public static bool EnqueueDialogueIfNew(string message)
        {
            lock (_dialogueLock)
            {
                if (_lastMsg.Equals(message))
                {
                    return false;
                }

                EnqueueDialogueUnsafe(message);
                _lastMsg = message;
                return true;
            }
        }

        public static void EnqueueDialogue(string message)
        {
            lock (_dialogueLock)
            {
                EnqueueDialogueUnsafe(message);
                _lastMsg = message;
            }
        }

        private static void EnqueueDialogueUnsafe(string message)
        {
            while (rq.Count >= MaxDialogueQueueSize && rq.TryDequeue(out _))
            {
            }

            rq.Enqueue(message);
        }
    }
}
