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

        // Thread-safe access to lastMsg
        private static readonly object _lastMsgLock = new();
        private static string _lastMsg = "";

        public static string LastMsg
        {
            get { lock (_lastMsgLock) return _lastMsg; }
            set { lock (_lastMsgLock) _lastMsg = value; }
        }
    }
}
