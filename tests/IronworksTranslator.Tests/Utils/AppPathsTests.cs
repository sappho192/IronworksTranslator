using IronworksTranslator.Utils;
using System.IO;

namespace IronworksTranslator.Tests.Utils;

public class AppPathsTests
{
    [Fact]
    public void SettingsFilePath_UsesCurrentSchemaVersion()
    {
        Assert.EndsWith(
            $"settings.v{AppPaths.SettingsSchemaVersion}.yaml",
            AppPaths.SettingsFilePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            "settings.yaml",
            AppPaths.LegacySettingsFilePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(AppPaths.LegacySettingsFilePath, AppPaths.SettingsFilePath);
    }

    [Fact]
    public void FindSettingsMigrationSourcePath_UsesNewestEarlierSchemaWithoutChangingIt()
    {
        var directory = CreateTempDirectory();

        try
        {
            var legacyPath = Path.Combine(directory, "settings.yaml");
            var version2Path = AppPaths.GetSettingsFilePath(directory, 2);
            var version3Path = AppPaths.GetSettingsFilePath(directory, 3);
            File.WriteAllText(legacyPath, "legacy");
            File.WriteAllText(version2Path, "version 2");
            File.WriteAllText(version3Path, "version 3");

            var source = AppPaths.FindSettingsMigrationSourcePath(
                directory,
                currentSchemaVersion: 4);

            Assert.Equal(version3Path, source);
            Assert.Equal("legacy", File.ReadAllText(legacyPath));
            Assert.Equal("version 2", File.ReadAllText(version2Path));
            Assert.Equal("version 3", File.ReadAllText(version3Path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FindSettingsMigrationSourcePath_FallsBackToUnversionedLegacySettings()
    {
        var directory = CreateTempDirectory();

        try
        {
            var legacyPath = Path.Combine(directory, "settings.yaml");
            File.WriteAllText(legacyPath, "legacy");

            var source = AppPaths.FindSettingsMigrationSourcePath(
                directory,
                currentSchemaVersion: 2);

            Assert.Equal(legacyPath, source);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"IronworksTranslator.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
