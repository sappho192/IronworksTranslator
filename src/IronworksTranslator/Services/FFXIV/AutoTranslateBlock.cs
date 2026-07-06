namespace IronworksTranslator.Services.FFXIV
{
    internal sealed record AutoTranslateBlock(
        ulong PayloadKey,
        string MarkerText,
        string SourceText,
        string TargetText,
        bool SourceResolved,
        bool TargetResolved);
}
