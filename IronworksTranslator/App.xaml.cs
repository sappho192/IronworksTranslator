using System;
using System.IO;
using System.Threading.Tasks;
using IronworksTranslator.Core;
using System.Windows;
using IronworksTranslator.Util;
using Serilog;
using Serilog.Formatting.Compact;

namespace IronworksTranslator
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public static readonly string Birthdate = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        public static bool makeMiniDump = false;

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

            /* Crash reporter initialization */
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            /* Logger initialization */
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(formatter: new CompactJsonFormatter(),
                    path: $"./logs/log-{Birthdate}.txt",
                    retainedFileCountLimit: null)
                .MinimumLevel.Debug()
                .CreateLogger();

            Log.Debug("Program started");
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            SaveMiniDump(e.Exception);
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            SaveMiniDump(e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            SaveMiniDump((Exception)e.ExceptionObject);
        }

        public static void SaveMiniDump(Exception exception)
        {
            if (makeMiniDump)
            {
                using (FileStream fs = new FileStream($"./logs/log-{Birthdate}.mdmp", FileMode.Create, FileAccess.ReadWrite, FileShare.Write))
                {
                    MiniDump.Write(fs.SafeFileHandle, MiniDump.Option.WithFullMemory, MiniDump.ExceptionInfo.Present);
                }
            }
            else
            {
                if (exception.InnerException != null)
                {
                    Log.Fatal("TERMINATED BY UNHANDLED EXCEPTION: {@Exception}, {@InnerException}, {@GroundZero}",
                        exception,
                        exception.InnerException,
                        exception.InnerException.StackTrace);
                }
                else
                {
                    Log.Fatal("TERMINATED BY UNHANDLED EXCEPTION: {@Exception}, {@GroundZero}",
                        exception,
                        exception.StackTrace);
                }
            }
        }
    }
}
