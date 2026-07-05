using IronworksTranslator.Models.Enums;
using Serilog;
using System.IO;

namespace IronworksTranslator.Utils
{
    public static class AppPaths
    {
        private const string AppFolderName = "IronworksTranslator";

        public static string RoamingAppDataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName);

        public static string LocalAppDataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

        public static string SettingsFilePath { get; } = Path.Combine(
            RoamingAppDataDirectory,
            "settings.yaml");

        public static string LogsDirectory { get; } = Path.Combine(
            LocalAppDataDirectory,
            "logs");

        public static string DataDirectory { get; } = Path.Combine(
            LocalAppDataDirectory,
            "data");

        public static string ModelDirectory { get; } = Path.Combine(
            DataDirectory,
            "model");

        public static string AihubJaKoModelDirectory { get; } = Path.Combine(
            ModelDirectory,
            "aihub-ja-ko-translator");

        public static string MiLMMTModelDirectory { get; } = Path.Combine(
            ModelDirectory,
            "milmmt-46-1b-v0.1");

        public static string MiLMMTModelPath { get; } = Path.Combine(
            MiLMMTModelDirectory,
            "MiLMMT-46-1B-v0.1.Q8_0.gguf");

        public static string GetMiLMMTModelDirectory(MiLMMTModelSize modelSize)
        {
            return Path.Combine(
                ModelDirectory,
                modelSize switch
                {
                    MiLMMTModelSize.MiLLMT_4B => "milmmt-46-4b-v0.1",
                    MiLMMTModelSize.MiLLMT_12B => "milmmt-46-12b-v0.1",
                    _ => "milmmt-46-1b-v0.1",
                });
        }

        public static string TokenizersDirectory { get; } = Path.Combine(
            DataDirectory,
            "tokenizers");

        public static string BundledDataDirectory { get; } = Path.Combine(
            AppContext.BaseDirectory,
            "data");

        public static string BundledUnidicDirectory { get; } = Path.Combine(
            BundledDataDirectory,
            "unidic-mecab-2.1.2_bin");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(RoamingAppDataDirectory);
            Directory.CreateDirectory(LocalAppDataDirectory);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(DataDirectory);
        }

        public static void MigrateLegacyUserData()
        {
            EnsureDirectories();

            foreach (var baseDirectory in GetLegacyBaseDirectories())
            {
                MigrateLegacyFile(Path.Combine(baseDirectory, "settings.yaml"), SettingsFilePath);
                MigrateLegacyDirectory(Path.Combine(baseDirectory, "data", "model"), ModelDirectory);
                MigrateLegacyDirectory(Path.Combine(baseDirectory, "data", "tokenizers"), TokenizersDirectory);
            }
        }

        private static IEnumerable<string> GetLegacyBaseDirectories()
        {
            return new[]
                {
                    AppContext.BaseDirectory,
                    Environment.CurrentDirectory,
                }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void MigrateLegacyFile(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath) || File.Exists(destinationPath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath);
            Log.Information(
                "Migrated legacy user file from {SourcePath} to {DestinationPath}",
                sourcePath,
                destinationPath);
        }

        private static void MigrateLegacyDirectory(string sourcePath, string destinationPath)
        {
            if (!Directory.Exists(sourcePath) || DirectoryHasFiles(destinationPath))
            {
                return;
            }

            CopyDirectory(sourcePath, destinationPath);
            Log.Information(
                "Migrated legacy user directory from {SourcePath} to {DestinationPath}",
                sourcePath,
                destinationPath);
        }

        private static bool DirectoryHasFiles(string path)
        {
            return Directory.Exists(path)
                && Directory.EnumerateFileSystemEntries(path).Any();
        }

        private static void CopyDirectory(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var directoryPath in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourcePath, directoryPath);
                Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
            }

            foreach (var filePath in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourcePath, filePath);
                var destinationFilePath = Path.Combine(destinationPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath)!);
                File.Copy(filePath, destinationFilePath, overwrite: false);
            }
        }
    }
}
