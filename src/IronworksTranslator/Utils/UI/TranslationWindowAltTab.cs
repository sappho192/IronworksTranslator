using IronworksTranslator.Models.Settings;
using Serilog;
using System.ComponentModel;
using System.Windows.Interop;

namespace IronworksTranslator.Utils.UI
{
    internal sealed class TranslationWindowAltTab
    {
        private readonly Window _window;
        private readonly UISettings _settings;

        private TranslationWindowAltTab(Window window, UISettings settings)
        {
            _window = window;
            _settings = settings;
            window.SourceInitialized += OnSourceInitialized;
            window.Closed += OnClosed;
            settings.PropertyChanged += OnSettingsChanged;
            Apply();
        }

        internal static void Attach(Window window, UISettings settings) =>
            _ = new TranslationWindowAltTab(window, settings);

        private void OnSourceInitialized(object? sender, EventArgs e) => Apply();

        private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UISettings.HideTranslationWindowsFromAltTab))
                _window.Dispatcher.InvokeAsync(Apply);
        }

        private void Apply()
        {
            var excluded = _settings.HideTranslationWindowsFromAltTab;
            _window.ShowInTaskbar = !excluded;
            var handle = new WindowInteropHelper(_window).Handle;
            if (handle == IntPtr.Zero) return;
            try
            {
                NativeWindowMethods.SetAltTabExcluded(handle, excluded);
            }
            catch (Win32Exception ex)
            {
                Log.Warning(ex, "Failed to update translation window Alt+Tab visibility.");
            }
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _settings.PropertyChanged -= OnSettingsChanged;
            _window.SourceInitialized -= OnSourceInitialized;
            _window.Closed -= OnClosed;
        }
    }
}
