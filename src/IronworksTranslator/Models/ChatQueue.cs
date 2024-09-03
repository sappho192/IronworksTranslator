using Sharlayan.Core;
using System.Collections.Concurrent;

namespace IronworksTranslator.Models
{
    public class ChatQueue
    {
        public static BlockingCollection<ChatLogItem> q = new (new ConcurrentQueue<ChatLogItem>());
        public static ConcurrentQueue<string> rq = new() { };
        public static string lastMsg = "";
    }
}
