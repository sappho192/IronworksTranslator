using System.ComponentModel;

namespace IronworksTranslator.Core
{
    public enum DialogueTranslationMethod
    {
        [Description("Memory Search")]
        MemorySearch = 0,
        [Description("Chat Message")]
        ChatMessage = 1
    }
}
