using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Tests.Utils;

public class PapagoTranslationSettlerTests
{
    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    // Sequences below mirror what Papago actually rendered (sampled every 100ms).

    [Fact]
    public void Observe_SkipsInterimResultAndReturnsFinal()
    {
        var settler = new PapagoTranslationSettler("レストエリアに入った！");

        Assert.Null(settler.Observe("", Ms(100)));
        Assert.Null(settler.Observe("레스토랑에 들어갔다!...", Ms(800)));
        Assert.Null(settler.Observe("레스토랑에 들어갔다!...", Ms(1200)));
        Assert.Null(settler.Observe("휴게 구역에 들어갔어!", Ms(1300)));
        Assert.Null(settler.Observe("휴게 구역에 들어갔어!", Ms(1500)));

        Assert.Equal("휴게 구역에 들어갔어!", settler.Observe("휴게 구역에 들어갔어!", Ms(1600)));
    }

    [Fact]
    public void Observe_RequiresTextToStayUnchangedForStableWindow()
    {
        var settler = new PapagoTranslationSettler("ありがとう", stableWindow: Ms(300));

        Assert.Null(settler.Observe("고마워.", Ms(1000)));
        Assert.Null(settler.Observe("고마워.", Ms(1299)));

        Assert.Equal("고마워.", settler.Observe("고마워.", Ms(1300)));
    }

    [Fact]
    public void Observe_IgnoresEmptyText()
    {
        var settler = new PapagoTranslationSettler("ありがとう");

        Assert.Null(settler.Observe(null, Ms(0)));
        Assert.Null(settler.Observe("   ", Ms(500)));
        Assert.Null(settler.Observe("", Ms(5000)));
        Assert.Null(settler.GetBestEffortResult());
    }

    [Fact]
    public void Observe_AcceptsUnchangedTextAfterLongerStability()
    {
        // Papago echoes numbers, names and already-translated text; that is an answer, not a failure.
        var settler = new PapagoTranslationSettler(
            "12345",
            stableWindow: Ms(300),
            interimStableWindow: Ms(1500));

        Assert.Null(settler.Observe("12345...", Ms(700)));
        Assert.Null(settler.Observe("12345", Ms(1300)));
        Assert.Null(settler.Observe("12345", Ms(1700)));

        Assert.Equal("12345", settler.Observe("12345", Ms(2800)));
    }

    [Fact]
    public void GetBestEffortResult_RejectsUnsettledEchoOfSource()
    {
        // An echo is only an answer once it is stable; an unsettled one may be a transient outage render.
        var settler = new PapagoTranslationSettler("Choco");
        settler.Observe("Choco", Ms(800));

        Assert.Null(settler.GetBestEffortResult());
    }

    [Fact]
    public void Observe_TextEndingWithEllipsisNeedsLongerStability()
    {
        var settler = new PapagoTranslationSettler(
            "これは…",
            stableWindow: Ms(300),
            interimStableWindow: Ms(1500));

        Assert.Null(settler.Observe("이건......", Ms(700)));
        Assert.Null(settler.Observe("이건......", Ms(1100)));
        Assert.Null(settler.Observe("이건......", Ms(2199)));

        Assert.Equal("이건......", settler.Observe("이건......", Ms(2200)));
    }

    [Fact]
    public void Observe_ChangeRestartsStabilityWindow()
    {
        var settler = new PapagoTranslationSettler("今日はいい天気ですね。", stableWindow: Ms(300));

        Assert.Null(settler.Observe("오늘은 날씨가", Ms(0)));
        Assert.Null(settler.Observe("오늘은 날씨가 좋네요。", Ms(250)));
        Assert.Null(settler.Observe("오늘은 날씨가 좋네요。", Ms(500)));

        Assert.Equal("오늘은 날씨가 좋네요。", settler.Observe("오늘은 날씨가 좋네요。", Ms(550)));
    }

    [Fact]
    public void GetBestEffortResult_StripsInterimMarker()
    {
        var settler = new PapagoTranslationSettler("シロガネ第24区");
        settler.Observe("흰가네 제24구...", Ms(800));

        Assert.Equal("흰가네 제24구", settler.GetBestEffortResult());
    }

    [Fact]
    public void GetBestEffortResult_KeepsEllipsisWhenSourceEndsWithEllipsis()
    {
        var settler = new PapagoTranslationSettler("待って...");
        settler.Observe("기다려...", Ms(800));

        Assert.Equal("기다려...", settler.GetBestEffortResult());
    }

    [Fact]
    public void GetBestEffortResult_ReturnsLastCandidateWhenItWasNotInterim()
    {
        var settler = new PapagoTranslationSettler("ありがとう");
        settler.Observe("고마워.", Ms(800));

        Assert.Equal("고마워.", settler.GetBestEffortResult());
    }

    [Fact]
    public void IsSameText_IgnoresWhitespace()
    {
        Assert.True(PapagoTranslator.IsSameText(" よろしく\nお願い ", "よろしくお願い"));
        Assert.False(PapagoTranslator.IsSameText("よろしく", "ありがとう"));
    }
}
