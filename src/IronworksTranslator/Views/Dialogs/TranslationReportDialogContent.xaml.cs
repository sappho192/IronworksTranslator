using IronworksTranslator.ViewModels.Dialogs;
using System.Windows.Controls;
using System.Windows.Threading;

namespace IronworksTranslator.Views.Dialogs
{
    public partial class TranslationReportDialogContent : UserControl
    {
        public TranslationReportDialogContent(TranslationReportViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // WPF-UI 4.x ContentDialog focuses the first focusable control in its content on load,
        // which would be a read-only source box. Move focus to the editable suggestion so typing
        // starts there and Enter inserts a newline instead of triggering the default (Primary) button.
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                SuggestedTranslationTextBox.Focus();
                SuggestedTranslationTextBox.CaretIndex = SuggestedTranslationTextBox.Text.Length;
            });
        }

        // The comment box appears below the fold when expanded; scroll the dialog to it.
        // The expander slides its content in with a render transform, so bring the expander's
        // untransformed layout bounds into view rather than the still-animating text box.
        private void CommentExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is not Expander expander)
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                CommentTextBox.Focus();
                expander.BringIntoView();
            });
        }
    }
}
