using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Tests.Utils;

public class DialogueSpeakerTranslationTests
{
    [Fact]
    public void TranslateOrOriginal_TranslatesSpeakerSeparately()
    {
        string? observedInput = null;

        var result = DialogueSpeakerTranslation.TranslateOrOriginal(
            "  Alphinaud  ",
            input =>
            {
                observedInput = input;
                return "  알피노  ";
            });

        Assert.Equal("Alphinaud", observedInput);
        Assert.Equal("알피노", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TranslateOrOriginal_EmptySpeakerSkipsTranslation(string? speaker)
    {
        var called = false;

        var result = DialogueSpeakerTranslation.TranslateOrOriginal(
            speaker,
            _ =>
            {
                called = true;
                return "translated";
            });

        Assert.False(called);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void TranslateOrOriginal_FailureKeepsOriginalSpeaker()
    {
        Exception? observedException = null;

        var result = DialogueSpeakerTranslation.TranslateOrOriginal(
            "Alphinaud",
            _ => throw new InvalidOperationException("translation failed"),
            ex => observedException = ex);

        Assert.Equal("Alphinaud", result);
        Assert.IsType<InvalidOperationException>(observedException);
    }

    [Fact]
    public async Task TranslateOrOriginalAsync_EmptyResultKeepsOriginalSpeaker()
    {
        var result = await DialogueSpeakerTranslation.TranslateOrOriginalAsync(
            "Alphinaud",
            (_, _) => Task.FromResult("   "),
            CancellationToken.None);

        Assert.Equal("Alphinaud", result);
    }

    [Fact]
    public async Task TranslateOrOriginalAsync_RequestedCancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DialogueSpeakerTranslation.TranslateOrOriginalAsync(
                "Alphinaud",
                (_, token) => Task.FromCanceled<string>(token),
                cancellation.Token));
    }
}
