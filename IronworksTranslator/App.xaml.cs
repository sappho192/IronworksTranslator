using System;
using System.IO;
using System.Threading.Tasks;
using IronworksTranslator.Core;
using System.Windows;
using IronworksTranslator.Util;
using Serilog;
using Serilog.Formatting.Compact;
using Newtonsoft.Json;

namespace IronworksTranslator
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public static readonly string Birthdate = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        public static bool makeMiniDump = false;
        public static bool newcomer = false;

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if(IronworksContext.Instance().Attached)
            {
                IronworksContext.Instance().webBrowser.Dispose();
            }
            Log.Debug("Closing program");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            /* Crash reporter initialization */
            InitWatchDog();

            /* Logger initialization */
            InitLogger();

            /* Find or create settings file */
            InitSettings();
        }

        private void InitWatchDog()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private static void InitSettings()
        {
            Directory.CreateDirectory("settings");
            string settingsFilePath = "./settings/settings.json";
            if (File.Exists(settingsFilePath))
            {// Read settings
                using (StreamReader reader = File.OpenText(settingsFilePath))
                {
                    var settings = reader.ReadToEnd();
                    var previousSettings = JsonConvert.DeserializeObject<IronworksSettings>(settings);
                    IronworksSettings.Instance = previousSettings;
                    Log.Debug("settings.json loaded");
                }
            }
            else
            {// Create new one
                newcomer = true;
                var ironworksSettings = new IronworksSettings();
                IronworksSettings.Instance = ironworksSettings;
                string settings = JsonConvert.SerializeObject(ironworksSettings, Formatting.Indented);
                File.WriteAllText(settingsFilePath, settings);
                Log.Debug("settings.json created");
            }
        }

        private static void InitLogger()
        {
            Log.Logger = new LoggerConfiguration()
                            .WriteTo.File(formatter: new CompactJsonFormatter(),
                                path: $"./logs/log-{Birthdate}.txt",
                                retainedFileCountLimit: null)
                            .MinimumLevel.Debug()
                            .CreateLogger();
            Log.Debug("Logger initialized");
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
                using FileStream fs = new($"./logs/log-{Birthdate}.mdmp", FileMode.Create, FileAccess.ReadWrite, FileShare.Write);
                MiniDump.Write(fs.SafeFileHandle, MiniDump.Option.WithFullMemory, MiniDump.ExceptionInfo.Present);
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
