using IronworksTranslator.Utils;

namespace IronworksTranslator.Tests.Utils;

public class BuildInfoTests
{
    [Fact]
    public void ReleaseChannel_UsesExpectedBuildDefaults()
    {
        var channel = BuildInfo.ReleaseChannel;

        Assert.True(channel.DisplayName is "Stable" or "Beta");
        if (channel.DisplayName == "Beta")
        {
            Assert.Equal("beta", channel.VelopackChannel);
            Assert.True(channel.IncludePrereleases);
            return;
        }

        Assert.Equal("win", channel.VelopackChannel);
        Assert.False(channel.IncludePrereleases);
    }

    [Theory]
    [InlineData("2.2.0", "2.2.0")]
    [InlineData("2.2.0+Branch.master.Sha.abc123", "2.2.0")]
    [InlineData("2.2.0-beta.2+Sha.abc123", "2.2.0-beta.2")]
    [InlineData("", "2.1.0")]
    [InlineData(null, "2.1.0")]
    public void ReleaseVersion_IgnoresBuildMetadataAndKeepsPrerelease(string? informationalVersion, string expected)
    {
        Assert.Equal(expected, BuildInfo.GetReleaseVersion(informationalVersion, new Version(2, 1, 0, 0)));
    }
}
