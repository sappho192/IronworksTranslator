using IronworksTranslator.Views.Windows;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Wpf.Ui;

namespace IronworksTranslator.Services.FFXIV
{
    public class MinimizationLookupService : IHostedService, IDisposable
    {
        private Timer? minimizationTimer;
        private const int period = 1000;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (minimizationTimer == null)
            {
                minimizationTimer = new Timer(MinimizeWindow, null, 0, period);
            }
            else
            {
                minimizationTimer.Change(0, period);
            }

            return Task.CompletedTask;
        }

        private void MinimizeWindow(object? state)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var chatWindow = App.GetService<ChatWindow>();
                if (ApplicationIsActivated() || AppWindowIsFocused())
                {
                    if (chatWindow.WindowState != WindowState.Normal)
                    {
                        chatWindow.WindowState = WindowState.Normal;
                    }
                }
                else
                {
                    if (chatWindow.WindowState != WindowState.Minimized)
                    {
                        chatWindow.WindowState = WindowState.Minimized;
                    }
                }
            });
        }

        private static bool AppWindowIsFocused()
        {
            var chatWindow = App.GetService<ChatWindow>();
            var mainWindow = App.GetServices<INavigationWindow>().OfType<MainWindow>().Single();
            if (chatWindow.IsActive || mainWindow.IsActive)
            {
                return true;
            }
            return false;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            minimizationTimer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            minimizationTimer?.Dispose();
        }

        /// <summary>Returns true if the current application has focus, false otherwise</summary>
        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var chatLookupService = App.GetServices<IHostedService>().OfType<ChatLookupService>().Single();
            var procId = chatLookupService.GameProcessID;
            GetWindowThreadProcessId(activatedHandle, out int activeProcId);

            return activeProcId == procId;
        }


        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
    }
}
