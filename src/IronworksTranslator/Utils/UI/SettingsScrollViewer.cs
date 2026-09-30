using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace IronworksTranslator.Utils.UI
{
    public static class SettingsScrollViewer
    {
        public static readonly DependencyProperty ScrollOnMouseWheelProperty =
            DependencyProperty.RegisterAttached("ScrollOnMouseWheel", typeof(bool), typeof(SettingsScrollViewer),
                new PropertyMetadata(false, OnScrollOnMouseWheelChanged));

        public static bool GetScrollOnMouseWheel(DependencyObject element) =>
            (bool)element.GetValue(ScrollOnMouseWheelProperty);

        public static void SetScrollOnMouseWheel(DependencyObject element, bool value) =>
            element.SetValue(ScrollOnMouseWheelProperty, value);

        private static void OnScrollOnMouseWheelChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is not ScrollViewer viewer) return;
            if ((bool)e.NewValue)
                viewer.PreviewMouseWheel += OnPreviewMouseWheel;
            else
                viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || sender is not ScrollViewer viewer) return;
            for (var element = e.OriginalSource as DependencyObject; element != null && element != viewer;
                element = GetParent(element))
            {
                // An open selector keeps its own list scrolling.
                if (element is ComboBox { IsDropDownOpen: true }) return;
            }

            // NavigationView measures the page inside its own DynamicScrollViewer.
            // The tab scroller can consequently have no scroll range even when the page
            // overflows. Forward to the nearest ancestor that actually has a viewport.
            ScrollViewer? target = viewer;
            while (target != null && target.ScrollableHeight <= 0)
            {
                var parent = GetParent(target);
                target = null;
                while (parent != null)
                {
                    if (parent is ScrollViewer ancestor)
                    {
                        target = ancestor;
                        break;
                    }
                    parent = GetParent(parent);
                }
            }
            if (target == null) return;

            // Handle preview before nested controls consume the wheel. Use the scroller's
            // native handling to respect Windows wheel settings.
            e.Handled = true;
            target.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = Mouse.MouseWheelEvent,
                Source = target
            });
        }

        private static DependencyObject? GetParent(DependencyObject element) => element switch
        {
            Visual or Visual3D => VisualTreeHelper.GetParent(element),
            FrameworkContentElement content => content.Parent,
            _ => LogicalTreeHelper.GetParent(element)
        };
    }
}
