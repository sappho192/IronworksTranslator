using IronworksTranslator.Models.Cloudflare;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Translator;
using Serilog;

namespace IronworksTranslator.ViewModels.Dialogs
{
    public partial class TranslationReportViewModel : ObservableObject
    {
        public const string DraftSourceCurrent = "current";
        public const string DraftSourcePapago = "papago";
        public const string DraftSourceDeepL = "deepl";

        private readonly Func<TranslatorEngine, string, TranslationLanguageCode, TranslationLanguageCode, CancellationToken, Task<string>> _translateDraftAsync;
        private readonly Func<string, string> _localize;
        private string _baselineDraft;

        public TranslationReportViewModel(
            TranslationText translationText,
            Func<TranslatorEngine, string, TranslationLanguageCode, TranslationLanguageCode, CancellationToken, Task<string>> translateDraftAsync,
            bool isDeepLAvailable,
            Func<string, string>? localize = null)
        {
            ArgumentNullException.ThrowIfNull(translationText);
            ArgumentNullException.ThrowIfNull(translateDraftAsync);

            _translateDraftAsync = translateDraftAsync;
            _localize = localize ?? Localizer.GetString;

            SourceText = translationText.OriginalText;
            OriginalTranslation = translationText.TranslatedText ?? string.Empty;
            SourceLanguage = translationText.SourceLanguage;
            TargetLanguage = translationText.TargetLanguage;
            Engine = translationText.Engine;
            ModelId = translationText.ModelId;
            ModelSha256 = translationText.ModelSha256;
            TranslationKind = translationText.TranslationKind;
            OriginalNpcSpeaker = translationText.TranslationKind == MiLMMTTranslationKind.Dialogue
                && !string.IsNullOrWhiteSpace(translationText.OriginalSpeaker)
                    ? translationText.OriginalSpeaker.Trim()
                    : null;
            IsDeepLAvailable = isDeepLAvailable;

            _suggestedTranslation = OriginalTranslation;
            _baselineDraft = OriginalTranslation;
        }

        public string SourceText { get; }
        public string OriginalTranslation { get; }
        public TranslationLanguageCode SourceLanguage { get; }
        public TranslationLanguageCode TargetLanguage { get; }
        public TranslatorEngine? Engine { get; }
        public string? ModelId { get; }
        public string? ModelSha256 { get; }
        public MiLMMTTranslationKind TranslationKind { get; }
        public string? OriginalNpcSpeaker { get; }
        public bool HasNpcSpeaker => !string.IsNullOrWhiteSpace(OriginalNpcSpeaker);
        public bool IsDeepLAvailable { get; }

        public string EngineDisplayLabel => Engine switch
        {
            TranslatorEngine.MiLMMT when !string.IsNullOrWhiteSpace(ModelId) => $"MiLMMT ({ModelId})",
            TranslatorEngine.MiLMMT => "MiLMMT",
            TranslatorEngine.Papago => "Papago",
            TranslatorEngine.DeepL_API => "DeepL (API)",
            _ => "Unknown",
        };

        public string? DeepLDraftToolTip => IsDeepLAvailable
            ? null
            : _localize("chat.report.draft.deepl_unavailable");

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDraftEdited))]
        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        private string _suggestedTranslation = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        private string _glossaryHintText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        private string _comment = string.Empty;

        [ObservableProperty]
        private string _draftSource = DraftSourceCurrent;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [NotifyCanExecuteChangedFor(nameof(ResetToCurrentDraftCommand))]
        [NotifyCanExecuteChangedFor(nameof(LoadPapagoDraftCommand))]
        [NotifyCanExecuteChangedFor(nameof(LoadDeepLDraftCommand))]
        private bool _isLoadingDraft;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasDraftStatusMessage))]
        private string _draftStatusMessage = string.Empty;

        public bool HasDraftStatusMessage => !string.IsNullOrWhiteSpace(DraftStatusMessage);

        public bool IsDraftEdited => !string.Equals(
            SuggestedTranslation.Trim(),
            _baselineDraft.Trim(),
            StringComparison.Ordinal);

        public bool CanSubmit => !IsLoadingDraft
            && ((!string.IsNullOrWhiteSpace(SuggestedTranslation)
                    && !string.Equals(SuggestedTranslation.Trim(), OriginalTranslation.Trim(), StringComparison.Ordinal))
                || GlossaryHintParser.Parse(GlossaryHintText).Count > 0
                || !string.IsNullOrWhiteSpace(Comment));

        private bool CanResetToCurrentDraft() => !IsLoadingDraft;

        private bool CanLoadPapagoDraft() => !IsLoadingDraft && Engine != TranslatorEngine.Papago;

        private bool CanLoadDeepLDraft() => !IsLoadingDraft
            && Engine != TranslatorEngine.DeepL_API
            && IsDeepLAvailable;

        [RelayCommand(CanExecute = nameof(CanResetToCurrentDraft))]
        private void ResetToCurrentDraft()
        {
            _baselineDraft = OriginalTranslation;
            DraftSource = DraftSourceCurrent;
            SuggestedTranslation = OriginalTranslation;
            DraftStatusMessage = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanLoadPapagoDraft))]
        private async Task LoadPapagoDraftAsync(CancellationToken cancellationToken)
        {
            await LoadDraftAsync(TranslatorEngine.Papago, DraftSourcePapago, cancellationToken);
        }

        [RelayCommand(CanExecute = nameof(CanLoadDeepLDraft))]
        private async Task LoadDeepLDraftAsync(CancellationToken cancellationToken)
        {
            await LoadDraftAsync(TranslatorEngine.DeepL_API, DraftSourceDeepL, cancellationToken);
        }

        private async Task LoadDraftAsync(
            TranslatorEngine draftEngine,
            string draftSourceKey,
            CancellationToken cancellationToken)
        {
            IsLoadingDraft = true;
            DraftStatusMessage = _localize("chat.report.draft.loading");
            try
            {
                var translated = await _translateDraftAsync(
                    draftEngine,
                    SourceText,
                    SourceLanguage,
                    TargetLanguage,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(translated)
                    || string.Equals(translated.Trim(), SourceText.Trim(), StringComparison.Ordinal))
                {
                    DraftStatusMessage = _localize("chat.report.draft.failed");
                    return;
                }

                _baselineDraft = translated;
                DraftSource = draftSourceKey;
                SuggestedTranslation = translated;
                DraftStatusMessage = string.Empty;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                DraftStatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load {DraftEngine} draft for translation report.", draftEngine);
                DraftStatusMessage = _localize("chat.report.draft.failed");
            }
            finally
            {
                IsLoadingDraft = false;
            }
        }

        public BiggsReportRequest BuildRequest(string? appVersion = null, DateTime? timestampUtc = null)
        {
            var glossaryTerms = GlossaryHintParser.Parse(GlossaryHintText);
            return BiggsReportRequest.Create(
                appVersion: appVersion ?? BuildInfo.ReleaseVersion,
                translationKind: TranslationKind,
                originalNpcSpeaker: OriginalNpcSpeaker,
                sourceLanguage: SourceLanguage,
                targetLanguage: TargetLanguage,
                sourceText: SourceText,
                engine: Engine,
                modelId: ModelId,
                modelSha256: ModelSha256,
                originalTranslation: OriginalTranslation,
                suggestedTranslation: SuggestedTranslation,
                draftSource: DraftSource,
                draftEdited: IsDraftEdited,
                glossaryTerms: glossaryTerms,
                comment: Comment,
                timestampUtc: timestampUtc);
        }
    }
}
