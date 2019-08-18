using System;
using IronworksTranslator.Core;
using System.Windows;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace IronworksTranslator
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if(IronworksContext.Instance().Attached)
            {
                IronworksContext.driver.Dispose();
            }
            Log.Debug("Closing program");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(formatter: new CompactJsonFormatter(),
                    path: $"log-{timestamp}.txt",
                    retainedFileCountLimit: null)
                .MinimumLevel.Debug()
                .CreateLogger();

            Log.Debug("Program started");
        }
    }
}
