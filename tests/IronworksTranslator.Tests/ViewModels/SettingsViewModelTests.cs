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
}
