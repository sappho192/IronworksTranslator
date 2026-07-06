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
}
