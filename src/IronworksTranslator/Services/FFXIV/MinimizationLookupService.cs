using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils.UI;
using IronworksTranslator.Views.Windows;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.ComponentModel;
using System.Windows.Threading;

namespace IronworksTranslator.Services.FFXIV
{
    public sealed class MinimizationLookupService : IHostedService, IDisposable
    {
        private readonly Dictionary<Window, AutomaticWindowState> _windowStates = new();
        private DispatcherTimer? _timer;
        private IntPtr _gameWindow;
        private int _gameProcessId;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_timer == null)
            {
                // Dispatcher ticks cannot overlap or block host shutdown waiting for the UI.
                _timer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                _timer.Tick += OnTick;
            }
            _timer.Start();
            return Task.CompletedTask;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var settings = IronworksSettings.Instance?.UiSettings;
            var lookup = App.GetServices<IHostedService>().OfType<ChatLookupService>().Single();
            var gameProcessId = lookup.Attached ? lookup.GameProcessID : 0;
            if (_gameProcessId != gameProcessId ||
                NativeWindowMethods.GetProcessId(_gameWindow) != gameProcessId)
            {
                _gameProcessId = gameProcessId;
                _gameWindow = NativeWindowMethods.FindProcessWindow(gameProcessId);
            }
            if (_gameWindow == IntPtr.Zero && gameProcessId > 0)
            {
                _gameWindow = NativeWindowMethods.FindProcessWindow(gameProcessId);
            }

            var action = GameWindowFollowPolicy.Decide(
                settings?.FollowGameWindow == true,
                _gameWindow != IntPtr.Zero,
                NativeWindowMethods.IsIconic(_gameWindow),
                NativeWindowMethods.GetProcessId(NativeWindowMethods.GetForegroundWindow()),
                gameProcessId,
                Environment.ProcessId);
            if (action == GameWindowFollowAction.Keep) return;

            // Polling must not construct or show disabled overlays.
            foreach (var window in Application.Current.Windows.Cast<Window>()
                .Where(window => window is MainWindow or ChatWindow or DialogueWindow).ToArray())
            {
                if (window is ChatWindow or DialogueWindow)
                {
                    TranslationWindowVisibility.SetSuppressed(window,
                        action == GameWindowFollowAction.Minimize);
                    continue;
                }
                if (!_windowStates.TryGetValue(window, out var state))
                {
                    state = new AutomaticWindowState();
                    _windowStates.Add(window, state);
                    window.Closed += (_, _) => _windowStates.Remove(window);
                }

                var nextState = state.Update(window.IsVisible, window.WindowState,
                    action == GameWindowFollowAction.Minimize);
                if (nextState.HasValue)
                {
                    try
                    {
                        NativeWindowMethods.SetWindowStateWithoutActivation(window, nextState.Value);
                    }
                    catch (Win32Exception ex)
                    {
                        Log.Warning(ex, "Failed to update automatic window state.");
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
                _timer = null;
            }
            GC.SuppressFinalize(this);
        }
    }
}
