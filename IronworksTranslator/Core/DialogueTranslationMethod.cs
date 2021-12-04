using System.ComponentModel;

namespace IronworksTranslator.Core
{
    public enum DialogueTranslationMethod
    {
        [Description("Chat Message")]
        ChatMessage = 0,
        [Description("Memory Search")]
        MemorySearch = 1
    }
}
