using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using System.IO;

namespace IronworksTranslator.Tests.Services;

public class VelopackPackageCleanupTests
{
    [Fact]
    public void CleanPackagesExcept_RetainsSpecifiedPackageAndRemovesObsoletePackages()
    {
        var packagesDirectory = CreateTempDirectory();

        try
        {
            const string packageToKeep = "IronworksTranslator-2.0.0-full.nupkg";
            var olderPackage = Path.Combine(packagesDirectory, "IronworksTranslator-1.0.0-full.nupkg");
            var deltaPackage = Path.Combine(packagesDirectory, "IronworksTranslator-2.0.0-delta.nupkg");
            var partialDownload = Path.Combine(packagesDirectory, "IronworksTranslator-3.0.0-full.nupkg.partial");
            var unrelatedFile = Path.Combine(packagesDirectory, "keep.txt");

            File.WriteAllText(Path.Combine(packagesDirectory, packageToKeep), "newest");
            File.WriteAllText(olderPackage, "older");
            File.WriteAllText(deltaPackage, "delta");
            File.WriteAllText(partialDownload, "partial");
            File.WriteAllText(unrelatedFile, "unrelated");

            var updateManager = CreateUpdateManager(packagesDirectory);
            updateManager.CleanPackagesExcept(Path.Combine(packagesDirectory, packageToKeep));

            Assert.True(File.Exists(Path.Combine(packagesDirectory, packageToKeep)));
            Assert.False(File.Exists(olderPackage));
            Assert.False(File.Exists(deltaPackage));
            Assert.False(File.Exists(partialDownload));
            Assert.True(File.Exists(unrelatedFile));
        }
        finally
        {
            Directory.Delete(packagesDirectory, recursive: true);
        }
    }

    [Fact]
    public void CleanPackagesExcept_WithNoPackageToKeep_RemovesAllDownloadedPackages()
    {
        var packagesDirectory = CreateTempDirectory();

        try
        {
            var fullPackage = Path.Combine(packagesDirectory, "IronworksTranslator-1.0.0-full.nupkg");
            var partialDownload = Path.Combine(packagesDirectory, "IronworksTranslator-2.0.0-full.nupkg.partial");

            File.WriteAllText(fullPackage, "full");
            File.WriteAllText(partialDownload, "partial");

            var updateManager = CreateUpdateManager(packagesDirectory);
            updateManager.CleanPackagesExcept(null);

            Assert.False(File.Exists(fullPackage));
            Assert.False(File.Exists(partialDownload));
        }
        finally
        {
            Directory.Delete(packagesDirectory, recursive: true);
        }
    }

    private static TestUpdateManager CreateUpdateManager(string packagesDirectory)
    {
        var locator = new TestVelopackLocator(
            "Sappho192.IronworksTranslator",
            "1.0.0",
            packagesDirectory,
            NullVelopackLogger.Instance);

        return new TestUpdateManager(locator);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"IronworksTranslator.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class TestUpdateManager : UpdateManager
    {
        public TestUpdateManager(IVelopackLocator locator)
            : base("https://example.invalid", new UpdateOptions(), locator)
        {
        }

        public new void CleanPackagesExcept(string? assetToKeep)
        {
            base.CleanPackagesExcept(assetToKeep);
        }
    }
}
