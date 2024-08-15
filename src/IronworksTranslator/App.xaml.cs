using IronworksTranslator.Services;
using IronworksTranslator.Services.FFXIV;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.ViewModels.Windows;
using IronworksTranslator.Views.Pages;
using IronworksTranslator.Views.Windows;
using Lepo.i18n.DependencyInjection;
using Lepo.i18n.Yaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using Wpf.Ui;

namespace IronworksTranslator
{
#pragma warning disable CS8602, CS8603, CS8604
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)); })
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<ApplicationHostService>();
                services.AddHostedService<ChatLookupService>();

                // Add i18n
                services.AddStringLocalizer(b =>
                {
                    b.FromYaml(Assembly.GetExecutingAssembly(), "Resources/Strings/ko-KR.yaml", new("ko-KR"));
                    b.FromYaml(Assembly.GetExecutingAssembly(), "Resources/Strings/en-US.yaml", new("en-US"));
                });
                Localizer.ChangeLanguage("ko-KR");

                // Page resolver service
                services.AddSingleton<IPageService, PageService>();

                // Theme manipulation
                services.AddSingleton<IThemeService, ThemeService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();

                // Dialog manipulation
                services.AddSingleton<IContentDialogService, ContentDialogService>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DashboardViewModel>();
#if DEBUG
                services.AddSingleton<DeveloperPage>();
                services.AddSingleton<DeveloperViewModel>();
#endif
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();

                // Chat window
                services.AddSingleton<ChatWindow>();
                services.AddSingleton<ChatWindowViewModel>();

                // Translator stuff
                services.AddSingleton<WebBrowser>();
                services.AddSingleton<PapagoTranslator>();
                services.AddSingleton<DeepLAPITranslator>();
            }).Build();

        /// <summary>
        /// Gets registered service.
        /// </summary>
        /// <typeparam name="T">Type of the service to get.</typeparam>
        /// <returns>Instance of the service or <see langword="null"/>.</returns>
        public static T GetService<T>()
            where T : class
        {
            return _host.Services.GetService(typeof(T)) as T;
        }

        public static IEnumerable<T> GetServices<T>()
            where T : class
        {
            return _host.Services.GetServices<T>();
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private void OnStartup(object sender, StartupEventArgs e)
        {
            InitLogger();
            SetupUnhandledExceptionHandlers();

            // Check if the _host is disposed
            try
            {
                _host.Start();
                var chatWindow = GetService<ChatWindow>();
                chatWindow.Show();
                BootWatchDog();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start host.");
                return;
            }
        }

        private static void BootWatchDog()
        {
            var pid = Environment.ProcessId;
            var info = new ProcessStartInfo
            {
                FileName = "WatchDogMain.exe",
                Arguments = $"{pid}",
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            _ = Process.Start(info);
        }

        private void SetupUnhandledExceptionHandlers()
        {
            // Catch exceptions from all threads in the AppDomain.
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                ShowUnhandledException(args.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");

            // Catch exceptions from each AppDomain that uses a task scheduler for async operations.
            TaskScheduler.UnobservedTaskException += (sender, args) =>
                ShowUnhandledException(args.Exception, "TaskScheduler.UnobservedTaskException");

            // Catch exceptions from a single specific UI dispatcher thread.
            Dispatcher.UnhandledException += (sender, args) =>
            {
                if (!Debugger.IsAttached)
                {
                    args.Handled = true;
                    ShowUnhandledException(args.Exception, "Dispatcher.UnhandledException");
                }
            };
        }

        private static void ShowUnhandledException(Exception e, string unhandledExceptionType)
        {
            var messageBoxTitle = Localizer.GetString("app.exception.title"); 
            var messageBoxMessage = Localizer.GetString("app.exception.description");
            var messageBoxButtons = MessageBoxButton.OK;

            Log.Error(e, unhandledExceptionType);

            var t = new Thread(() =>
            {
                MessageBox.Show(messageBoxMessage, messageBoxTitle, messageBoxButtons);
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            Log.Information("OnExit: IronworksTranslator is closing.");
            Log.CloseAndFlush();

            await _host.StopAsync();
            _host.Dispose();
        }

        public static void RequestShutdown()
        {
            _host.StopAsync().Wait();
            Log.Information("RequestShutdown: IronworksTranslator is closing.");
            _host.Dispose();

            Current.Shutdown();
        }

        private static void InitLogger()
        {
            /*
             Enabled Log levels: Debug, Information, Warning, Error, Fatal
             Disabled Log levels: Verbose
             */

            // Logging
            var date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File($"logs/{date}.txt")
                .CreateLogger();
            Log.Information($"IronworksTranslator {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)} started.");
        }


    }
#pragma warning restore CS8602, CS8603, CS8604
}
