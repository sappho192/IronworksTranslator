using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.Utils.Translators;
using Serilog;

namespace IronworksTranslator.ViewModels.Pages
{
    public partial class ComposeViewModel : ObservableObject
    {
        private readonly Func<TranslatorBase> _getTranslator;
        private readonly Func<string, string> _localize;

        public ComposeViewModel() : this(() => IronworksSettings.Instance?.TranslatorSettings?.TranslatorEngine switch
        {
            TranslatorEngine.DeepL_API => App.GetService<DeepLAPITranslator>(),
            TranslatorEngine.MiLMMT => App.GetService<MiLMMTTranslator>(),
            _ => App.GetService<PapagoTranslator>()
        }) { }

        public TranslatorSettings Settings { get; }
        private bool _replacingInput;
        private bool _inputIsResult;
        private string _originalInput = string.Empty;

        internal ComposeViewModel(Func<TranslatorBase> getTranslator, Func<string, string>? localize = null, TranslatorSettings? settings = null)
        {
            _getTranslator = getTranslator;
            _localize = localize ?? Localizer.GetString;
            Settings = settings ?? IronworksSettings.Instance?.TranslatorSettings ?? new TranslatorSettings();
            Settings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TranslatorSettings.ComposeShowDetails))
                {
                    OnPropertyChanged(nameof(MinimumWindowHeight));
                    if (_inputIsResult && Settings.ComposeShowDetails)
                        ReplaceInput(_originalInput, false);
                    else if (!Settings.ComposeShowDetails && InputText == _originalInput && !string.IsNullOrWhiteSpace(TranslatedText))
                        ReplaceInput(TranslatedText, true);
                }
            };
        }

        public TranslationLanguageCode SourceLanguage { get => Settings.ComposeSourceLanguage; set => Settings.ComposeSourceLanguage = value; }
        public TranslationLanguageCode TargetLanguage { get => Settings.ComposeTargetLanguage; set => Settings.ComposeTargetLanguage = value; }

        public record LanguageOption(TranslationLanguageCode Code, string Name);
        public static LanguageOption[] LanguageOptions { get; } =
        [
            new(TranslationLanguageCode.Korean, "한국어"),
            new(TranslationLanguageCode.Japanese, "日本語"),
            new(TranslationLanguageCode.English, "English"),
            new(TranslationLanguageCode.German, "Deutsch"),
            new(TranslationLanguageCode.French, "Français")
        ];

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(TranslateCommand))]
        private string _inputText = string.Empty;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
        private string _translatedText = string.Empty;
        [ObservableProperty]
        private string _backTranslation = string.Empty;
        [ObservableProperty]
        private bool _checkTranslation = true;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIdle))]
        [NotifyCanExecuteChangedFor(nameof(TranslateCommand))]
        [NotifyCanExecuteChangedFor(nameof(SwapLanguagesCommand))]
        private bool _isBusy;
        [ObservableProperty]
        private string _status = string.Empty;
        public bool IsIdle => !IsBusy;
        public double MinimumWindowHeight => Settings.ComposeShowDetails ? 300 : 140;
        private bool CanTranslate() => IsIdle && !_inputIsResult && !string.IsNullOrWhiteSpace(InputText);
        private bool CanSwapLanguages() => IsIdle;
        private bool CanCopy() => !string.IsNullOrWhiteSpace(TranslatedText);

        [RelayCommand(CanExecute = nameof(CanSwapLanguages))]
        private void SwapLanguages() => (SourceLanguage, TargetLanguage) = (TargetLanguage, SourceLanguage);

        [RelayCommand(CanExecute = nameof(CanTranslate))]
        private async Task TranslateAsync()
        {
            var input = InputText.Trim();
            var source = SourceLanguage;
            var target = TargetLanguage;
            if (!CanTranslate()) return;
            _originalInput = InputText;
            var check = Settings.ComposeShowDetails && CheckTranslation;
            IsBusy = true;
            TranslatedText = BackTranslation = string.Empty;
            Status = _localize("compose.busy");
            try
            {
                var translator = _getTranslator();
                TranslatedText = source == target ? input : await Task.Run(() => translator.TranslateAsync(input, source, target));
                Status = TranslatedText == input && source != target ? _localize("compose.unchanged") : string.Empty;
                if (string.IsNullOrWhiteSpace(TranslatedText))
                {
                    Status = _localize("compose.failed");
                    return;
                }
                if (!Settings.ComposeShowDetails && TranslatedText != input)
                    ReplaceInput(TranslatedText, true);
                if (check && !string.IsNullOrWhiteSpace(TranslatedText))
                {
                    var result = TranslatedText;
                    BackTranslation = source == target ? result : await Task.Run(() => translator.TranslateAsync(result, target, source));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Manual translation failed.");
                Status = _localize("compose.failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnInputTextChanged(string value)
        {
            if (_replacingInput) return;
            _inputIsResult = false;
            TranslatedText = BackTranslation = Status = string.Empty;
        }

        private void ReplaceInput(string text, bool isResult)
        {
            _replacingInput = true;
            InputText = text;
            _inputIsResult = isResult;
            _replacingInput = false;
            TranslateCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanCopy))]
        private void Copy()
        {
            try
            {
                Clipboard.SetText(TranslatedText);
                Status = _localize("compose.copied");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy manual translation.");
                Status = _localize("compose.copy_failed");
            }
        }
    }
}
