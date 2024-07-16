using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace IronworksTranslator.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableRecipient, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private ApplicationTheme _currentTheme = IronworksSettings.Instance.UiSettings.Theme;
        [ObservableProperty]
        private string _exampleChatBox = $"이프 저격하는 무작위 레벨링 가실 분~{Environment.NewLine}エキルレ行く方いますか？{Environment.NewLine}Mechanics are for Cars KUPO!{Environment.NewLine}제작자: 사포 (sappho192@gmail.com)";

        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private int _chatBoxFontSize = IronworksSettings.Instance.ChatUiSettings.ChatboxFontSize;

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
    }
}
