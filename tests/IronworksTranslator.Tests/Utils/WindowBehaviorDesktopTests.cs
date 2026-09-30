using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils.UI;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace IronworksTranslator.Tests.Utils;

// Opt-in because these tests create real HWNDs and briefly activate their own test window.
public class WindowBehaviorDesktopTests
{
    public static bool DesktopSmokeEnabled =>
        Environment.GetEnvironmentVariable("IRONWORKS_WINDOW_SMOKE") == "1";

    [Fact(Skip = "Set IRONWORKS_WINDOW_SMOKE=1 on an interactive Windows desktop.", SkipUnless = nameof(DesktopSmokeEnabled))]
    public void NativeStates_RestoreNormalAndMaximizedWithoutStealingFocus()
    {
        RunSta(() =>
        {
            var followed = NewWindow("Ironworks window smoke: followed");
            var anchor = NewWindow("Ironworks window smoke: focus");
            try
            {
                followed.Show();
                anchor.Show();
                followed.ShowActivated = true; // Match the application's default after initial show.
                var anchorHandle = new WindowInteropHelper(anchor).Handle;
                Assert.Equal(Environment.ProcessId, NativeWindowMethods.GetProcessId(anchorHandle));
                Assert.Equal(0, NativeWindowMethods.GetProcessId(IntPtr.Zero));
                Assert.NotEqual(IntPtr.Zero, NativeWindowMethods.FindProcessWindow(Environment.ProcessId));

                foreach (var original in new[] { WindowState.Normal, WindowState.Maximized })
                {
                    followed.WindowState = original;
                    SetActiveWindow(anchorHandle);
                    var foreground = NativeWindowMethods.GetForegroundWindow();
                    Assert.Equal(anchorHandle, GetActiveWindow());
                    NativeWindowMethods.SetWindowStateWithoutActivation(followed, WindowState.Minimized);
                    Assert.Equal(WindowState.Minimized, followed.WindowState);
                    Assert.True(NativeWindowMethods.IsIconic(new WindowInteropHelper(followed).Handle));
                    Assert.Equal(foreground, NativeWindowMethods.GetForegroundWindow());
                    Assert.Equal(anchorHandle, GetActiveWindow());
                    NativeWindowMethods.SetWindowStateWithoutActivation(followed, original);
                    Assert.Equal(original, followed.WindowState);
                    Assert.Equal(foreground, NativeWindowMethods.GetForegroundWindow());
                    Assert.Equal(anchorHandle, GetActiveWindow());
                }

                var finalForeground = NativeWindowMethods.GetForegroundWindow();
                NativeWindowMethods.SetWindowStateWithoutActivation(followed, WindowState.Minimized);
                followed.Hide();
                NativeWindowMethods.SetWindowStateWithoutActivation(followed, WindowState.Normal);
                Assert.False(followed.IsVisible);
                Assert.Equal(WindowState.Normal, followed.WindowState);
                Assert.Equal(finalForeground, NativeWindowMethods.GetForegroundWindow());
            }
            finally
            {
                followed.Close();
                anchor.Close();
            }
        });
    }

    [Fact(Skip = "Set IRONWORKS_WINDOW_SMOKE=1 on an interactive Windows desktop.", SkipUnless = nameof(DesktopSmokeEnabled))]
    public void AltTabSetting_AppliesBeforeShowAndUpdatesVisibleMinimizedAndHiddenWindows()
    {
        RunSta(() =>
        {
            var settings = new UISettings();
            var overlay = NewWindow("Ironworks window smoke: overlay");
            overlay.WindowStyle = WindowStyle.None;
            overlay.AllowsTransparency = true;
            overlay.Topmost = true;
            var anchor = NewWindow("Ironworks window smoke: focus");
            try
            {
                TranslationWindowAltTab.Attach(overlay, settings);
                Assert.False(overlay.ShowInTaskbar);
                overlay.Show();
                anchor.Show();
                overlay.ShowActivated = true;
                var anchorHandle = new WindowInteropHelper(anchor).Handle;
                SetActiveWindow(anchorHandle);
                var foreground = NativeWindowMethods.GetForegroundWindow();
                AssertStyle(overlay, true);
                foreach (var state in new[] { WindowState.Normal, WindowState.Minimized })
                {
                    NativeWindowMethods.SetWindowStateWithoutActivation(overlay, state);
                    foreach (var excluded in new[] { false, true })
                    {
                        settings.HideTranslationWindowsFromAltTab = excluded;
                        PumpDispatcher();
                        AssertStyle(overlay, excluded);
                        Assert.Equal(state, overlay.WindowState);
                        Assert.Equal(foreground, NativeWindowMethods.GetForegroundWindow());
                        Assert.Equal(anchorHandle, GetActiveWindow());
                    }
                }
                overlay.Hide();
                settings.HideTranslationWindowsFromAltTab = false;
                PumpDispatcher();
                Assert.False(overlay.IsVisible);
                AssertStyle(overlay, false);
                Assert.Equal(foreground, NativeWindowMethods.GetForegroundWindow());
                Assert.True(overlay.Topmost);
                Assert.True(overlay.AllowsTransparency);
            }
            finally
            {
                overlay.Close();
                anchor.Close();
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(settings);
            }
        });
    }

    private static Window NewWindow(string title) => new()
    {
        Title = title,
        Width = 200,
        Height = 100,
        ShowActivated = false
    };

    [Fact(Skip = "Set IRONWORKS_WINDOW_SMOKE=1 on an interactive Windows desktop.", SkipUnless = nameof(DesktopSmokeEnabled))]
    public void SettingsWheel_ScrollsOverNestedControlsWithoutChangingSelection()
    {
        RunSta(() =>
        {
            var selector = new ComboBox { ItemsSource = new[] { "First", "Second" }, SelectedIndex = 0 };
            UIElement[] controls =
            [
                new Button { Content = "Button" },
                selector,
                new TextBox { Text = "Nested text box", Height = 40 },
                new Slider { Value = 0.5 },
                new Grid { Height = 40 }
            ];
            var panel = new StackPanel { Height = 1400 };
            foreach (var control in controls) panel.Children.Add(control);
            var viewer = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            SettingsScrollViewer.SetScrollOnMouseWheel(viewer, true);
            var window = NewWindow("Ironworks window smoke: settings scroll");
            window.Height = 280;
            window.Content = viewer;
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(viewer.ScrollableHeight > 0);
                foreach (var control in controls)
                {
                    var childConsumedWheel = false;
                    MouseWheelEventHandler childHandler = (_, e) =>
                    {
                        childConsumedWheel = true;
                        e.Handled = true;
                    };
                    control.PreviewMouseWheel += childHandler;
                    try
                    {
                        var before = viewer.VerticalOffset;
                        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120)
                        {
                            RoutedEvent = Mouse.PreviewMouseWheelEvent
                        };
                        control.RaiseEvent(wheel);
                        PumpDispatcher();
                        Assert.True(wheel.Handled);
                        Assert.False(childConsumedWheel);
                        Assert.True(viewer.VerticalOffset > before);
                    }
                    finally
                    {
                        control.PreviewMouseWheel -= childHandler;
                    }
                }
                Assert.Equal(0, selector.SelectedIndex);
                Assert.Equal(0.5, ((Slider)controls[3]).Value);

                selector.IsDropDownOpen = true;
                PumpDispatcher();
                var offset = viewer.VerticalOffset;
                selector.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120)
                {
                    RoutedEvent = Mouse.PreviewMouseWheelEvent
                });
                PumpDispatcher();
                Assert.Equal(offset, viewer.VerticalOffset);
                selector.IsDropDownOpen = false;
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact(Skip = "Set IRONWORKS_WINDOW_SMOKE=1 on an interactive Windows desktop.", SkipUnless = nameof(DesktopSmokeEnabled))]
    public void SettingsWheel_ScrollsActualCardInsideNavigationView()
    {
        RunSta(() =>
        {
            var selector = new ComboBox { ItemsSource = new[] { "First", "Second" }, SelectedIndex = 0 };
            var button = new Wpf.Ui.Controls.Button { Content = "Inside card" };
            var content = new StackPanel { Height = 1400 };
            content.Children.Add(button);
            content.Children.Add(selector);
            var card = new Wpf.Ui.Controls.Card
            {
                Content = content,
                VerticalAlignment = VerticalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top
            };
            var inner = new ScrollViewer { Content = card, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            SettingsScrollViewer.SetScrollOnMouseWheel(inner, true);
            var tabs = new TabControl { Margin = new Thickness(0, 48, 0, 16) };
            tabs.Items.Add(new TabItem { Header = "Settings", Content = inner });
            var page = new Page { Content = tabs };
            page.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
            page.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            var navigation = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = Wpf.Ui.Controls.NavigationViewPaneDisplayMode.LeftFluent,
                FrameMargin = new Thickness(0),
                Padding = new Thickness(42, 0, 42, 0)
            };
            var window = NewWindow("Ironworks window smoke: actual settings card");
            window.Width = 900;
            window.Height = 400;
            window.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
            window.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            window.Content = navigation;
            try
            {
                window.Show();
                Assert.True(navigation.ReplaceContent(page, null));
                PumpDispatcher();
                card.ApplyTemplate();
                var outer = FindVisualDescendants<ScrollViewer>(navigation)
                    .First(viewer => viewer != inner && viewer.ScrollableHeight > 0);
                Assert.Equal(0, inner.ScrollableHeight);
                var border = Assert.IsType<Border>(card.Template.FindName("ContentBorder", card));
                foreach (var target in new UIElement[] { border, card, button, selector })
                {
                    var before = outer.VerticalOffset + inner.VerticalOffset;
                    target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -120)
                    {
                        RoutedEvent = Mouse.PreviewMouseWheelEvent
                    });
                    PumpDispatcher();
                    Assert.True(outer.VerticalOffset + inner.VerticalOffset > before,
                        $"Wheel over {target.GetType().Name} did not scroll: inner range={inner.ScrollableHeight}, outer range={outer.ScrollableHeight}.");
                }
                Assert.Equal(0, selector.SelectedIndex);
                Assert.Equal(0, inner.VerticalOffset);

                var downOffset = outer.VerticalOffset;
                border.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120)
                {
                    RoutedEvent = Mouse.PreviewMouseWheelEvent
                });
                PumpDispatcher();
                Assert.True(outer.VerticalOffset < downOffset);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact(Skip = "Set IRONWORKS_WINDOW_SMOKE=1 on an interactive Windows desktop.", SkipUnless = nameof(DesktopSmokeEnabled))]
    public void ScrollHint_TracksRemainingContentAcrossScrollingTabsAndResizing()
    {
        RunSta(() =>
        {
            var inner = new ScrollViewer
            {
                Content = new Wpf.Ui.Controls.Card { Content = new Border { Height = 1000 } },
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            SettingsScrollViewer.SetScrollOnMouseWheel(inner, true);
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "Long page", Content = inner });
            tabs.Items.Add(new TabItem { Header = "Short page", Content = new Border { Height = 50 } });
            var page = new Page { Content = tabs };
            page.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
            page.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            var navigation = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = Wpf.Ui.Controls.NavigationViewPaneDisplayMode.LeftFluent,
                FrameMargin = new Thickness(0)
            };
            NavigationScrollHint.Attach(navigation);
            var window = NewWindow("Ironworks window smoke: scroll hint");
            window.Width = 900;
            window.Height = 550;
            window.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
            window.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
            window.Content = navigation;
            try
            {
                window.Show();
                Assert.True(navigation.ReplaceContent(page, null));
                PumpDispatcher();
                Assert.True(NavigationScrollHint.GetHasMoreBelow(navigation));
                var scroller = FindVisualDescendants<ScrollViewer>(navigation)
                    .First(viewer => viewer.ScrollableHeight > 0);
                scroller.ScrollToBottom();
                PumpDispatcher();
                Assert.False(NavigationScrollHint.GetHasMoreBelow(navigation));
                scroller.ScrollToTop();
                PumpDispatcher();
                Assert.True(NavigationScrollHint.GetHasMoreBelow(navigation));

                tabs.SelectedIndex = 1;
                PumpDispatcher();
                Assert.False(NavigationScrollHint.GetHasMoreBelow(navigation));
                tabs.SelectedIndex = 0;
                PumpDispatcher();
                Assert.True(NavigationScrollHint.GetHasMoreBelow(navigation));

                window.Height = 1400;
                PumpDispatcher();
                Assert.False(NavigationScrollHint.GetHasMoreBelow(navigation));
                window.Height = 550;
                PumpDispatcher();
                Assert.True(NavigationScrollHint.GetHasMoreBelow(navigation));

                Assert.True(navigation.ReplaceContent(new Page { Content = new Border { Height = 50 } }, null));
                PumpDispatcher();
                Assert.False(NavigationScrollHint.GetHasMoreBelow(navigation));
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualDescendants<T>(child)) yield return descendant;
        }
    }

    [Fact(Skip = "Set IRONWORKS_WINDOW_SMOKE=1 on an interactive Windows desktop.", SkipUnless = nameof(DesktopSmokeEnabled))]
    public void GameSuppression_HidesOverlayCompletelyAndPreservesVisibilityIntentAndFocus()
    {
        RunSta(() =>
        {
            var overlay = NewWindow("Ironworks window smoke: hidden overlay");
            overlay.WindowStyle = WindowStyle.None;
            overlay.AllowsTransparency = true;
            overlay.Topmost = true;
            var anchor = NewWindow("Ironworks window smoke: focus");
            var settings = new UISettings();
            try
            {
                TranslationWindowAltTab.Attach(overlay, settings);
                TranslationWindowVisibility.SetVisible(overlay, true);
                overlay.ShowActivated = true;
                anchor.Show();
                var anchorHandle = new WindowInteropHelper(anchor).Handle;
                SetActiveWindow(anchorHandle);
                var foreground = NativeWindowMethods.GetForegroundWindow();
                var bounds = new Rect(overlay.Left, overlay.Top, overlay.Width, overlay.Height);
                var handle = new WindowInteropHelper(overlay).Handle;

                TranslationWindowVisibility.SetSuppressed(overlay, true);
                Assert.False(overlay.IsVisible);
                Assert.False(IsWindowVisible(handle));
                Assert.False(NativeWindowMethods.IsIconic(handle));
                Assert.Equal(WindowState.Normal, overlay.WindowState);
                PumpDispatcher();
                TranslationWindowVisibility.SetSuppressed(overlay, true);
                TranslationWindowVisibility.SetSuppressed(overlay, false);
                Assert.True(overlay.IsVisible);
                Assert.True(IsWindowVisible(handle));
                Assert.True(overlay.ShowActivated);
                Assert.Equal(bounds, new Rect(overlay.Left, overlay.Top, overlay.Width, overlay.Height));
                Assert.Equal(foreground, NativeWindowMethods.GetForegroundWindow());
                Assert.Equal(anchorHandle, GetActiveWindow());

                // A user hide while already suppressed must cancel automatic restoration.
                TranslationWindowVisibility.SetSuppressed(overlay, true);
                TranslationWindowVisibility.SetVisible(overlay, false);
                TranslationWindowVisibility.SetSuppressed(overlay, false);
                Assert.False(overlay.IsVisible);

                // A user show while away must wait until the game/app is active again.
                TranslationWindowVisibility.SetSuppressed(overlay, true);
                TranslationWindowVisibility.SetVisible(overlay, true);
                Assert.False(overlay.IsVisible);
                TranslationWindowVisibility.SetSuppressed(overlay, false);
                Assert.True(overlay.IsVisible);
                Assert.Equal(foreground, NativeWindowMethods.GetForegroundWindow());
                Assert.Equal(anchorHandle, GetActiveWindow());

                overlay.Hide(); // Honor ordinary visibility changes as well.
                TranslationWindowVisibility.SetSuppressed(overlay, true);
                TranslationWindowVisibility.SetSuppressed(overlay, false);
                Assert.False(overlay.IsVisible);
            }
            finally
            {
                overlay.Close();
                anchor.Close();
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.UnregisterAll(settings);
            }
        });
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    private static void AssertStyle(Window window, bool excluded)
    {
        var style = NativeWindowMethods.GetExtendedStyle(new WindowInteropHelper(window).Handle);
        Assert.Equal(excluded, (style & NativeWindowMethods.WsExToolWindow) != 0);
        Assert.Equal(!excluded, (style & NativeWindowMethods.WsExAppWindow) != 0);
        Assert.Equal(!excluded, window.ShowInTaskbar);
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Desktop smoke test timed out.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
