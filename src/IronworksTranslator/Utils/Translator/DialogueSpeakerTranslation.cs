namespace IronworksTranslator.Utils.Translator
{
    internal static class DialogueSpeakerTranslation
    {
        internal static string TranslateOrOriginal(
            string? speaker,
            Func<string, string> translate,
            Action<Exception>? onFailure = null)
        {
            ArgumentNullException.ThrowIfNull(translate);

            var original = speaker?.Trim() ?? string.Empty;
            if (original.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                var translated = translate(original);
                return string.IsNullOrWhiteSpace(translated)
                    ? original
                    : translated.Trim();
            }
            catch (Exception ex)
            {
                onFailure?.Invoke(ex);
                return original;
            }
        }

        internal static async Task<string> TranslateOrOriginalAsync(
            string? speaker,
            Func<string, CancellationToken, Task<string>> translate,
            CancellationToken cancellationToken,
            Action<Exception>? onFailure = null)
        {
            ArgumentNullException.ThrowIfNull(translate);

            var original = speaker?.Trim() ?? string.Empty;
            if (original.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                var translated = await translate(original, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return string.IsNullOrWhiteSpace(translated)
                    ? original
                    : translated.Trim();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                onFailure?.Invoke(ex);
                return original;
            }
        }
    }
}
