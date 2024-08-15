using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils.Translator;
using Sharlayan.Core;
using System.Collections.Frozen;
using System.Collections.Specialized;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace IronworksTranslator.ViewModels.Windows
{
    public partial class ChatWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isDraggable = true;
        public FlowDocument ChatDocument { get; }

        private readonly Random random = new();
        private readonly FontWeight[] fontWeights = [
                FontWeights.Bold, FontWeights.Regular
            ];

        public ChatWindowViewModel()
        {
            ChatDocument = new FlowDocument();
            ChatQueue.ChatLogItems.CollectionChanged += ChatLogItems_CollectionChanged;
        }

#pragma warning disable CS8602
        private void ChatLogItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ChatQueue.oq.TryDequeue(out ChatLogItem? chat);
            if (chat == null) return;

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
                    if (!ContainsNativeLanguage(decodedChat.Line))
                    {
                        TranslationText text = new(line, 
                            (TranslationLanguageCode)channel.MajorLanguage,
                            (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddMessage(text, channel);
                        });
                    }
                }
                else
                {
                    var author = decodedChat.Line.RemoveAfter(":");
                    var sentence = decodedChat.Line.RemoveBefore(":");
                    var translated = Translate(sentence, channel.MajorLanguage);

                    if (!(code.Equals(ChatCode.NPCDialog) || code.Equals(ChatCode.NPCAnnounce) || code.Equals(ChatCode.BossQuotes)))
                    {// Push to ChatWindow
                        TranslationText text = new(sentence, 
                            (TranslationLanguageCode)channel.MajorLanguage, 
                            (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage)
                        {
                            TranslatedText = translated,
                            Author = author,
                        };
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
                            TranslationText text = new(sentence,
                            (TranslationLanguageCode)channel.MajorLanguage,
                            (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage)
                            {
                                TranslatedText = translated,
                                Author = author,
                            };
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddMessage(text, channel, author);
                            });
                        }
                    }
                }
            }
        }

        public void AddMessage(TranslationText text, ChatChannel channel, string author = "")
        {
            string? translated = author == "" ? text.TranslatedText : $"{author}: {text.TranslatedText}";
            var paragraph = new Paragraph(new Run(translated))
            {
                Foreground = new SolidColorBrush
                {
                    Color = (Color)ColorConverter.ConvertFromString(channel.Color)
                },
                FontFamily = new FontFamily(IronworksSettings.Instance.ChatUiSettings.Font),
                FontSize = IronworksSettings.Instance.ChatUiSettings.ChatboxFontSize
            };
            TranslationParagraph translationParagraph = new(paragraph, text);

            // Create and attach a custom context menu to the paragraph
            var contextMenu = new ContextMenu();

            var menuItem = new MenuItem { Header = "Custom Action" };
            menuItem.Click += MenuItem_Click;
            // Store the Paragraph object in the Tag property of the MenuItem
            menuItem.Tag = translationParagraph;
            contextMenu.Items.Add(menuItem);

            var menuItemReplace = new MenuItem { Header = "Replace" };
            menuItemReplace.Click += MenuItemReplace_Click;
            menuItemReplace.Tag = translationParagraph;
            contextMenu.Items.Add(menuItemReplace);

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

            var menuItem = new MenuItem { Header = "Custom Action" };
            menuItem.Click += MenuItem_Click;
            // Store the Paragraph object in the Tag property of the MenuItem
            menuItem.Tag = translationParagraph;
            contextMenu.Items.Add(menuItem);

            var menuItemReplace = new MenuItem { Header = "Replace" };
            menuItemReplace.Click += MenuItemReplace_Click;
            menuItemReplace.Tag = translationParagraph;
            contextMenu.Items.Add(menuItemReplace);

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
        }

        private static string ReTranslate(TranslationText tText, TranslatorEngine api)
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

        private static string Translate(string input, ClientLanguage channelLanguage, TranslatorEngine? translatorEngine = null)
        {
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
            return result;
        }

        private bool ContainsNativeLanguage(string sentence)
        {
            switch (IronworksSettings.Instance.TranslatorSettings.ClientLanguage)
            {
                case ClientLanguage.Japanese:
                    return sentence.HasJapanese();
                case ClientLanguage.English:
                    return sentence.HasEnglish();
                case ClientLanguage.Korean:
                    return sentence.HasKorean();
                case ClientLanguage.German:
                case ClientLanguage.French:
                default:
                    return false;
            }
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

        private void MenuItem_Click(object sender, RoutedEventArgs e)
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
