using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace IronworksTranslator.Utils.UI
{
    public static class NavigationScrollHint
    {
        private static readonly DependencyPropertyKey HasMoreBelowPropertyKey =
            DependencyProperty.RegisterAttachedReadOnly("HasMoreBelow", typeof(bool),
                typeof(NavigationScrollHint), new PropertyMetadata(false));

        public static readonly DependencyProperty HasMoreBelowProperty = HasMoreBelowPropertyKey.DependencyProperty;

        private static readonly ConditionalWeakTable<NavigationView, Observer> Observers = new();

        public static bool GetHasMoreBelow(DependencyObject element) => (bool)element.GetValue(HasMoreBelowProperty);

        internal static void Attach(NavigationView navigation) => Observers.GetValue(navigation, view => new Observer(view));

        private sealed class Observer
        {
            private readonly NavigationView _navigation;
            private bool _updatePending;

            public Observer(NavigationView navigation)
            {
                _navigation = navigation;
                navigation.Loaded += (_, _) => QueueUpdate();
                navigation.SizeChanged += (_, _) => QueueUpdate();
                navigation.Navigated += (_, _) => QueueUpdate();
                navigation.Unloaded += (_, _) => navigation.SetValue(HasMoreBelowPropertyKey, false);
                navigation.AddHandler(ScrollViewer.ScrollChangedEvent,
                    new ScrollChangedEventHandler((_, _) => QueueUpdate()), true);
                QueueUpdate();
            }

            private void QueueUpdate()
            {
                if (_updatePending)
                    return;

                _updatePending = true;
                _navigation.Dispatcher.InvokeAsync(() =>
                {
                    _updatePending = false;
                    var presenter = Descendants<NavigationViewContentPresenter>(_navigation).FirstOrDefault();
                    bool hasMore = _navigation.IsLoaded && presenter != null &&
                        Descendants<ScrollViewer>(presenter).Any(viewer =>
                            viewer.IsVisible && viewer.ViewportHeight > 0 &&
                            (viewer is DynamicScrollViewer || SettingsScrollViewer.GetScrollOnMouseWheel(viewer)) &&
                            viewer.ScrollableHeight - viewer.VerticalOffset > 1);

                    _navigation.SetValue(HasMoreBelowPropertyKey, hasMore);
                }, DispatcherPriority.Loaded);
            }
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                    yield return match;

                foreach (var descendant in Descendants<T>(child))
                    yield return descendant;
            }
        }
    }
}
