using IronworksTranslator.ViewModels.Pages;

namespace IronworksTranslator.Tests.ViewModels;

public class SettingsViewModelTests
{
    [Theory]
    [InlineData("1.2.1", "1.2.1")]
    [InlineData("1.2.1-beta.1", "1.2.1-beta.1")]
    [InlineData("1.2.1-beta.1+Branch.master.Sha.abc123", "1.2.1-beta.1")]
    public void FormatDisplayVersion_RemovesBuildMetadata(
        string informationalVersion,
        string expected)
    {
        Assert.Equal(expected, SettingsViewModel.FormatDisplayVersion(informationalVersion));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void FormatDisplayVersion_ReturnsNullForEmptyVersion(string informationalVersion)
    {
        Assert.Null(SettingsViewModel.FormatDisplayVersion(informationalVersion));
    }

    [Theory]
    [InlineData(0, "0B")]
    [InlineData(1, "1Bytes")]
    [InlineData(1024, "1KB")]
    [InlineData(1048576, "1MB")]
    public void FormatBytes_UsesReadableUnits(long bytes, string expected)
    {
        Assert.Equal(expected, SettingsViewModel.FormatBytes(bytes));
    }
}
