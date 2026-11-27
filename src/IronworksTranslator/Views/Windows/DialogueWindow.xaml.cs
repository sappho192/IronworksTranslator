using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Services;
using IronworksTranslator.ViewModels.Windows;
using Serilog;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WpfScreenHelper;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.Utils.UI;

namespace IronworksTranslator.Views.Windows
{
    /// <summary>
    /// DialogueWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DialogueWindow : Window
    {
        public DialogueWindowViewModel ViewModel { get; }

        private readonly DispatcherTimer _resizeEndTimer = new();
        private readonly DispatcherTimer _repositionEndTimer = new();

        private readonly Timer chatboxTimer;
        private const int period = 500;
        private const int MaxDialogueTextLength = 30000;
        private static readonly Regex regexItem = MyRegex();
        private readonly bool _isUiInitialized = false;

        // Semaphore to ensure dialogue messages are processed sequentially
        private readonly SemaphoreSlim _translationSemaphore = new(1, 1);
        private readonly List<DialogueSpan> _dialogueSpans = [];
        private TranslationText? _contextMenuTargetDialogue;

        private readonly record struct DialogueSpan(int StartOffset, int EndOffset, TranslationText Text);

        public DialogueWindow(DialogueWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            TranslationWindowAltTab.Attach(this, IronworksSettings.Instance!.UiSettings!);

#pragma warning disable CS8602
            if (IronworksSettings.Instance.UiSettings.DialogueWindowVisible && !IronworksSettings.Instance.ChatUiSettings.ShowDialogueInChat)
            {
                TranslationWindowVisibility.SetVisible(this, true);
            }
            else
            {
                TranslationWindowVisibility.SetVisible(this, false);
            }
            if (IronworksSettings.Instance.ChatUiSettings.IsResizable)
            {
                ResizeMode = ResizeMode.CanResizeWithGrip;
            } else
            {
                ResizeMode = ResizeMode.NoResize;
            }
            ChangeDialogueFontSize(IronworksSettings.Instance.ChatUiSettings.DialogueFontSize);

            _resizeEndTimer.Interval = TimeSpan.FromMilliseconds(3000);
            _resizeEndTimer.Tick += ResizeEndTimer_Tick;
            _repositionEndTimer.Interval = TimeSpan.FromMilliseconds(3000);
            _repositionEndTimer.Tick += RepositionEndTimer_Tick;

            IronworksSettings.Instance.ChatUiSettings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ChatUISettings.ShowDialogueInChat))
                {
                    Dispatcher.Invoke(() =>
                    {
                        TranslationWindowVisibility.SetVisible(this,
                            !IronworksSettings.Instance.ChatUiSettings.ShowDialogueInChat &&
                            IronworksSettings.Instance.UiSettings.DialogueWindowVisible);
                    });
                }
            };
            _isUiInitialized = true;
            chatboxTimer = new Timer(RefreshDialogueTextBox, null, 0, period);
            Log.Debug($"New RefreshChatbox timer with period {period}ms");
        }

        public void ChangeDialogueFontSize(int fontSize)
        {
            tbDialogueTextBox.FontSize = fontSize;
        }

        private void RefreshDialogueTextBox(object? state)
        {
            // Skip if previous dialogue is still being processed
            // This ensures dialogues are displayed in the order they were received
            if (!_translationSemaphore.Wait(0))
            {
                return; // Another translation is in progress, skip this cycle
            }

            try
            {
                if (ChatQueue.TryDequeueDialogue(out DialogueEntry? entry))
                {
                    if (IronworksSettings.Instance.TranslatorSettings.DialogueTranslationMethod == DialogueTranslationMethod.MemorySearch)
                    {
                        var msg = entry.Text;
                        msg = MyRegex1().Replace(msg, "[HQ]");
                        msg = MyRegex2().Replace(msg, "⇒");
                        msg = MyRegex3().Replace(msg, string.Empty);
                        msg = MyRegex4().Replace(msg, string.Empty);
                        if (msg.StartsWith('\u0002'))
                        {
                            var filter = regexItem.Match(msg);
                            if (filter.Success)
                            {
                                msg = filter.Groups[1].Value;
                            }
                        }
                        if (!msg.Equals(string.Empty))
                        {
                            var sourceLanguage =
                                IronworksSettings.Instance.ChannelSettings.NpcDialog.MajorLanguage;
                            var engine = IronworksSettings.Instance.TranslatorSettings.TranslatorEngine;
                            var modelProfile = engine == TranslatorEngine.MiLMMT
                                ? MiLMMTModelProfiles.GetEffectiveCurrent()
                                : null;
                            var targetLanguage =
                                (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage;
                            var glossaryService = App.GetService<MiLMMTGlossaryService>();
                            var translated = Translate(msg, sourceLanguage, engine);
                            var translatedSpeaker = DialogueSpeakerTranslation.TranslateOrOriginal(
                                entry.Speaker,
                                speaker => Translate(speaker, sourceLanguage, engine),
                                ex => Log.Warning(
                                    ex,
                                    "Failed to translate dialogue speaker; using the original speaker."),
                                speaker => glossaryService != null
                                    && glossaryService.TryResolveNpcName(
                                        (TranslationLanguageCode)sourceLanguage,
                                        targetLanguage,
                                        speaker,
                                        out var officialName)
                                        ? officialName
                                        : null);
                            var displayText = DialogueTextFormatter.Format(
                                translatedSpeaker,
                                translated);

                            var dialogueText = new TranslationText(
                                msg,
                                (TranslationLanguageCode)sourceLanguage,
                                (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage)
                            {
                                TranslatedText = translated,
                                Author = translatedSpeaker,
                                OriginalSpeaker = entry.Speaker,
                                TranslationKind = MiLMMTTranslationKind.Dialogue,
                            };
                            ChatWindowViewModel.PopulateTranslationMetadata(dialogueText, engine, modelProfile);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                AppendDialogueText(displayText, dialogueText);
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in RefreshDialogueTextBox timer callback");
            }
            finally
            {
                // Always release the semaphore so next dialogue can be processed
                _translationSemaphore.Release();
            }
        }

        public void PushDialogueTextBox(DialogueEntry? entry, TranslationText? translationText = null)
        {
            if (entry == null) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => PushDialogueTextBox(entry, translationText));
                return;
            }

            AppendDialogueText(DialogueTextFormatter.Format(entry.Speaker, entry.Text), translationText);
        }

        private void AppendDialogueText(string dialogue, TranslationText? translationText = null)
        {
            if (IronworksSettings.Instance.ChatUiSettings.ShowDialogueInChat)
            {
                App.GetService<ChatWindowViewModel>().AddDialogue(dialogue, translationText);
                return;
            }

            var separator = $"{Environment.NewLine}{Environment.NewLine}";
            var startOffset = tbDialogueTextBox.Text.Length + separator.Length;
            tbDialogueTextBox.AppendText($"{separator}{dialogue}");
            if (translationText != null)
            {
                _dialogueSpans.Add(new DialogueSpan(startOffset, startOffset + dialogue.Length, translationText));
            }

            TrimDialogueTextBox();
            tbDialogueTextBox.ScrollToEnd();
        }

        private void DialogueTextBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var charIndex = e.CursorLeft < 0 || e.CursorTop < 0
                ? tbDialogueTextBox.CaretIndex
                : tbDialogueTextBox.GetCharacterIndexFromPoint(Mouse.GetPosition(tbDialogueTextBox), snapToText: true);
            _contextMenuTargetDialogue = ResolveDialogueAtIndex(charIndex);
            if (_contextMenuTargetDialogue == null)
            {
                e.Handled = true;
            }
        }

        private TranslationText? ResolveDialogueAtIndex(int charIndex)
        {
            if (_dialogueSpans.Count == 0)
            {
                return null;
            }

            if (charIndex >= 0)
            {
                for (var i = _dialogueSpans.Count - 1; i >= 0; i--)
                {
                    var span = _dialogueSpans[i];
                    if (charIndex >= span.StartOffset && charIndex <= span.EndOffset)
                    {
                        return span.Text;
                    }
                }
            }

            return _dialogueSpans[^1].Text;
        }

        private async void ReportDialogue_Click(object sender, RoutedEventArgs e)
        {
            if (_contextMenuTargetDialogue == null)
            {
                return;
            }

            await App.GetService<TranslationReportService>().ShowReportDialogAsync(_contextMenuTargetDialogue);
        }

        private void TrimDialogueTextBox()
        {
            var text = tbDialogueTextBox.Text;
            if (text.Length <= MaxDialogueTextLength)
            {
                return;
            }

            var trimStart = text.Length - MaxDialogueTextLength;
            var separator = $"{Environment.NewLine}{Environment.NewLine}";
            var nextSeparator = text.IndexOf(separator, trimStart, StringComparison.Ordinal);
            var removedLength = nextSeparator >= 0
                ? nextSeparator + separator.Length
                : trimStart;
            tbDialogueTextBox.Text = text[removedLength..];
            tbDialogueTextBox.CaretIndex = tbDialogueTextBox.Text.Length;

            if (_dialogueSpans.Count > 0)
            {
                var updatedSpans = new List<DialogueSpan>(_dialogueSpans.Count);
                foreach (var span in _dialogueSpans)
                {
                    if (span.EndOffset <= removedLength)
                    {
                        continue;
                    }

                    updatedSpans.Add(new DialogueSpan(
                        Math.Max(0, span.StartOffset - removedLength),
                        span.EndOffset - removedLength,
                        span.Text));
                }

                _dialogueSpans.Clear();
                _dialogueSpans.AddRange(updatedSpans);
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
                case TranslatorEngine.MiLMMT:
                    result = App.GetService<MiLMMTTranslator>().Translate(
                            input,
                            (TranslationLanguageCode)channelLanguage,
                            (TranslationLanguageCode)IronworksSettings.Instance.TranslatorSettings.ClientLanguage,
                            MiLMMTTranslationKind.Dialogue
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

        private void RepositionEndTimer_Tick(object? sender, EventArgs e)
        {
            _repositionEndTimer.Stop();

            if (_isUiInitialized)
            {
                var top = mainWindow.Top;
                var left = mainWindow.Left;

                if (WindowChecker.IsMinimized(top, left) || WindowChecker.IsMaximized(top, left))
                {
                    return;
                }

                if (mainWindow.WindowState.Equals(WindowState.Normal))
                {
                    IronworksSettings.Instance.UiSettings.DialogueWindowTop = top;
                    IronworksSettings.Instance.UiSettings.DialogueWindowLeft = left;
                    IronworksSettings.Instance.UiSettings.DialogueWindowScreen = Screen.FromWindow(mainWindow).DeviceName;
                }
            }
        }

        private void ResizeEndTimer_Tick(object? sender, EventArgs e)
        {
            _resizeEndTimer.Stop();

            IronworksSettings.Instance.UiSettings.DialogueWindowWidth = Width;
            IronworksSettings.Instance.UiSettings.DialogueWindowHeight = Height;
        }
#pragma warning restore CS8602

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            TranslationWindowVisibility.SetVisible(this, false);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _resizeEndTimer.Stop();
            _resizeEndTimer.Start();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            _repositionEndTimer.Stop();
            _repositionEndTimer.Start();
        }

#pragma warning disable CS8602
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            double windowTop = IronworksSettings.Instance.UiSettings.DialogueWindowTop;
            double windowLeft = IronworksSettings.Instance.UiSettings.DialogueWindowLeft;
            string? deviceName = IronworksSettings.Instance.UiSettings.DialogueWindowScreen;
            if (windowTop != 0 && windowLeft != 0)
            {
                var mainScreen = Screen.AllScreens.Where(screen => screen.DeviceName.Equals(deviceName)).FirstOrDefault();
                mainScreen ??= Screen.PrimaryScreen;

                WindowHelper.SetWindowPosition(mainWindow, WpfScreenHelper.Enum.WindowPositions.Center, mainScreen);
                mainWindow.Left = windowLeft;
                mainWindow.Top = windowTop;
            }
        }

        [GeneratedRegex(@"&\u0003(.*)\u0002I\u0002")]
        private static partial Regex MyRegex();
        [GeneratedRegex(@"\uE03C")]
        private static partial Regex MyRegex1();
        [GeneratedRegex(@"\uE06F")]
        private static partial Regex MyRegex2();
        [GeneratedRegex(@"\uE0BB")]
        private static partial Regex MyRegex3();
        [GeneratedRegex(@"\uFFFD")]
        private static partial Regex MyRegex4();
#pragma warning restore CS8602
    }
}
