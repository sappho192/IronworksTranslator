using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.ViewModels.Pages;

namespace IronworksTranslator.ViewModels.Windows
{
    public partial class DialogueWindowViewModel : ObservableRecipient
    {
        public DialogueWindowViewModel() 
        {
            Messenger.Register<PropertyChangedMessage<double>>(this, OnDoubleMessage);
        }

#pragma warning disable CS8602
        [ObservableProperty]
        private bool _isDraggable = IronworksSettings.Instance.ChatUiSettings.IsDraggable;
        [ObservableProperty]
        private bool _isResizable = IronworksSettings.Instance.ChatUiSettings.IsResizable;
        [ObservableProperty]
        private double _windowOpacity = IronworksSettings.Instance.ChatUiSettings.WindowOpacity;
#pragma warning restore CS8602

        private void OnDoubleMessage(object recipient, PropertyChangedMessage<double> message)
        {
            switch (message.PropertyName)
            {
                case nameof(SettingsViewModel.ChildWindowOpacity):
                    WindowOpacity = message.NewValue;
                    break;
            }
        }
    }
}
