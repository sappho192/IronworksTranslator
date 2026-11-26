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

        // Start typing in the editable suggestion instead of wherever the dialog puts initial focus.
        // The default (Primary) button takes Enter unless the focused control accepts returns, and
        // WPF-UI 4.x (master since #92; this branch still pins 3.1.1) focuses the first focusable
        // control in the content on load, which would be the read-only source box.
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
