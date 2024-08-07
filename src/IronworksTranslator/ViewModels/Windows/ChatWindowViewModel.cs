using IronworksTranslator.Helpers.Extensions;
using IronworksTranslator.Models;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using Sharlayan.Core;
using System;
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
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddMessage(decodedChat.Line, channel);
                        });
                    }
                }
                else
                {
                    var author = decodedChat.Line.RemoveAfter(":");
                    var sentence = decodedChat.Line.RemoveBefore(":");

                    if (!(code.Equals(ChatCode.NPCDialog) || code.Equals(ChatCode.NPCAnnounce) || code.Equals(ChatCode.BossQuotes)))
                    {// Push to ChatWindow
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddMessage($"{author}: {sentence}", channel);
                        });
                    }
                    else
                    {// Push to DialogueWindow
                        if (IronworksSettings.Instance.TranslatorSettings.DialogueTranslationMethod == DialogueTranslationMethod.ChatMessage)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddMessage($"{author}: {sentence}", channel);
                            });
                        }
                    }
                }
            }
        }
#pragma warning restore CS8602

        public void AddMessage(string message, ChatChannel channel)
        {
            var paragraph = new Paragraph(new Run(message))
            {
                Foreground = new SolidColorBrush
                {
                    Color = (Color)ColorConverter.ConvertFromString(channel.Color)
                },
                FontFamily = new FontFamily(IronworksSettings.Instance.ChatUiSettings.Font),
                FontSize = IronworksSettings.Instance.ChatUiSettings.ChatboxFontSize
            };

            // Create and attach a custom context menu to the paragraph
            var contextMenu = new ContextMenu();

            var menuItem = new MenuItem { Header = "Custom Action" };
            menuItem.Click += MenuItem_Click;
            // Store the Paragraph object in the Tag property of the MenuItem
            menuItem.Tag = paragraph;
            contextMenu.Items.Add(menuItem);

            var menuItemReplace = new MenuItem { Header = "Replace" };
            menuItemReplace.Click += MenuItemReplace_Click;
            menuItemReplace.Tag = paragraph;
            contextMenu.Items.Add(menuItemReplace);

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

            // Create and attach a custom context menu to the paragraph
            var contextMenu = new ContextMenu();

            var menuItem = new MenuItem { Header = "Custom Action" };
            menuItem.Click += MenuItem_Click;
            // Store the Paragraph object in the Tag property of the MenuItem
            menuItem.Tag = paragraph;
            contextMenu.Items.Add(menuItem);

            var menuItemReplace = new MenuItem { Header = "Replace" };
            menuItemReplace.Click += MenuItemReplace_Click;
            menuItemReplace.Tag = paragraph;
            contextMenu.Items.Add(menuItemReplace);

            paragraph.ContextMenu = contextMenu;

            ChatDocument.Blocks.Add(paragraph);
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
            if (((MenuItem)sender).Tag is Paragraph paragraph)
            {
                string newMessage = $"replaced! ({count++})";
                ReplaceTextInParagraph(paragraph, newMessage);
                System.Windows.MessageBox.Show($"Custom action triggered for paragraph: {newMessage}");
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
            if (((MenuItem)sender).Tag is Paragraph paragraph)
            {
                string paragraphText = GetTextFromParagraph(paragraph);
                System.Windows.MessageBox.Show($"Custom action triggered for paragraph: {paragraphText}");
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
}
