using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Cloudflare;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.ViewModels.Dialogs;
using IronworksTranslator.Views.Dialogs;
using IronworksTranslator.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using MessageBox = System.Windows.MessageBox;

namespace IronworksTranslator.Services
{
    public sealed class TranslationReportService
    {
        private readonly IContentDialogService _contentDialogService;
        private readonly BiggsWorker _biggsWorker;
        private readonly PapagoTranslator _papagoTranslator;
        private readonly DeepLAPITranslator _deepLTranslator;

        public TranslationReportService(
            IContentDialogService contentDialogService,
            BiggsWorker biggsWorker,
            PapagoTranslator papagoTranslator,
            DeepLAPITranslator deepLTranslator)
        {
            _contentDialogService = contentDialogService;
            _biggsWorker = biggsWorker;
            _papagoTranslator = papagoTranslator;
            _deepLTranslator = deepLTranslator;
        }

        public async Task ShowReportDialogAsync(
            TranslationText translationText,
            CancellationToken cancellationToken = default)
        {
            if (translationText == null || string.IsNullOrWhiteSpace(translationText.OriginalText))
            {
                return;
            }

            var mainWindow = App.GetServices<INavigationWindow>().OfType<MainWindow>().Single();
            if (mainWindow.WindowState == WindowState.Minimized || mainWindow.Visibility != Visibility.Visible)
            {
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Show();
            }

            var isDeepLAvailable = IronworksSettings.Instance?.TranslatorSettings?.DeeplApiKeys?
                .Any(key => !string.IsNullOrWhiteSpace(key)) == true;

            var viewModel = new TranslationReportViewModel(
                translationText,
                TranslateDraftAsync,
                isDeepLAvailable);

            while (true)
            {
                var content = new TranslationReportDialogContent(viewModel);
                var dialogResult = await _contentDialogService.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = Localizer.GetString("chat.report"),
                        Content = content,
                        PrimaryButtonText = Localizer.GetString("chat.report.primary"),
                        CloseButtonText = Localizer.GetString("cancel"),
                    },
                    cancellationToken);

                if (dialogResult != ContentDialogResult.Primary)
                {
                    return;
                }

                if (!viewModel.CanSubmit)
                {
                    viewModel.DraftStatusMessage = Localizer.GetString("chat.report.empty_change");
                    continue;
                }

                var payload = viewModel.BuildRequest(BuildInfo.ReleaseVersion);
                var result = await _biggsWorker.SubmitReportAsync(payload, cancellationToken);
                if (result.IsSuccess)
                {
                    MessageBox.Show(Localizer.GetString("chat.report.send.success"));
                    return;
                }

                viewModel.DraftStatusMessage = Localizer.GetString("chat.report.send.fail");
            }
        }

        private Task<string> TranslateDraftAsync(
            TranslatorEngine engine,
            string input,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(
                () => engine switch
                {
                    TranslatorEngine.Papago => _papagoTranslator.TranslateAsync(input, sourceLanguage, targetLanguage),
                    TranslatorEngine.DeepL_API => _deepLTranslator.TranslateAsync(input, sourceLanguage, targetLanguage),
                    _ => Task.FromResult(input),
                },
                cancellationToken);
        }
    }
}
