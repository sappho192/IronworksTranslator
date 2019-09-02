using Sharlayan.Core;
using System.Collections.Concurrent;

namespace IronworksTranslator.Core
{
    public static class ChatQueue
    {
        //public static ConcurrentQueue<ChatLogItem> q = new ConcurrentQueue<ChatLogItem>();
        public static BlockingCollection<ChatLogItem> q = new BlockingCollection<ChatLogItem>(new ConcurrentQueue<ChatLogItem>());
        public static ConcurrentQueue<string> rq = new ConcurrentQueue<string>() { };
        public static string lastMsg = "";
    }
}
