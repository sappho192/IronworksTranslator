using IronworksTranslator.Utils;
using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace IronworksTranslator.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = Localizer.GetString("app.name");

        [ObservableProperty]
        private ObservableCollection<object> _menuItems =
        [
            new NavigationViewItem()
            {
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            }
        ];

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems =
        [
#if DEBUG
            new NavigationViewItem()
            {
                Content = Localizer.GetString("main.navigation.developer"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Code24 },
                TargetPageType = typeof(Views.Pages.DeveloperPage)
            },
#endif
            new NavigationViewItem()
            {
                Content = Localizer.GetString("main.navigation.settings"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            },
        ];

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems =
        [
            new MenuItem { Header = "Home", Tag = "tray_home" }
        ];
    }
}
