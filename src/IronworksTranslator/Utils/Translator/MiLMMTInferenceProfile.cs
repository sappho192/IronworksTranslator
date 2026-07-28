namespace IronworksTranslator.Utils.Translator
{
    internal sealed record MiLMMTInferenceProfile(
        string Name,
        int ContextSize,
        int GpuLayerCount,
        int BatchSize,
        int UBatchSize,
        int ChatMaxTokens,
        int DialogueMaxTokens,
        int ManualMaxTokens)
    {
        public static MiLMMTInferenceProfile GameSafe { get; } = new(
            Name: "GameSafe",
            ContextSize: 2048,
            GpuLayerCount: 99,
            BatchSize: 512,
            UBatchSize: 128,
            ChatMaxTokens: 64,
            DialogueMaxTokens: 256,
            ManualMaxTokens: 512);

        public int GetMaxTokens(MiLMMTTranslationKind requestKind)
        {
            return requestKind switch
            {
                MiLMMTTranslationKind.Chat => ChatMaxTokens,
                MiLMMTTranslationKind.Dialogue => DialogueMaxTokens,
                MiLMMTTranslationKind.Manual => ManualMaxTokens,
                _ => throw new ArgumentOutOfRangeException(nameof(requestKind), requestKind, null),
            };
        }
    }

    public enum MiLMMTTranslationKind
    {
        Chat,
        Dialogue,
        Manual,
    }
}
