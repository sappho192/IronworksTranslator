using IronworksTranslator.ViewModels.Dialogs;
using System.Windows.Controls;

namespace IronworksTranslator.Views.Dialogs
{
    public partial class TranslationReportDialogContent : UserControl
    {
        public TranslationReportDialogContent(TranslationReportViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
