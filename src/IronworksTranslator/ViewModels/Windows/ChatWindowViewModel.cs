using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.Views.Windows;
using Serilog;
using Sharlayan.Core;
using System.Collections.Frozen;
using System.Collections.Specialized;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace IronworksTranslator.ViewModels.Windows
{
    public partial class ChatWindowViewModel : ObservableRecipient
    {
#pragma warning disable CS8602
        [ObservableProperty]
        private bool _isDraggable = IronworksSettings.Instance.ChatUiSettings.IsDraggable;
        [ObservableProperty]
        private double _chatWindowOpacity = IronworksSettings.Instance.ChatUiSettings.WindowOpacity;
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

        public ChatWindowViewModel()
        {
            ChatDocument = new FlowDocument();
            ChatQueue.ChatLogItems.CollectionChanged += ChatLogItems_CollectionChanged;
            Messenger.Register<PropertyChangedMessage<double>>(this, OnDoubleMessage);
        }

        private void OnDoubleMessage(object recipient, PropertyChangedMessage<double> message)
        {
            switch (message.PropertyName)
            {
                case nameof(SettingsViewModel.ChildWindowOpacity):
                    ChatWindowOpacity = message.NewValue;
                    break;
            }
        }

#pragma warning disable CS8602
        private void ChatLogItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            lock (ChatQueue.oq)
            {
                ChatQueue.oq.TryDequeue(out ChatLogItem? chat);
                if (chat == null) return;
                Log.Information($"Dequeued {chat.Line}");

                int.TryParse(chat.Code, System.Globalization.NumberStyles.HexNumber, null, out var intCode);
                ChatCode code = (ChatCode)intCode;
                if (IronworksSettings.Instance.ChannelSettings.ChatChannels.Where(ch => ch.Code == code && ch.Show).Any())
                {
                    ChatChannel channel = IronworksSettings.Instance.ChannelSettings.ChatChannels.Where(ch => ch.Code == code).First();
                    string line = chat.Line;
                    ChatLogItem decodedChat = chat.Bytes.DecodeAutoTranslate();

                    if (code == ChatCode.Recruitment || code == ChatCode.System || code == ChatCode.Error ||
                        code == ChatCode.Notice || code == ChatCode.Emote || code == ChatCode.MarketSold)
                    {
                        TranslationText text;
                        if (ContainsNativeLanguage(decodedChat.Line))
                        {// Skip translation task
                            text = new(line,
                                (TranslationLanguageCode)channel.MajorLanguage,
                                (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage);
                        }
                        else
                        {
                            var translated = Translate(line, channel.MajorLanguage);
                            text = new(line,
                                (TranslationLanguageCode)channel.MajorLanguage,
                                (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage)
                            {
                                TranslatedText = translated
                            };
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddMessage(text, channel);
                        });
                    }
                    else
                    {
                        var author = decodedChat.Line.RemoveAfter(":");
                        var sentence = decodedChat.Line.RemoveBefore(":");

                        if (!(code.Equals(ChatCode.NPCDialog) || code.Equals(ChatCode.NPCAnnounce) || code.Equals(ChatCode.BossQuotes)))
                        {// Push to ChatWindow
                            TranslationText text;
                            if (ContainsNativeLanguage(sentence))
                            {// Skip translation
                                text = new(sentence,
                                    (TranslationLanguageCode)channel.MajorLanguage,
                                    (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage);
                            }
                            else
                            {
                                var translated = Translate(sentence, channel.MajorLanguage);
                                text = new(sentence,
                                    (TranslationLanguageCode)channel.MajorLanguage,
                                    (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage)
                                {
                                    TranslatedText = translated,
                                    Author = author,
                                };
                            }

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddMessage(text, channel, author);
                            });
                        }
                        else
                        {// Push to DialogueWindow
                            if (IronworksSettings.Instance.TranslatorSettings.DialogueTranslationMethod
                                == DialogueTranslationMethod.ChatMessage)
                            {
                                TranslationText text;
                                if (ContainsNativeLanguage(sentence))
                                {
                                    text = new(sentence,
                                        (TranslationLanguageCode)channel.MajorLanguage,
                                        (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage);
                                }
                                else
                                {
                                    var translated = Translate(sentence, channel.MajorLanguage);
                                    text = new(sentence,
                                    (TranslationLanguageCode)channel.MajorLanguage,
                                    (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage)
                                    {
                                        TranslatedText = translated,
                                        Author = author,
                                    };
                                }
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    AddMessage(text, channel, author);
                                });
                            }
                        }
                    }
                }
            }
        }

        public void AddMessage(TranslationText text, ChatChannel channel, string author = "")
        {
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

            var menuItemReTranslate = new MenuItem { Header = "Re-Translate" };
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

            menuItemReTranslate.Items.Add(menuItemPapago);
            menuItemReTranslate.Items.Add(menuItemDeepLAPI);
            contextMenu.Items.Add(menuItemReTranslate);

            paragraph.ContextMenu = contextMenu;

            ChatDocument.Blocks.Add(paragraph);
            ScrollToEnd();
        }

        // from https://stackoverflow.com/a/44604957/4183595
        private static void ScrollToEnd()
        {
            var fdsv = App.GetService<ChatWindow>().ChatPanel;
            ScrollViewer? sv = fdsv.Template.FindName("PART_ContentHost", fdsv) as ScrollViewer;
            sv?.ScrollToEnd();
        }

        private static string GenerateTranslationText(TranslationText text, string author)
        {
            var result = new StringBuilder();
            if (author != null || author.Equals(""))
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

            var menuItemReTranslate = new MenuItem { Header = Localizer.GetString("chat.retranslate") };
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

            menuItemReTranslate.Items.Add(menuItemPapago);
            menuItemReTranslate.Items.Add(menuItemDeepLAPI);
            contextMenu.Items.Add(menuItemReTranslate);

            paragraph.ContextMenu = contextMenu;

            ChatDocument.Blocks.Add(paragraph);
            ScrollToEnd();
        }

        private string ReTranslate(TranslationText tText, TranslatorEngine api)
        {
            string newMessage = Translate(tText.OriginalText, (ClientLanguage)tText.SourceLanguage, api);
            string? translated = tText.Author == "" ? newMessage : $"{tText.Author}: {newMessage}";
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

        private string Translate(string input, ClientLanguage channelLanguage, TranslatorEngine? translatorEngine = null)
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
                case TranslatorEngine.JESC_Ja_Ko:
                    result = input;
                    break;
                default:
                    break;
            }
            Log.Information($"Translated {input}");
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
            // Traverse all elements in the FlowDocument
            foreach (var block in ChatDocument.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    paragraph.FontFamily = new FontFamily(fontFamily);
                }
            }
        }

        private static int blockIndex = 0;
        public void Diet()
        {
            var blocks = ChatDocument.Blocks.Take(1).ToFrozenSet();
            foreach (var item in blocks)
            {
                ChatDocument.Blocks.Remove(item);
            }

            AddRandomMessage($"Lorem ipsum dolor sit amet, consectetur adipiscing elit. ({blockIndex++})");
        }

        private static int count = 1;
        private void MenuItemReplace_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve the Paragraph object from the Tag property of the MenuItem
            if (((MenuItem)sender).Tag is TranslationParagraph tParagraph)
            {
                string newMessage = $"replaced! ({count++}), raw: {tParagraph.Text.OriginalText}";
                ReplaceTextInParagraph(tParagraph.Paragraph, newMessage);
                //MessageBox.Show($"Custom action triggered for paragraph: {newMessage}");
            }
        }

        private static void ReplaceTextInParagraph(Paragraph paragraph, string newText)
        {
            // Clear existing inlines
            paragraph.Inlines.Clear();

            // Add new text
            paragraph.Inlines.Add(new Run(newText));
        }

        private void Report_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve the Paragraph object from the Tag property of the MenuItem
            if (((MenuItem)sender).Tag is TranslationParagraph tParagraph)
            {
                string paragraphText = GetTextFromParagraph(tParagraph.Paragraph);
                MessageBox.Show($"Custom action triggered for paragraph: {paragraphText}");
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
