namespace IronworksTranslator.Utils.Translator
{
    /// <summary>
    /// Decides when Papago's rendered result is final.
    /// Papago first renders a rough interim translation with "..." appended (e.g. "레스토랑에 들어갔다!...")
    /// and replaces it with the final one roughly 0.5-0.7s later, so the first non-empty text is not the answer.
    /// </summary>
    internal sealed class PapagoTranslationSettler
    {
        internal const string InterimMarker = "...";
        internal static readonly TimeSpan DefaultStableWindow = TimeSpan.FromMilliseconds(300);
        internal static readonly TimeSpan DefaultInterimStableWindow = TimeSpan.FromMilliseconds(1500);

        private readonly string sourceText;
        private readonly TimeSpan stableWindow;
        private readonly TimeSpan interimStableWindow;
        private string? candidate;
        private TimeSpan candidateSince;

        public PapagoTranslationSettler(
            string sourceText,
            TimeSpan? stableWindow = null,
            TimeSpan? interimStableWindow = null)
        {
            this.sourceText = sourceText;
            this.stableWindow = stableWindow ?? DefaultStableWindow;
            this.interimStableWindow = interimStableWindow ?? DefaultInterimStableWindow;
        }

        /// <summary>
        /// Feeds the currently rendered text. Returns the final translation once it has stayed unchanged
        /// long enough, otherwise <see langword="null"/>.
        /// </summary>
        public string? Observe(string? renderedText, TimeSpan elapsed)
        {
            var text = renderedText?.Trim() ?? string.Empty;
            if (text.Length == 0 || PapagoTranslator.IsSameText(text, sourceText))
            {
                candidate = null;
                return null;
            }

            if (!string.Equals(candidate, text, StringComparison.Ordinal))
            {
                candidate = text;
                candidateSince = elapsed;
                return null;
            }

            // Text ending with "..." may still be the interim result. When the source itself ends with "...",
            // the final result can too, so only a long period without change proves it is final.
            var window = text.EndsWith(InterimMarker, StringComparison.Ordinal)
                ? interimStableWindow
                : stableWindow;
            return elapsed - candidateSince >= window ? text : null;
        }

        /// <summary>
        /// The best available text when Papago never produced a settled result before the timeout.
        /// </summary>
        public string? GetBestEffortResult()
        {
            if (candidate is null)
            {
                return null;
            }

            if (!candidate.EndsWith(InterimMarker, StringComparison.Ordinal)
                || sourceText.TrimEnd().EndsWith(InterimMarker, StringComparison.Ordinal))
            {
                return candidate;
            }

            var stripped = candidate.TrimEnd('.').TrimEnd();
            return stripped.Length == 0 ? null : stripped;
        }
    }
}
