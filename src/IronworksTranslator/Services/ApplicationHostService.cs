using IronworksTranslator.Models.Settings;
using IronworksTranslator.Utils;
using IronworksTranslator.Views.Windows;
using Microsoft.Extensions.Hosting;
using Wpf.Ui;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System.IO;
using Serilog;

namespace IronworksTranslator.Services
{
    /// <summary>
    /// Managed host of the application.
    /// </summary>
    public class ApplicationHostService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        private INavigationWindow _navigationWindow;

        public ApplicationHostService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            LoadSettings();
        }

        [TraceMethod]
        private static void LoadSettings()
        {
            var fileName = "settings.yaml";
            if (!File.Exists(fileName))
            {
                var settings = IronworksSettings.CreateDefault();
                IronworksSettings.Instance = settings;
                IronworksSettings.UpdateSettingsFile(settings);
            }
            else
            {
                var deserializer = new DeserializerBuilder()
                                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                                    .Build();
                var settings = deserializer.Deserialize<IronworksSettings>(
                    File.ReadAllText("settings.yaml")
                );
                IronworksSettings.Instance = settings;
                if (IronworksSettings.IsSettingsFileInValid(settings))
                {
                    Log.Error("Failed to load settings.");
                    if (MessageBox.Show(Localizer.GetString("app.settings.failed_to_load"), "Error", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        settings = IronworksSettings.CreateDefault();
                        IronworksSettings.Instance = settings;
                        IronworksSettings.UpdateSettingsFile(settings);
                    }
                    else
                    {
                        App.RequestShutdown();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await HandleActivationAsync();
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Creates main window during activation.
        /// </summary>
        private async Task HandleActivationAsync()
        {
            if (!Application.Current.Windows.OfType<MainWindow>().Any())
            {
                _navigationWindow = (
                    _serviceProvider.GetService(typeof(INavigationWindow)) as INavigationWindow
                )!;
                _navigationWindow!.ShowWindow();

                _navigationWindow.Navigate(typeof(Views.Pages.DashboardPage));
            }

            await Task.CompletedTask;
        }
    }
}
