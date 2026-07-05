using IronworksTranslator.Helpers;

namespace IronworksTranslator.Tests.Helpers;

public class LogScaleTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Invert_ReversesScale(double value)
    {
        var scale = new LogScale(0.01, 1.0);

        var roundTrip = scale.Invert(scale.Scale(value));

        Assert.Equal(value, roundTrip, precision: 10);
    }
}
