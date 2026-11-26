using IronworksTranslator.Models.Enums;
using Serilog;

namespace IronworksTranslator.Utils.Translators
{
    public abstract class TranslatorBase
    {
        public abstract TranslationLanguageCode[] SupportedSourceLanguages { get; }
        public abstract TranslationLanguageCode[] SupportedTargetLanguages { get; }

        /// <summary>
        /// Raised after a translation request finishes so health monitors (e.g. engine fallback)
        /// can tell a real translation from the "return the original text" failure path.
        /// </summary>
        public event EventHandler<TranslatorOutcomeEventArgs>? OutcomeReported;

        // Synchronous version for backward compatibility
        public abstract string Translate(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage);

        // Asynchronous version for better performance
        public abstract Task<string> TranslateAsync(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage);

        protected void ReportTranslationSucceeded()
        {
            RaiseOutcome(new TranslatorOutcomeEventArgs(succeeded: true, error: null));
        }

        protected void ReportTranslationFailed(Exception? error = null)
        {
            RaiseOutcome(new TranslatorOutcomeEventArgs(succeeded: false, error));
        }

        private void RaiseOutcome(TranslatorOutcomeEventArgs args)
        {
            try
            {
                OutcomeReported?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                // A faulty listener must never break the translation path.
                Log.Warning(ex, "Translator outcome listener failed. Translator: {Translator}", GetType().Name);
            }
        }
    }

    public sealed class TranslatorOutcomeEventArgs : EventArgs
    {
        public TranslatorOutcomeEventArgs(bool succeeded, Exception? error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public bool Succeeded { get; }
        public Exception? Error { get; }
    }
}
