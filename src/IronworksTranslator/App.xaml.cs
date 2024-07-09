using IronworksTranslator.Services;
using IronworksTranslator.Utils;
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
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using Wpf.Ui;

namespace IronworksTranslator
{
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
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();
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

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private void OnStartup(object sender, StartupEventArgs e)
        {
            InitLogger();

            _host.Start();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            Log.Information("IronworksTranslator is closing.");

            await _host.StopAsync();
            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }

        public static void RequestShutdown()
        {
            _host.StopAsync().Wait();
            Log.Information("IronworksTranslator is closing.");
            Log.CloseAndFlush();
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
}
