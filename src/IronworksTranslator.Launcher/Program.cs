using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using Velopack;

namespace IronworksTranslator.Launcher
{
    internal static class Program
    {
        private const string MainExeName = "IronworksTranslator.exe";
        private const int ErrorCancelled = 1223;

        [STAThread]
        private static int Main(string[] args)
        {
            VelopackApp.Build()
                .SetArgs(args)
                .Run();

            var mainExePath = Path.Combine(AppContext.BaseDirectory, MainExeName);
            if (!File.Exists(mainExePath))
            {
                ShowError(
                    "IronworksTranslator could not be started because the main executable is missing.",
                    $"Missing file: {mainExePath}");
                return 1;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = mainExePath,
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas",
                });
                return 0;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
            {
                MessageBox.Show(
                    "Administrator permission was not granted. IronworksTranslator was not started.",
                    "IronworksTranslator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                ShowError(
                    "IronworksTranslator could not be started with administrator permission.",
                    ex.Message);
                return 1;
            }
        }

        private static void ShowError(string message, string detail)
        {
            MessageBox.Show(
                $"{message}{Environment.NewLine}{Environment.NewLine}{detail}",
                "IronworksTranslator",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
