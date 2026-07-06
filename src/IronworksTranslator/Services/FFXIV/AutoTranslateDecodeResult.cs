using Sharlayan.Core;

namespace IronworksTranslator.Services.FFXIV
{
    internal sealed class AutoTranslateDecodeResult(
        ChatLogItem decodedChat,
        IReadOnlyList<AutoTranslateBlock> blocks)
    {
        public ChatLogItem DecodedChat { get; } = decodedChat;
        public IReadOnlyList<AutoTranslateBlock> Blocks { get; } = blocks;
        public bool HasAutoTranslate => Blocks.Count > 0;
    }
}
