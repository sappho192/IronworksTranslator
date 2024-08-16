using IronworksTranslator.Helpers;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils;
using ObservableCollections;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace IronworksTranslator.ViewModels.Pages
{
#pragma warning disable CS8602
    public partial class SettingsViewModel : ObservableRecipient, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ApplicationTheme _currentTheme = IronworksSettings.Instance.UiSettings.Theme;
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private AppLanguage _appLanguage = IronworksSettings.Instance.UiSettings.AppLanguage;
        [ObservableProperty]
        private int _appLanguageIndex = (int)IronworksSettings.Instance.UiSettings.AppLanguage;
        [ObservableProperty]
        private string _exampleChatBox = $"이프 저격하는 무작위 레벨링 가실 분~{Environment.NewLine}エキルレ行く方いますか？{Environment.NewLine}Mechanics are for Cars KUPO!{Environment.NewLine}제작자: 사포 (sappho192@gmail.com)";

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private int _chatBoxFontSize = IronworksSettings.Instance.ChatUiSettings.ChatboxFontSize;
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private int _chatMargin = IronworksSettings.Instance.ChatUiSettings.ChatMargin;

        private readonly LogScale _logScale = new(0.01, 1.0);
        [ObservableProperty]
        private double _childWindowOpacityRaw = IronworksSettings.Instance.ChatUiSettings.WindowOpacity;
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private double _childWindowOpacity = IronworksSettings.Instance.ChatUiSettings.WindowOpacity;
        partial void OnChildWindowOpacityRawChanged(double value)
        {
            ChildWindowOpacity = _logScale.Scale(ChildWindowOpacityRaw);
        }

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(ClientLanguageIndex))]
        private ClientLanguage _clientLanguage = IronworksSettings.Instance.TranslatorSettings.ClientLanguage;
        [ObservableProperty]
        private int _clientLanguageIndex = (int)IronworksSettings.Instance.TranslatorSettings.ClientLanguage;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(TranslatorEngineIndex))]
        private TranslatorEngine _translatorEngine = IronworksSettings.Instance.TranslatorSettings.TranslatorEngine;
        [ObservableProperty]
        private int _translatorEngineIndex = (int)IronworksSettings.Instance.TranslatorSettings.TranslatorEngine;

        [ObservableProperty]
        private INotifyCollectionChangedSynchronizedView<string> _deeplApiKeys = 
            IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys.CreateView(key => key).ToNotifyCollectionChanged();
        [ObservableProperty]
        private int _selectedDeeplApiKeyIndex = 0;
        [ObservableProperty]
        private string _newDeepLApiKey = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(DialogueTranslationMethodIndex))]
        private DialogueTranslationMethod _dialogueTranslationMethod = IronworksSettings.Instance.TranslatorSettings.DialogueTranslationMethod;
        [ObservableProperty]
        private int _dialogueTranslationMethodIndex = (int)IronworksSettings.Instance.TranslatorSettings.DialogueTranslationMethod;

        [ObservableProperty]
        private List<string> _fontList = ChatUISettings.systemFontList;
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        [NotifyPropertyChangedFor(nameof(CurrentFontIndex))]
        private string _currentFont = IronworksSettings.Instance.ChatUiSettings.Font;
        [ObservableProperty]
        private int _currentFontIndex = ChatUISettings.systemFontList.IndexOf(IronworksSettings.Instance.ChatUiSettings.Font);

        private readonly IContentDialogService _contentDialogService;
        public SettingsViewModel(IContentDialogService contentDialogService)
        {
            _contentDialogService = contentDialogService;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                InitializeViewModel();
        }

        public void OnNavigatedFrom() { }

        [TraceMethod]
        private void InitializeViewModel()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"{Localizer.GetString("version")}: {GetAssemblyVersion()}";

            _isInitialized = true;
        }

        private static string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                ?? string.Empty;
        }

        [RelayCommand]
        [TraceMethod]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;

                    break;
            }
        }

        [RelayCommand]
        [TraceMethod]
        private async Task OnAddDeepLApiKey(object content)
        {
            ContentDialogResult dialogResult = await _contentDialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions()
                {
                    Title = Localizer.GetString("settings.translator.engine.apikey"),
                    Content = content,
                    PrimaryButtonText = Localizer.GetString("confirm"),
                    CloseButtonText = Localizer.GetString("cancel"),
                }
            );

            var resultBool = dialogResult switch
            {
                ContentDialogResult.Primary => true,
                ContentDialogResult.Secondary => false,
                _ => false
            };

            if (!resultBool)
                return;

            var apiKey = NewDeepLApiKey.Trim();

            if (string.IsNullOrEmpty(apiKey))
                return;
            
            // Check if the API key already exists
            if (DeeplApiKeys.Any(key => key == apiKey))
            {
                var errorDialog = new Wpf.Ui.Controls.MessageBox
                {
                    Title = Localizer.GetString("error"),
                    Content = Localizer.GetString("settings.translator.engine.apikey.exists"),
                    CloseButtonText = Localizer.GetString("huh")
                };
                errorDialog.ShowDialogAsync();
                return;
            }

            IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys.Add(apiKey);
        }

        [RelayCommand]
        [TraceMethod]
        private void OnRemoveDeepLApiKeyInfo()
        {
            if (SelectedDeeplApiKeyIndex < 0)
                return;
            IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys.RemoveAt(SelectedDeeplApiKeyIndex);
            if (IronworksSettings.Instance.TranslatorSettings.DeeplApiKeys.Count == 0)
            {
                System.Windows.MessageBox.Show(Localizer.GetString("settings.translator.engine.default.fallback"));
                TranslatorEngine = (TranslatorEngine)0;
                TranslatorEngineIndex = 0;
            }
        }
    }
#pragma warning restore CS8602
}
