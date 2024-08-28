using IronworksTranslator.Models.Settings;
using IronworksTranslator.Services.FFXIV;
using IronworksTranslator.Utils;
using IronworksTranslator.Views.Windows;
using Microsoft.Extensions.Hosting;

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
    }
}
