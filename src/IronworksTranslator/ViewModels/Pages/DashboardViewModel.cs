using IronworksTranslator.Models.Settings;
using IronworksTranslator.Services.FFXIV;
using IronworksTranslator.Utils;
using IronworksTranslator.Views.Windows;
using MdXaml;
using Microsoft.Extensions.Hosting;
using Octokit;
using Serilog;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfScreenHelper;

namespace IronworksTranslator.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableRecipient
    {
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

        public DashboardViewModel()
        {
            // run CheckUpdate() in different thread
            System.Windows.Application.Current.Dispatcher.InvokeAsync(CheckUpdate);
            if (!IronworksSettings.Instance.UiSettings.IsTosDisplayed)
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(ShowTosDialog);
            }
        }

        [TraceMethod]
        private void ShowTosDialog()
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
                return;
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
            var tosResult = tosMessageBox.ShowDialogAsync();
            if (tosResult.Result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
#pragma warning disable CS8602
                IronworksSettings.Instance.UiSettings.IsTosDisplayed = true;
#pragma warning restore CS8602
            }
            else
            {
                App.RequestShutdown();
            }
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

        [TraceMethod]
        private void CheckUpdate()
        {
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var githubClient = new GitHubClient(new ProductHeaderValue("IronworksTranslator"));
            var latestRelease = githubClient.Repository.Release.GetLatest("sappho192", "ironworkstranslator").Result;
            var latestVersion = new Version(latestRelease.TagName);

            if (currentVersion == null)
            {
                Log.Fatal("Failed to get latest version");
                MessageBox.Show("app.exception.description");
                App.RequestShutdown();
                return;
            }
            if (currentVersion.CompareTo(latestVersion) >= 0)
            {
                Log.Information("IronworksTranslator is up to date");
            }
            else
            {
                Log.Information("IronworksTranslator is outdated");
                Log.Information($"Current version: {currentVersion}");
                Log.Information($"Latest version: {latestVersion}");

                AskToUpdate(currentVersion, latestRelease, latestVersion);
            }
        }

#pragma warning disable CS8602
        private static void AskToUpdate(Version? currentVersion, Release latestRelease, Version latestVersion)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Localizer.GetString("main.update.description"));
            sb.AppendLine();
            sb.AppendLine($"{Localizer.GetString("main.update.current_version")}{currentVersion.ToString(3)}");
            sb.AppendLine($"{Localizer.GetString("main.update.latest_version")}**{latestVersion.ToString(3)}**");
            sb.AppendLine();
            sb.AppendLine(latestRelease.Body);
            var updateContent = sb.ToString();
            var markdownEngine = new Markdown();
            FlowDocument document = markdownEngine.Transform(updateContent);
            document.FontFamily = new System.Windows.Media.FontFamily("sans-serif");
            var scrollViewer = new ScrollViewer
            {
                Content = new RichTextBox
                {
                    Document = document,
                    IsReadOnly = true,
                },
            };
            var updateMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = Localizer.GetString("main.update.title"),
                Content = scrollViewer,
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = Localizer.GetString("yes"),
                CloseButtonText = Localizer.GetString("no"),
            };
            var result = updateMessageBox.ShowDialogAsync();
            if (result.Result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                Log.Information("User clicked Yes to update PartyYomi");
                var ps = new ProcessStartInfo(latestRelease.Assets[0].BrowserDownloadUrl)
                {
                    UseShellExecute = true,
                    Verb = "open"
                };
                Process.Start(ps);
            }
        }
#pragma warning restore CS8602
    }
}
