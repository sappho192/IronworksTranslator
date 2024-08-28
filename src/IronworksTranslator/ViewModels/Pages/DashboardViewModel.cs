﻿using IronworksTranslator.Models.Settings;
using IronworksTranslator.Services.FFXIV;
using IronworksTranslator.Utils;
using IronworksTranslator.Views.Windows;
using MdXaml;
using Microsoft.Extensions.Hosting;
using Octokit;
using Serilog;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;

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
        private void CheckUpdate()
        {
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var githubClient = new GitHubClient(new ProductHeaderValue("IronworksTranslator"));
            var latestRelease = githubClient.Repository.Release.GetLatest("sappho192", "ironworkstranslator").Result;
            var latestVersion = new Version(latestRelease.TagName);

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
                CloseButtonText = Localizer.GetString("no")
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
    }
}
