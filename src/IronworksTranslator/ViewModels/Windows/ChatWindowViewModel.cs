using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Services;
using IronworksTranslator.Services.FFXIV;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Aspect;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.Views.Windows;
using Serilog;
using Sharlayan.Core;
using System.Collections.Frozen;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace IronworksTranslator.ViewModels.Windows
{
    public partial class ChatWindowViewModel : ObservableRecipient
    {
#pragma warning disable CS8602
        [ObservableProperty]
        private bool _isDraggable = IronworksSettings.Instance.ChatUiSettings.IsDraggable;
        [ObservableProperty]
        private double _chatWindowOpacity = IronworksSettings.Instance.ChatUiSettings.ChatWindowOpacity;
        [ObservableProperty]
        private double _width = IronworksSettings.Instance.UiSettings.ChatWindowWidth;
        [ObservableProperty]
        private double _height = IronworksSettings.Instance.UiSettings.ChatWindowHeight;
#pragma warning restore CS8602
        public FlowDocument ChatDocument { get; }

        private readonly Random random = new();
        private readonly FontWeight[] fontWeights = [
                FontWeights.Bold, FontWeights.Regular
            ];

        private readonly DispatcherTimer chatboxTimer;
        private readonly TranslationReportService _reportService;

        // Semaphore to ensure messages are processed sequentially
        private readonly SemaphoreSlim _translationSemaphore = new(1, 1);

#if DEBUG
        private const bool IsRetranslateUiEnabled = true;
#else
        private const bool IsRetranslateUiEnabled = false;
#endif

        public ChatWindowViewModel(TranslationReportService reportService)
        {
            _reportService = reportService;
            ChatDocument = new FlowDocument();
            Messenger.Register<PropertyChangedMessage<double>>(this, OnDoubleMessage);

            const int period = 250;
            chatboxTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(period)
            };
            chatboxTimer.Tick += UpdateChatbox;
            chatboxTimer.Start();
            Log.Debug($"New RefreshChatbox timer with period {period}ms");
        }

        private void OnDoubleMessage(object recipient, PropertyChangedMessage<double> message)
        {
            switch (message.PropertyName)
            {
                case nameof(SettingsViewModel.ChatWindowOpacity):
                    ChatWindowOpacity = message.NewValue;
                    break;
            }
        }

#pragma warning disable CS8602
        private void UpdateChatbox(object? sender, EventArgs e)
        {
            if (!_translationSemaphore.Wait(0))
            {
                // Preserve FIFO delivery: queued messages must not supersede the
                // translation already in progress.
                return;
            }

            if (!ChatQueue.q.TryTake(out var chat))
            {
                _translationSemaphore.Release();
                return;
            }

            _ = Task.Run(() => ProcessChatAsync(chat));
        }

        private async Task ProcessChatAsync(ChatLogItem chat)
        {
            try
            {
                Log.Information($"Dequeued {chat.Line}");

                if (!int.TryParse(chat.Code, System.Globalization.NumberStyles.HexNumber, null, out var intCode))
                {
                    Log.Warning("Failed to parse chat code: {Code}", chat.Code);
                    return;
                }

                ChatCode code = (ChatCode)intCode;
                var channel = IronworksSettings.Instance.ChannelSettings.ChatChannels
                    .FirstOrDefault(ch => ch.Code == code && ch.Show);
                if (channel == null)
                {
                    return;
                }

                var targetLanguage = IronworksSettings.Instance.TranslatorSettings.ClientLanguage;
                var markerDecode = chat.Bytes.DecodeAutoTranslateMarker(
                    channel.MajorLanguage,
                    targetLanguage);
                var sourceDecode = markerDecode.HasAutoTranslate
                    ? chat.Bytes.DecodeAutoTranslate(
                        channel.MajorLanguage,
                        channel.MajorLanguage,
                        targetLanguage,
                        channel.MajorLanguage)
                    : markerDecode;
                var targetDecode = markerDecode.HasAutoTranslate
                    ? chat.Bytes.DecodeAutoTranslate(
                        targetLanguage,
                        channel.MajorLanguage,
                        targetLanguage,
                        channel.MajorLanguage)
                    : markerDecode;

                ChatLogItem decodedChat = sourceDecode.DecodedChat;
                ChatLogItem targetChat = targetDecode.DecodedChat;
                var pureAutoTranslate = IsPureAutoTranslateMessage(markerDecode);

                if (IsEmoteMessage(code))
                {
                    var emoteBody = GetEmoteBody(chat, decodedChat);
                    var sourceLanguage = ResolveEmoteSourceLanguage(emoteBody, channel.MajorLanguage);
                    var text = pureAutoTranslate
                        ? CreateResolvedAutoTranslateText(
                            emoteBody,
                            GetEmoteBody(chat, targetChat),
                            sourceLanguage,
                            targetLanguage)
                        : await CreateTranslationTextAsync(
                            emoteBody,
                            sourceLanguage,
                            string.Empty,
                            MiLMMTTranslationKind.Chat,
                            CancellationToken.None);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddMessage(text, channel);
                        Diet();
                    });
                    return;
                }

                if (IsSystemMessage(code))
                {
                    var sourceLine = FirstNonEmpty(decodedChat.Line, chat.Line);
                    var targetLine = FirstNonEmpty(targetChat.Line, sourceLine);
                    var text = pureAutoTranslate
                        ? CreateResolvedAutoTranslateText(
                            sourceLine,
                            targetLine,
                            channel.MajorLanguage,
                            targetLanguage)
                        : await CreateTranslationTextAsync(
                            sourceLine,
                            channel.MajorLanguage,
                            string.Empty,
                            MiLMMTTranslationKind.Chat,
                            CancellationToken.None);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddMessage(text, channel);
                        Diet();
                    });
                    return;
                }

                var decodedLine = FirstNonEmpty(decodedChat.Line, chat.Line);
                var targetLineForChat = FirstNonEmpty(targetChat.Line, decodedLine);
                var author = FirstNonEmpty(GetAuthorFromLine(decodedLine), decodedChat.PlayerName);
                var sentence = GetBodyFromLine(decodedLine);
                var targetSentence = GetBodyFromLine(targetLineForChat);

                if (!IsDialogueMessage(code))
                {
                    var text = pureAutoTranslate
                        ? CreateResolvedAutoTranslateText(
                            sentence,
                            targetSentence,
                            channel.MajorLanguage,
                            targetLanguage,
                            author)
                        : await CreateTranslationTextAsync(
                            sentence,
                            channel.MajorLanguage,
                            author,
                            MiLMMTTranslationKind.Chat,
                            CancellationToken.None);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddMessage(text, channel, author);
                        Diet();
                    });
                    return;
                }

                if (IronworksSettings.Instance.TranslatorSettings.DialogueTranslationMethod
                    == DialogueTranslationMethod.ChatMessage)
                {
                    var text = pureAutoTranslate
                        ? CreateResolvedAutoTranslateText(
                            sentence,
                            targetSentence,
                            channel.MajorLanguage,
                            targetLanguage,
                            author)
                        : await CreateTranslationTextAsync(
                            sentence,
                            channel.MajorLanguage,
                            author,
                            MiLMMTTranslationKind.Dialogue,
                            CancellationToken.None);
                    text.Author = await TranslateDialogueSpeakerAsync(
                        text.Author,
                        channel.MajorLanguage,
                        CancellationToken.None);
                    DialogueEntry? dialogueEntry = CreateDialogueEntry(text);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        App.GetService<DialogueWindow>().PushDialogueTextBox(dialogueEntry, text);
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while processing chat message");
            }
            finally
            {
                _translationSemaphore.Release();
            }
        }

        internal static bool IsSystemMessage(ChatCode code)
        {
            return code == ChatCode.Recruitment ||
                   code == ChatCode.System ||
                   code == ChatCode.Error ||
                   code == ChatCode.Notice ||
                   code == ChatCode.Emote ||
                   code == ChatCode.MarketSold ||
                   code == ChatCode.Echo ||
                   code == ChatCode.GilReceive ||
                   code == ChatCode.Gather ||
                   code == ChatCode.FieldAttack;
        }

        internal static bool IsDialogueMessage(ChatCode code)
        {
            return code == ChatCode.NPCDialog ||
                   code == ChatCode.NPCAnnounce ||
                   code == ChatCode.BossQuotes;
        }

        internal static bool IsEmoteMessage(ChatCode code)
        {
            return code == ChatCode.Emote ||
                   code == ChatCode.EmoteCustom;
        }

        internal static bool IsPureAutoTranslateMessage(AutoTranslateDecodeResult markerDecode)
        {
            if (!markerDecode.HasAutoTranslate)
            {
                return false;
            }

            var lineBody = GetBodyFromLine(markerDecode.DecodedChat.Line);
            var body = string.IsNullOrWhiteSpace(GetAuthorFromLine(markerDecode.DecodedChat.Line))
                ? FirstNonEmpty(markerDecode.DecodedChat.Message, lineBody)
                : lineBody;
            foreach (var block in markerDecode.Blocks)
            {
                body = body.Replace(block.MarkerText, string.Empty, StringComparison.Ordinal);
            }

            return body.All(char.IsWhiteSpace);
        }

        internal static string GetEmoteBody(ChatLogItem chat, ChatLogItem decodedChat)
        {
            var author = GetAuthorFromLine(decodedChat.Line);
            var body = FirstNonEmpty(decodedChat.Message, chat.Message);

            if (string.IsNullOrWhiteSpace(body))
            {
                body = GetBodyFromLine(decodedChat.Line);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                body = GetBodyFromLine(chat.Line);
            }

            return StripAuthorPrefix(
                body,
                author,
                decodedChat.PlayerName,
                chat.PlayerName,
                decodedChat.PlayerCharacterName,
                chat.PlayerCharacterName);
        }

        internal static ClientLanguage ResolveEmoteSourceLanguage(
            string sentence,
            ClientLanguage configuredSourceLanguage)
        {
            if (configuredSourceLanguage != ClientLanguage.English &&
                sentence.HasEnglish() &&
                !sentence.HasJapanese() &&
                !sentence.HasKorean())
            {
                return ClientLanguage.English;
            }

            return configuredSourceLanguage;
        }

        internal static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim()
                ?? string.Empty;
        }

        internal static string GetAuthorFromLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            var index = line.IndexOf(':');
            return index > 0
                ? line[..index].Trim()
                : string.Empty;
        }

        internal static string GetBodyFromLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            var index = line.IndexOf(':');
            return index > 0
                ? line[(index + 1)..].Trim()
                : line.Trim();
        }

        internal static string StripAuthorPrefix(string value, params string?[] authors)
        {
            var result = value.Trim();
            foreach (var author in authors.Where(author => !string.IsNullOrWhiteSpace(author)).Distinct())
            {
                var trimmedAuthor = author!.Trim();
                if (!result.StartsWith(trimmedAuthor, StringComparison.Ordinal))
                {
                    continue;
                }

                var remainder = result[trimmedAuthor.Length..];
                if (string.IsNullOrWhiteSpace(remainder))
                {
                    return string.Empty;
                }

                if (remainder[0] == ':' || remainder[0] == '：')
                {
                    return remainder[1..].Trim();
                }

                if (char.IsWhiteSpace(remainder[0]))
                {
                    return remainder.Trim();
                }

                var firstSpace = remainder.IndexOf(' ');
                if (firstSpace >= 0)
                {
                    return remainder[(firstSpace + 1)..].Trim();
                }
            }

            return result;
        }

        private static async Task<TranslationText> CreateTranslationTextAsync(
            string originalText,
            ClientLanguage sourceLanguage,
            string author,
            MiLMMTTranslationKind requestKind,
            CancellationToken cancellationToken)
        {
            var text = new TranslationText(
                originalText,
                (TranslationLanguageCode)sourceLanguage,
                (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage)
            {
                Author = author,
                OriginalSpeaker = requestKind == MiLMMTTranslationKind.Dialogue ? author : null,
                TranslationKind = requestKind,
            };

            if (!ContainsNativeLanguage(originalText))
            {
                var engine = IronworksSettings.Instance.TranslatorSettings.TranslatorEngine;
                var modelProfile = engine == TranslatorEngine.MiLMMT
                    ? MiLMMTModelProfiles.GetEffectiveCurrent()
                    : null;
                text.TranslatedText = await TranslateAsync(
                    originalText,
                    sourceLanguage,
                    requestKind,
                    cancellationToken,
                    engine,
                    speakerName: requestKind == MiLMMTTranslationKind.Dialogue ? author : null);
                cancellationToken.ThrowIfCancellationRequested();
                PopulateTranslationMetadata(text, engine, modelProfile);
            }

            return text;
        }

        internal static void PopulateTranslationMetadata(
            TranslationText text,
            TranslatorEngine engine,
            MiLMMTModelProfile? modelProfile = null)
        {
            text.Engine = engine;
            if (engine == TranslatorEngine.MiLMMT)
            {
                var profile = modelProfile ?? MiLMMTModelProfiles.GetEffectiveCurrent();
                text.ModelId = profile.FileName;
                text.ModelSha256 = profile.Sha256;
            }
            else
            {
                text.ModelId = null;
                text.ModelSha256 = null;
            }
        }

        private static async Task<string> TranslateDialogueSpeakerAsync(
            string? speaker,
            ClientLanguage sourceLanguage,
            CancellationToken cancellationToken)
        {
            var targetLanguage = (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage;
            var glossaryService = App.GetService<MiLMMTGlossaryService>();
            return await DialogueSpeakerTranslation.TranslateOrOriginalAsync(
                speaker,
                (value, token) => TranslateAsync(
                    value,
                    sourceLanguage,
                    MiLMMTTranslationKind.Dialogue,
                    token),
                cancellationToken,
                ex => Log.Warning(
                    ex,
                    "Failed to translate dialogue speaker; using the original speaker."),
                value => glossaryService != null
                    && glossaryService.TryResolveNpcName(
                        (TranslationLanguageCode)sourceLanguage,
                        targetLanguage,
                        value,
                        out var officialName)
                        ? officialName
                        : null);
        }

        internal static TranslationText CreateResolvedAutoTranslateText(
            string originalText,
            string translatedText,
            ClientLanguage sourceLanguage,
            ClientLanguage targetLanguage,
            string author = "")
        {
            return new TranslationText(
                originalText,
                (TranslationLanguageCode)sourceLanguage,
                (TranslationLanguageCode)targetLanguage)
            {
                Author = author,
                TranslatedText = translatedText,
            };
        }

        internal static DialogueEntry? CreateDialogueEntry(TranslationText translation)
        {
            ArgumentNullException.ThrowIfNull(translation);

            return translation.TranslatedText is string translatedText
                ? new DialogueEntry(DialogueKind.ChatLog, translation.Author, translatedText)
                : null;
        }

        private static void InvokeOnUiThread(Action action)
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        public void AddDialogue(string dialogue, TranslationText? translationText = null)
        {
            InvokeOnUiThread(() =>
            {
                var settings = IronworksSettings.Instance;
                var paragraph = new Paragraph(new Run(dialogue))
                {
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.ChannelSettings.NpcDialog.Color)),
                    FontFamily = new FontFamily(settings.ChatUiSettings.Font),
                    FontSize = settings.ChatUiSettings.ChatboxFontSize,
                    Margin = new Thickness(0, 0, 0, settings.ChatUiSettings.ChatMargin)
                };

                if (translationText != null)
                {
                    var translationParagraph = new TranslationParagraph(paragraph, translationText);
                    var contextMenu = new ContextMenu();
                    var reportItem = new MenuItem
                    {
                        Header = Localizer.GetString("chat.report"),
                        Tag = translationParagraph,
                    };
                    reportItem.Click += Report_Click;
                    contextMenu.Items.Add(reportItem);
                    paragraph.ContextMenu = contextMenu;
                }

                ChatDocument.Blocks.Add(paragraph);
                Diet();
                ScrollToEnd();
            });
        }

        public void AddMessage(TranslationText text, ChatChannel channel, string author = "")
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => AddMessage(text, channel, author));
                return;
            }

            string translated = GenerateTranslationText(text, author);
            var settings = IronworksSettings.Instance;
            var paragraph = new Paragraph(new Run(translated))
            {
                Foreground = new SolidColorBrush
                {
                    Color = (Color)ColorConverter.ConvertFromString(channel.Color)
                },
                FontFamily = new FontFamily(settings.ChatUiSettings.Font),
                FontSize = settings.ChatUiSettings.ChatboxFontSize,
                Margin = new Thickness
                {
                    Bottom = settings.ChatUiSettings.ChatMargin,
                }
            };
            TranslationParagraph translationParagraph = new(paragraph, text);

            // Create and attach a custom context menu to the paragraph
            var contextMenu = new ContextMenu();

            var menuItemReport = new MenuItem
            {
                Header = Localizer.GetString("chat.report"),
                Tag = translationParagraph,
            };
            menuItemReport.Click += Report_Click;
            contextMenu.Items.Add(menuItemReport);

            var menuItemReTranslate = new MenuItem
            {
                Header = Localizer.GetString("chat.retranslate"),
                IsEnabled = IsRetranslateUiEnabled
            };
            var menuItemPapago = new MenuItem
            {
                Header = "Papago",
                Tag = translationParagraph
            };
            menuItemPapago.Click += PapagoRetranslate_Click;
            var menuItemDeepLAPI = new MenuItem
            {
                Header = "DeepL (API)",
                Tag = translationParagraph
            };
            menuItemDeepLAPI.Click += DeepLRetranslate_Click;
            var menuItemMiLMMT = new MenuItem
            {
                Header = "MiLMMT",
                Tag = translationParagraph
            };
            menuItemMiLMMT.Click += MiLMMTRetranslate_Click;

            menuItemReTranslate.Items.Add(menuItemPapago);
            menuItemReTranslate.Items.Add(menuItemDeepLAPI);
            menuItemReTranslate.Items.Add(menuItemMiLMMT);
            contextMenu.Items.Add(menuItemReTranslate);

            paragraph.ContextMenu = contextMenu;

            ChatDocument.Blocks.Add(paragraph);
            ScrollToEnd();
        }

        // from https://stackoverflow.com/a/44604957/4183595
        private static void ScrollToEnd()
        {
            InvokeOnUiThread(() =>
            {
                var fdsv = App.GetService<ChatWindow>().ChatPanel;
                ScrollViewer? sv = fdsv.Template.FindName("PART_ContentHost", fdsv) as ScrollViewer;
                sv?.ScrollToEnd();
            });
        }

        private static string GenerateTranslationText(TranslationText text, string author)
        {
            var result = new StringBuilder();
            if (author != null && !author.Equals(""))
            {
                result.Append($"{author}: ");
            }
            if (text.TranslatedText != null)
            {
                result.Append(text.TranslatedText);
            }
            else
            {
                result.Append(text.OriginalText);
            }
            return result.ToString();
        }

        public void AddRandomMessage(string message)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => AddRandomMessage(message));
                return;
            }

            var paragraph = new Paragraph(new Run(message))
            {
                Foreground = new SolidColorBrush(
                    Color.FromArgb(
                            (byte)200,
                            (byte)random.Next(0, 250),
                            (byte)random.Next(0, 250),
                            (byte)random.Next(0, 250)
                        )
                ),
                FontWeight = fontWeights[random.Next(0, fontWeights.Length)],
                FontFamily = new FontFamily(IronworksSettings.Instance.ChatUiSettings.Font),
                FontSize = IronworksSettings.Instance.ChatUiSettings.ChatboxFontSize
            };
            TranslationParagraph translationParagraph = new(paragraph,
                new TranslationText(message, TranslationLanguageCode.English, TranslationLanguageCode.Korean)
                {
                    TranslatedText = "안녕하세요!",
                }
            );

            // Create and attach a custom context menu to the paragraph
            var contextMenu = new ContextMenu();

            var menuItem = new MenuItem { Header = Localizer.GetString("chat.report") };
            menuItem.Click += Report_Click;
            // Store the Paragraph object in the Tag property of the MenuItem
            menuItem.Tag = translationParagraph;
            contextMenu.Items.Add(menuItem);

            var menuItemReTranslate = new MenuItem
            {
                Header = Localizer.GetString("chat.retranslate"),
                IsEnabled = IsRetranslateUiEnabled
            };
            var menuItemPapago = new MenuItem
            {
                Header = "Papago",
                Tag = translationParagraph
            };
            menuItemPapago.Click += PapagoRetranslate_Click;
            var menuItemDeepLAPI = new MenuItem
            {
                Header = "DeepL (API)",
                Tag = translationParagraph
            };
            menuItemDeepLAPI.Click += DeepLRetranslate_Click;
            var menuItemMiLMMT = new MenuItem
            {
                Header = "MiLMMT",
                Tag = translationParagraph
            };
            menuItemMiLMMT.Click += MiLMMTRetranslate_Click;

            menuItemReTranslate.Items.Add(menuItemPapago);
            menuItemReTranslate.Items.Add(menuItemDeepLAPI);
            menuItemReTranslate.Items.Add(menuItemMiLMMT);
            contextMenu.Items.Add(menuItemReTranslate);

            paragraph.ContextMenu = contextMenu;

            ChatDocument.Blocks.Add(paragraph);
            ScrollToEnd();
        }

        public void AddBatchTranslationMessage()
        {
            for (int i = 0; i < 10; i++)
            {
                string line = $"これはテストメッセージ{i}です。";
                string playerName = "Python Volca";
                string code = "000A";
                var item = new ChatLogItem
                {
                    TimeStamp = DateTime.Now,
                    PlayerName = playerName,
                    Code = code,
                    Bytes = Encoding.UTF8.GetBytes(line),
                    IsInternational = true,
                    Line = $"{playerName}:{line}",
                    PlayerCharacterName = "UNRESOLVED",
                    Message = line,
                    Combined = $"{code}:{playerName}:{line}",
                    Raw = line
                };
                ChatQueue.q.Add(item);
            }
        }

        private string ReTranslate(TranslationText tText, TranslatorEngine api)
        {
            var modelProfile = api == TranslatorEngine.MiLMMT
                ? MiLMMTModelProfiles.GetEffectiveCurrent()
                : null;
            string newMessage = Translate(tText.OriginalText, (ClientLanguage)tText.SourceLanguage, api);
            tText.TranslatedText = newMessage;
            PopulateTranslationMetadata(tText, api, modelProfile);
            string? translated = string.IsNullOrEmpty(tText.Author) ? newMessage : $"{tText.Author}: {newMessage}";
            return translated;
        }

        private void DeepLRetranslate_Click(object sender, RoutedEventArgs e)
        {
            if (((MenuItem)sender).Tag is TranslationParagraph tParagraph)
            {
                var tText = tParagraph.Text;
                var api = TranslatorEngine.DeepL_API;
                ReplaceTextInParagraph(tParagraph.Paragraph, ReTranslate(tText, api));
            }
        }

        private void PapagoRetranslate_Click(object sender, RoutedEventArgs e)
        {
            if (((MenuItem)sender).Tag is TranslationParagraph tParagraph)
            {
                var tText = tParagraph.Text;
                var api = TranslatorEngine.Papago;
                ReplaceTextInParagraph(tParagraph.Paragraph, ReTranslate(tText, api));
            }
        }

        private void MiLMMTRetranslate_Click(object sender, RoutedEventArgs e)
        {
            if (((MenuItem)sender).Tag is TranslationParagraph tParagraph)
            {
                var tText = tParagraph.Text;
                var api = TranslatorEngine.MiLMMT;
                ReplaceTextInParagraph(tParagraph.Paragraph, ReTranslate(tText, api));
            }
        }

        private static string Translate(string input, ClientLanguage channelLanguage, TranslatorEngine? translatorEngine = null)
        {
            Log.Information($"Translating {input}");
            string result = string.Empty;
            var switcher = translatorEngine ?? IronworksSettings.Instance.TranslatorSettings.TranslatorEngine;
            switch (switcher)
            {
                case TranslatorEngine.Papago:
                    result = App.GetService<PapagoTranslator>().Translate(
                            input,
                            (TranslationLanguageCode)channelLanguage,
                            (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage
                        );
                    break;
                case TranslatorEngine.DeepL_API:
                    result = App.GetService<DeepLAPITranslator>().Translate(
                            input,
                            (TranslationLanguageCode)channelLanguage,
                            (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage
                        );
                    break;
                case TranslatorEngine.MiLMMT:
                    result = App.GetService<MiLMMTTranslator>().Translate(
                            input,
                            (TranslationLanguageCode)channelLanguage,
                            (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage
                        );
                    break;
                default:
                    Log.Warning("Unknown translator engine {TranslatorEngine}. Returning original input.", switcher);
                    result = input;
                    break;
            }
            Log.Information($"Translated {input}");
            return result;
        }

        private static async Task<string> TranslateAsync(
            string input,
            ClientLanguage channelLanguage,
            MiLMMTTranslationKind requestKind,
            CancellationToken cancellationToken,
            TranslatorEngine? translatorEngine = null,
            string? speakerName = null)
        {
            Log.Information("Translating {Input}", input);
            var switcher = translatorEngine ?? IronworksSettings.Instance.TranslatorSettings.TranslatorEngine;
            var targetLanguage = (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage;
            var sourceLanguage = (TranslationLanguageCode)channelLanguage;
            var result = switcher switch
            {
                TranslatorEngine.Papago => await App.GetService<PapagoTranslator>().TranslateAsync(
                    input,
                    sourceLanguage,
                    targetLanguage),
                TranslatorEngine.DeepL_API => await App.GetService<DeepLAPITranslator>().TranslateAsync(
                    input,
                    sourceLanguage,
                    targetLanguage),
                TranslatorEngine.MiLMMT => await App.GetService<MiLMMTTranslator>().TranslateAsync(
                    input,
                    sourceLanguage,
                    targetLanguage,
                    requestKind,
                    cancellationToken,
                    speakerName),
                _ => input,
            };

            if (!Enum.IsDefined(switcher))
            {
                Log.Warning("Unknown translator engine {TranslatorEngine}. Returning original input.", switcher);
            }

            Log.Information("Translated {Input}", input);
            return result;
        }

        private static bool ContainsNativeLanguage(string sentence)
        {
            return IronworksSettings.Instance.TranslatorSettings.ClientLanguage switch
            {
                ClientLanguage.Japanese => sentence.HasJapanese(),
                ClientLanguage.Korean => sentence.HasKorean(),
                ClientLanguage.English => sentence.HasEnglish(),
                _ => false,
            };
        }

        public void ChangeChatFontSize(int fontSize)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ChangeChatFontSize(fontSize));
                return;
            }

            ChatDocument.FontSize = fontSize;

            // Traverse all elements in the FlowDocument
            foreach (var block in ChatDocument.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    paragraph.FontSize = fontSize;
                }
            }
        }

        public void ChangeChatMargin(double margin)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ChangeChatMargin(margin));
                return;
            }

            foreach (var block in ChatDocument.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    paragraph.Margin = new Thickness
                    {
                        Bottom = margin
                    };
                }
            }
        }

        public void ChangeChatFontFamily(string fontFamily)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ChangeChatFontFamily(fontFamily));
                return;
            }

            ChatDocument.FontFamily = new FontFamily(fontFamily);

            // Traverse all elements in the FlowDocument
            foreach (var block in ChatDocument.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    paragraph.FontFamily = new FontFamily(fontFamily);
                }
            }
        }

        public void Diet()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(Diet);
                return;
            }

            if (ChatDocument.Blocks.Count < 500) return;
            Log.Information("Executing diet");
            var blocks = ChatDocument.Blocks.Take(50).ToFrozenSet();
            foreach (var item in blocks)
            {
                ChatDocument.Blocks.Remove(item);
            }
        }

        private static void ReplaceTextInParagraph(Paragraph paragraph, string newText)
        {
            // Clear existing inlines
            paragraph.Inlines.Clear();

            // Add new text
            paragraph.Inlines.Add(new Run(newText));
        }

        private async void Report_Click(object sender, RoutedEventArgs e)
        {
            await DoReport(sender);
        }

        [TraceMethod]
        private async Task DoReport(object sender)
        {
            if (((MenuItem)sender).Tag is TranslationParagraph tParagraph)
            {
                await _reportService.ShowReportDialogAsync(tParagraph.Text);
            }
        }

        private static string GetTextFromParagraph(Paragraph paragraph)
        {
            var sb = new StringBuilder();
            foreach (Inline inline in paragraph.Inlines)
            {
                if (inline is Run run)
                {
                    sb.Append(run.Text);
                }
                else if (inline is Span span)
                {
                    foreach (Inline childInline in span.Inlines)
                    {
                        if (childInline is Run childRun)
                        {
                            sb.Append(childRun.Text);
                        }
                    }
                }
                // Handle other types of Inline elements if necessary
            }
            return sb.ToString();
        }
    }
#pragma warning restore CS8602
}
