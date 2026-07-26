using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.ReleaseNotes;
using IronworksTranslator.Services;
using IronworksTranslator.Services.FFXIV;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Aspect;
using IronworksTranslator.Views.Windows;
using MdXaml;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfScreenHelper;

namespace IronworksTranslator.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableRecipient
    {
        private readonly AppUpdateService _appUpdateService;
        private readonly GitHubReleaseNotesService _releaseNotesService;
        private int _isLoadingReleaseNotes;
        private int _releaseNotesLoaded;

        [ObservableProperty]
        private bool _isTranslatorActive = false;

        [ObservableProperty]
        private string _translatorToogleState = Localizer.GetString("dashboard.translator.disabled");
        [ObservableProperty]
        private string _translatorToogleStateDescription = Localizer.GetString("dashboard.translator.disabled.description");
        [ObservableProperty]
        private string _translatorIcon = "DesktopSpeakerOff20";

#pragma warning disable CS8602
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _isDialogueWindowVisible = IronworksSettings.Instance.UiSettings.DialogueWindowVisible;
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _isChildWindowDraggable = IronworksSettings.Instance.ChatUiSettings.IsDraggable;
        [ObservableProperty]
        [NotifyPropertyChangedRecipients]
        private bool _isChildWindowResizable = IronworksSettings.Instance.ChatUiSettings.IsResizable;
#pragma warning restore CS8602

        [ObservableProperty]
        private string _releaseNotesStatus =
            Localizer.GetString("dashboard.releases.loading");

        public ObservableCollection<GitHubReleaseNote> RecentReleaseNotes { get; } = [];

        public string ReleaseNotesHeading => string.Format(
            Localizer.GetString("dashboard.releases.title"),
            BuildInfo.ReleaseChannel.DisplayName);

        public DashboardViewModel(
            AppUpdateService appUpdateService,
            GitHubReleaseNotesService releaseNotesService)
        {
            _appUpdateService = appUpdateService;
            _releaseNotesService = releaseNotesService;
        }

        public async Task LoadReleaseNotesAsync(CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _releaseNotesLoaded) != 0
                || Interlocked.Exchange(ref _isLoadingReleaseNotes, 1) != 0)
            {
                return;
            }

            ReleaseNotesStatus = Localizer.GetString("dashboard.releases.loading");
            try
            {
                var releases = await _releaseNotesService.GetRecentAsync(
                    BuildInfo.ReleaseChannel,
                    cancellationToken);
                RecentReleaseNotes.Clear();
                foreach (var release in releases)
                {
                    RecentReleaseNotes.Add(release);
                }

                ReleaseNotesStatus = releases.Count == 0
                    ? Localizer.GetString("dashboard.releases.empty")
                    : string.Empty;
                Volatile.Write(ref _releaseNotesLoaded, 1);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ReleaseNotesStatus = string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load recent GitHub release notes.");
                ReleaseNotesStatus = Localizer.GetString("dashboard.releases.failed");
            }
            finally
            {
                Interlocked.Exchange(ref _isLoadingReleaseNotes, 0);
            }
        }

        [RelayCommand]
        private async Task OnShowReleaseNotesAsync(GitHubReleaseNote? release)
        {
            if (release == null)
            {
                return;
            }

            var markdown = string.IsNullOrWhiteSpace(release.Body)
                ? Localizer.GetString("dashboard.releases.no_notes")
                : release.Body;
            FlowDocument document;
            try
            {
                document = new Markdown().Transform(markdown);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to render GitHub release notes as Markdown.");
                document = new FlowDocument(new Paragraph(new Run(markdown)));
            }

            document.FontFamily = new System.Windows.Media.FontFamily("sans-serif");
            document.ColumnWidth = double.PositiveInfinity;
            document.PagePadding = new Thickness(0);
            var releaseNotesViewer = new RichTextBox
            {
                Document = document,
                Height = 440,
                IsDocumentEnabled = true,
                IsReadOnly = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            releaseNotesViewer.Loaded += (_, _) =>
            {
                releaseNotesViewer.CaretPosition = document.ContentStart;
                releaseNotesViewer.ScrollToHome();
            };
            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = release.DisplayTitle,
                Width = 700,
                Content = releaseNotesViewer,
                CloseButtonText = Localizer.GetString("dashboard.releases.close"),
            };

            await messageBox.ShowDialogAsync();
        }

        [TraceMethod]
        public async Task RunStartupPromptsAsync()
        {
            var uiSettings = IronworksSettings.Instance?.UiSettings;
            if (uiSettings == null)
            {
                Log.Error("Startup prompts skipped because UI settings are not loaded.");
                return;
            }

            if (!uiSettings.IsTosDisplayed)
            {
                var acceptedTos = await ShowTosDialogAsync();
                if (!acceptedTos)
                {
                    return;
                }
            }

            await _appUpdateService.CheckForUpdatesWithPromptAsync();
        }

        [TraceMethod]
        private async Task<bool> ShowTosDialogAsync()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var cultureInfo = Localizer.GetCulture();
            string ns = "IronworksTranslator.Resources.Strings";
            string tosFilename = cultureInfo.Name switch
            {
                "ko-KR" => $"{ns}.terms-and-privacy-policy-ko.md",
                _ => $"{ns}.terms-and-privacy-policy-en.md",
            };
            using var stream = assembly.GetManifestResourceStream(tosFilename);
            if (stream == null)
            {
                Log.Fatal("Failed to load TOS file: {TOSFilename}", tosFilename);
                MessageBox.Show(Localizer.GetString("dashboard.tos.file.notfound"),
                    Localizer.GetString("dashboard.tos.file.notfound.title"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
                App.RequestShutdown();
                return false;
            }
            using var reader = new StreamReader(stream);
            string tos = reader.ReadToEnd();

            var markdownEngine = new Markdown();
            FlowDocument document = markdownEngine.Transform(tos);
            document.FontFamily = new System.Windows.Media.FontFamily("sans-serif");
            var scrollViewer = new ScrollViewer
            {
                Content = new RichTextBox
                {
                    Document = document,
                    IsReadOnly = true,
                },
            };
            var tosMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = Localizer.GetString("dashboard.tos.title"),
                Content = scrollViewer,
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = Localizer.GetString("accept"),
                CloseButtonText = Localizer.GetString("decline")
            };
            var tosResult = await tosMessageBox.ShowDialogAsync();
            if (tosResult == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
#pragma warning disable CS8602
                IronworksSettings.Instance.UiSettings.IsTosDisplayed = true;
#pragma warning restore CS8602
                return true;
            }

            App.RequestShutdown();
            return false;
        }

        [TraceMethod]
        [RelayCommand]
        public void OnTranslatorToggle()
        {
            if (IsTranslatorActive)
            {
                TranslatorToogleState = Localizer.GetString("dashboard.translator.enabled");
                TranslatorToogleStateDescription = Localizer.GetString("dashboard.translator.enabled.description");
                TranslatorIcon = "DesktopSpeaker20";
                var chatLookupService = App.GetServices<IHostedService>().OfType<ChatLookupService>().Single();
                chatLookupService.Initialize();
            }
            else
            {
                TranslatorToogleState = Localizer.GetString("dashboard.translator.disabled");
                TranslatorToogleStateDescription = Localizer.GetString("dashboard.translator.disabled.description");
                TranslatorIcon = "DesktopSpeakerOff20";
                var chatLookupService = App.GetServices<IHostedService>().OfType<ChatLookupService>().Single();
                chatLookupService.Destruct();
            }
        }

        public void InitTranslatorToggle()
        {
            if (IsTranslatorActive)
            {
                TranslatorToogleState = Localizer.GetString("dashboard.translator.enabled");
                TranslatorToogleStateDescription = Localizer.GetString("dashboard.translator.enabled.description");
                TranslatorIcon = "DesktopSpeaker20";
            }
            else
            {
                TranslatorToogleState = Localizer.GetString("dashboard.translator.disabled");
                TranslatorToogleStateDescription = Localizer.GetString("dashboard.translator.disabled.description");
                TranslatorIcon = "DesktopSpeakerOff20";
            }
        }

        [TraceMethod]
        [RelayCommand]
        public void OnDialogueWindowVisibilityToggle()
        {
            var dialogueWindow = App.GetService<DialogueWindow>();
            if (IsDialogueWindowVisible)
            {
                dialogueWindow.Show();
            }
            else
            {
                dialogueWindow.Hide();
            }
        }

        [TraceMethod]
        [RelayCommand]
        public void OnChildWindowDraggableToggle()
        {
            var chatWindow = App.GetService<ChatWindow>();
            var dialogueWindow = App.GetService<DialogueWindow>();
            if (IsChildWindowDraggable)
            {
                chatWindow.ViewModel.IsDraggable = true;
                dialogueWindow.ViewModel.IsDraggable = true;
            }
            else
            {
                chatWindow.ViewModel.IsDraggable = false;
                dialogueWindow.ViewModel.IsDraggable = false;
            }
        }

        [TraceMethod]
        [RelayCommand]
        public void OnChildWindowResizableToggle()
        {
            var chatWindow = App.GetService<ChatWindow>();
            var dialogueWindow = App.GetService<DialogueWindow>();
            if (IsChildWindowResizable)
            {
                chatWindow.ResizeMode = ResizeMode.CanResizeWithGrip;
                dialogueWindow.ResizeMode = ResizeMode.CanResizeWithGrip;
            }
            else
            {
                chatWindow.ResizeMode = ResizeMode.NoResize;
                dialogueWindow.ResizeMode = ResizeMode.NoResize;
            }
        }

        [TraceMethod]
        [RelayCommand]
        public void OnResetChatWindowPosition()
        {
            var chatWindow = App.GetService<ChatWindow>();
            WindowHelper.SetWindowPosition(chatWindow, WpfScreenHelper.Enum.WindowPositions.Center, Screen.PrimaryScreen);
        }

        [TraceMethod]
        [RelayCommand]
        public void OnResetDialogueWindowPosition()
        {
            var dialogueWindow = App.GetService<DialogueWindow>();
            WindowHelper.SetWindowPosition(dialogueWindow, WpfScreenHelper.Enum.WindowPositions.Center, Screen.PrimaryScreen);
        }

    }
}
