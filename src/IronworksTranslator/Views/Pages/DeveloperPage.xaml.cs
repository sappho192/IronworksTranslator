using IronworksTranslator.ViewModels.Pages;
using Wpf.Ui.Controls;

namespace IronworksTranslator.Views.Pages
{
    /// <summary>
    /// DeveloperPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DeveloperPage : INavigableView<DeveloperViewModel>
    {
        public DeveloperViewModel ViewModel { get; }

        public DeveloperPage(DeveloperViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }
    }
}
