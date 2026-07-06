using Serilog;
using System.IO;

namespace IronworksTranslator.Utils.Logging
{
    internal static class LegacyLogMigrator
    {
        public static void MigrateLegacyTextLogs(string logsDirectory)
        {
            if (!Directory.Exists(logsDirectory))
            {
                return;
            }

            foreach (var filePath in Directory.EnumerateFiles(logsDirectory, "*.txt", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);
                    Log.Information(
                        "Migrating legacy plaintext log {LegacyLogFileName}. Contents follow:{NewLine}{LegacyLogContents}",
                        fileName,
                        Environment.NewLine,
                        File.ReadAllText(filePath));
                    File.Delete(filePath);
                    Log.Information("Deleted legacy plaintext log {LegacyLogFileName}.", fileName);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to migrate legacy plaintext log {LegacyLogFilePath}.", filePath);
                }
            }
        }
    }
}
