using System.Runtime.CompilerServices;

namespace IronworksTranslator.Utils.UI
{
    // Keep user visibility separate from temporary game-focus suppression. Hide() on an
    // already hidden HWND has no visibility event, so dashboard/close actions use SetVisible.
    internal sealed class TranslationWindowVisibility
    {
        private static readonly ConditionalWeakTable<Window, TranslationWindowVisibility> Controllers = new();
        private readonly Window _window;
        private bool _requestedVisible;
        private bool _suppressed;
        private bool _applying;

        private TranslationWindowVisibility(Window window)
        {
            _window = window;
            _requestedVisible = window.IsVisible;
            window.IsVisibleChanged += OnVisibilityChanged;
            window.Closed += OnClosed;
        }

        internal static void SetVisible(Window window, bool visible)
        {
            var controller = Controllers.GetValue(window, window => new TranslationWindowVisibility(window));
            controller._requestedVisible = visible;
            controller.Apply();
        }

        internal static void SetSuppressed(Window window, bool suppressed)
        {
            var controller = Controllers.GetValue(window, window => new TranslationWindowVisibility(window));
            controller._suppressed = suppressed;
            controller.Apply();
        }

        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_applying) return;
            _requestedVisible = _window.IsVisible;
            Apply();
        }

        private void Apply()
        {
            var visible = _requestedVisible && !_suppressed;
            if (_window.IsVisible == visible) return;
            _applying = true;
            try
            {
                if (visible)
                {
                    var showActivated = _window.ShowActivated;
                    try
                    {
                        _window.ShowActivated = false;
                        _window.Show();
                    }
                    finally
                    {
                        _window.ShowActivated = showActivated;
                    }
                }
                else
                {
                    // Hide instead of minimize: tool windows otherwise leave a small
                    // minimized caption at the bottom of the desktop.
                    _window.Hide();
                }
            }
            finally
            {
                _applying = false;
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _window.IsVisibleChanged -= OnVisibilityChanged;
            _window.Closed -= OnClosed;
            Controllers.Remove(_window);
        }
    }
}
