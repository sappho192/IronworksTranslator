using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.ViewModels.Dialogs;
using IronworksTranslator.Views.Dialogs;
using Lepo.i18n.DependencyInjection;
using Lepo.i18n.Yaml;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace IronworksTranslator.Tests.Views;

// Opt-in because it creates a real WPF window hosting the report ContentDialog.
public class TranslationReportDialogDesktopTests
{
    public static bool DesktopSmokeEnabled =>
        Environment.GetEnvironmentVariable("IRONWORKS_WINDOW_SMOKE") == "1";

    [Fact(Skip = "Set IRONWORKS_WINDOW_SMOKE=1 on an interactive Windows desktop.", SkipUnless = nameof(DesktopSmokeEnabled))]
    public void ReportDialog_HasSingleScrollAreaThatWheelScrollsFromAnyControl()
    {
        RunSta(() =>
        {
            EnsureLocalizer();
            var longText = string.Join(" ", Enumerable.Repeat("The Scions of the Seventh Dawn gather once more.", 12));
            var text = new TranslationText(longText, TranslationLanguageCode.English, TranslationLanguageCode.Korean)
            {
                TranslatedText = string.Join(" ", Enumerable.Repeat("새벽의 혈맹이 다시 모였다.", 12)),
                Engine = TranslatorEngine.Papago,
                TranslationKind = MiLMMTTranslationKind.Chat,
            };
            var viewModel = new TranslationReportViewModel(
                text,
                (_, input, _, _, _) => Task.FromResult(input),
                isDeepLAvailable: false,
                localize: key => key);
            var content = new TranslationReportDialogContent(viewModel);

            var host = new ContentPresenter();
            var window = new Window
            {
                Title = "Ironworks window smoke: report dialog scroll",
                Width = 720,
                Height = 520,
                ShowActivated = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new Grid { Children = { host } },
            };
            window.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
            window.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());

            try
            {
                window.Show();
                window.Activate();
                // WPF-UI 3.x hosts dialogs in a ContentPresenter; 4.x deprecates it in favor of
                // ContentDialogHost. Switch the host type once this branch is on WPF-UI 4.x.
#pragma warning disable CS0618
                var dialog = new Wpf.Ui.Controls.ContentDialog(host)
#pragma warning restore CS0618
                {
                    Title = "Report",
                    Content = content,
                    PrimaryButtonText = "Send",
                    CloseButtonText = "Cancel",
                };
                _ = dialog.ShowAsync();
                PumpDispatcher();

                var contentViewers = FindVisualDescendants<ScrollViewer>(content).ToHashSet();
                var dialogViewer = FindVisualDescendants<ScrollViewer>(dialog)
                    .Single(viewer => !contentViewers.Contains(viewer) && IsAncestorOf(viewer, content));
                Assert.True(dialogViewer.ScrollableHeight > 0,
                    $"Expected the dialog to overflow the test window (range={dialogViewer.ScrollableHeight}).");

                var nestedRanges = contentViewers
                    .Where(viewer => viewer.ScrollableHeight > 0)
                    .Select(viewer => $"{DescribeOwner(viewer)} range={viewer.ScrollableHeight:0}")
                    .ToList();
                Assert.True(nestedRanges.Count == 0,
                    "Nested scroll areas inside the report content: " + string.Join(", ", nestedRanges));

                var textBoxes = FindVisualDescendants<TextBox>(content).ToList();
                var targets = new List<UIElement>(textBoxes)
                {
                    FindVisualDescendants<TextBlock>(content).First(),
                    FindVisualDescendants<Button>(content).First(),
                };
                foreach (var target in targets)
                {
                    dialogViewer.ScrollToVerticalOffset(0);
                    PumpDispatcher();
                    RaiseWheel(target, -120);
                    PumpDispatcher();
                    Assert.True(dialogViewer.VerticalOffset > 0,
                        $"Wheel over {DescribeOwner(target)} did not scroll the dialog.");
                }

                // The suggestion grows instead of scrolling internally, so the dialog scroller
                // must follow the caret when it moves below the visible area.
                dialogViewer.ScrollToVerticalOffset(0);
                PumpDispatcher();
                var suggestion = (TextBox)content.FindName("SuggestedTranslationTextBox");
                suggestion.Focus();
                suggestion.Text = string.Join(Environment.NewLine, Enumerable.Repeat("제안 번역 줄", 12));
                suggestion.CaretIndex = suggestion.Text.Length;
                PumpDispatcher();
                var caret = suggestion.GetRectFromCharacterIndex(suggestion.CaretIndex);
                var caretInViewer = suggestion.TransformToAncestor(dialogViewer).TransformBounds(caret);
                Assert.True(dialogViewer.VerticalOffset > 0, "Dialog did not scroll to follow the caret.");
                Assert.True(caretInViewer.Bottom <= dialogViewer.ViewportHeight + 1 && caretInViewer.Top >= -1,
                    $"Caret is outside the dialog viewport: {caretInViewer}, viewport={dialogViewer.ViewportHeight}.");

                // Expanding the collapsed comment section scrolls its text box into view.
                dialogViewer.ScrollToVerticalOffset(0);
                PumpDispatcher();
                var comment = (TextBox)content.FindName("CommentTextBox");
                var expander = FindVisualDescendants<Expander>(content).Single();
                expander.IsExpanded = true;
                PumpDispatcher();
                // Measure the expander's layout bounds: its content is still sliding in through a
                // render transform, so the text box's transformed position is not final yet.
                var expanderInViewer = expander.TransformToAncestor(dialogViewer)
                    .TransformBounds(new Rect(expander.RenderSize));
                Assert.True(comment.IsKeyboardFocused, "Comment box was not focused after expanding.");
                Assert.True(expanderInViewer.Bottom <= dialogViewer.ViewportHeight + 1,
                    $"Expanded comment section is outside the dialog viewport: {expanderInViewer}, viewport={dialogViewer.ViewportHeight}.");

                dialog.Hide();
                PumpDispatcher();
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RaiseWheel(UIElement target, int delta)
    {
        // Mirror the input manager: tunnel first, then bubble with the same handled state.
        var preview = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta)
        {
            RoutedEvent = Mouse.PreviewMouseWheelEvent,
        };
        target.RaiseEvent(preview);
        var bubble = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta)
        {
            RoutedEvent = Mouse.MouseWheelEvent,
            Handled = preview.Handled,
        };
        target.RaiseEvent(bubble);
    }

    private static string DescribeOwner(DependencyObject element)
    {
        for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is TextBox textBox)
                return $"TextBox('{Truncate(textBox.Text)}')";
            if (current is FrameworkElement { Name.Length: > 0 } named)
                return $"{named.GetType().Name}#{named.Name}";
        }
        return element.GetType().Name;
    }

    private static string Truncate(string value) => value.Length <= 16 ? value : value[..16] + "…";

    private static bool IsAncestorOf(DependencyObject ancestor, DependencyObject descendant)
    {
        for (var current = descendant; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor)) return true;
        }
        return false;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualDescendants<T>(child)) yield return descendant;
        }
    }

    private static void EnsureLocalizer()
    {
        // The dialog XAML uses {i18n:StringLocalizer}, which reads the global provider.
        new ServiceCollection().AddStringLocalizer(builder =>
            builder.FromYaml(typeof(App).Assembly, "Resources/Strings/ko-KR.yaml", new("ko-KR")));
        Lepo.i18n.LocalizationProviderFactory.GetInstance()?.SetCulture(new("ko-KR"));
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Desktop smoke test timed out.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
