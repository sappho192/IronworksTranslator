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

        public void OnNavigatedTo()
        {
            if (!_isInitialized)
                InitializeViewModel();
        }

        public void OnNavigatedFrom() { }

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
        private void OnChangeChatboxFontSize(int parameter)
        {

        }
    }
}
