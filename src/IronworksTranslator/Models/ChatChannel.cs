﻿using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils;
using Serilog;
using YamlDotNet.Serialization;

namespace IronworksTranslator.Models
{

    public partial class ChatChannel : ObservableRecipient
    {
        public ChatChannel()
        {
            Messenger.Register<PropertyChangedMessage<bool>>(this, OnBoolMessage);
            Messenger.Register<PropertyChangedMessage<ClientLanguage>>(this, OnClientLanguageMessage);
        }

        [ObservableProperty]
        [property: YamlMember(Alias = "show")]
        private bool _show;

        [ObservableProperty]
        [property: YamlMember(Alias = "code")]
        private ChatCode _code;

        [ObservableProperty]
        [property: YamlMember(Alias = "major_language")]
        private ClientLanguage _majorLanguage;

        [SaveSettingsOnChange]
        partial void OnShowChanged(bool value)
        {
            Log.Information($"{Code}: Show changed to {value}");
        }

        [SaveSettingsOnChange]
        partial void OnMajorLanguageChanged(ClientLanguage value)
        {
            Log.Information($"{Code}: MajorLanguage changed to {value}");
        }

        private void OnBoolMessage(object recipient, PropertyChangedMessage<bool> message)
        {
            //switch (message.PropertyName)
            //{
            //    case nameof(SettingsViewModel.ChatChannelShow):
            Show = message.NewValue;
            //        break;
            //}
        }

        private void OnClientLanguageMessage(object recipient, PropertyChangedMessage<ClientLanguage> message)
        {
            //switch (message.PropertyName)
            //{
            //    case nameof(SettingsViewModel.ChatChannelMajorLanguage):
            MajorLanguage = message.NewValue;
            //        break;
            //}
        }
    }
}
