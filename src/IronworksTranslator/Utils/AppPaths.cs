using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;
using Serilog;
using System.IO;

namespace IronworksTranslator.Utils
{
    public static class AppPaths
    {
        private const string AppFolderName = "IronworksTranslator";
        // Increment this before shipping any persisted change that an older
        // application may not deserialize. Never reuse an existing schema file.
        internal const int SettingsSchemaVersion = 2;

        public static string RoamingAppDataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName);

        public static string LocalAppDataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

        public static string LegacySettingsFilePath { get; } = Path.Combine(
            RoamingAppDataDirectory,
            "settings.yaml");

        public static string SettingsFilePath { get; } = GetSettingsFilePath(
            RoamingAppDataDirectory,
            SettingsSchemaVersion);

        public static string LogsDirectory { get; } = Path.Combine(
            LocalAppDataDirectory,
            "logs");

        public static string DataDirectory { get; } = Path.Combine(
            LocalAppDataDirectory,
            "data");

        public static string SharlayanCacheDirectory { get; } = Path.Combine(
            DataDirectory,
            "sharlayan");

        public static string ModelDirectory { get; } = Path.Combine(
            DataDirectory,
            "model");

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
                    MiLMMTModelSize.MiLMMT_4B => "milmmt-46-4b-v0.1",
                    MiLMMTModelSize.MiLMMT_12B => "milmmt-46-12b-v0.1",
                    MiLMMTModelSize.MiLMMT_1B_Compact => "milmmt-46-1b-v0.1",
                    _ => "milmmt-46-1b-v0.1",
                });
        }

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(RoamingAppDataDirectory);
            Directory.CreateDirectory(LocalAppDataDirectory);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(SharlayanCacheDirectory);
        }

        public static void MigrateLegacyUserData()
        {
            EnsureDirectories();

            foreach (var baseDirectory in GetLegacyBaseDirectories())
            {
                MigrateLegacyFile(Path.Combine(baseDirectory, "settings.yaml"), LegacySettingsFilePath);
                MigrateLegacyMiLMMTModels(baseDirectory);
            }
        }

        public static string? FindSettingsMigrationSourcePath()
        {
            return FindSettingsMigrationSourcePath(
                RoamingAppDataDirectory,
                SettingsSchemaVersion);
        }

        internal static string GetSettingsFilePath(string directory, int schemaVersion)
        {
            if (schemaVersion < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    "Versioned settings schemas start at version 2.");
            }

            return Path.Combine(directory, $"settings.v{schemaVersion}.yaml");
        }

        internal static string? FindSettingsMigrationSourcePath(
            string directory,
            int currentSchemaVersion)
        {
            for (var schemaVersion = currentSchemaVersion - 1; schemaVersion >= 2; schemaVersion--)
            {
                var candidate = GetSettingsFilePath(directory, schemaVersion);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var legacyCandidate = Path.Combine(directory, "settings.yaml");
            return File.Exists(legacyCandidate) ? legacyCandidate : null;
        }

        private static void MigrateLegacyMiLMMTModels(string baseDirectory)
        {
            foreach (var profile in MiLMMTModelProfiles.AllKnown)
            {
                var sourceDirectory = Path.Combine(baseDirectory, "data", "model", Path.GetFileName(profile.DirectoryPath));
                MigrateLegacyDirectory(sourceDirectory, profile.DirectoryPath);
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
