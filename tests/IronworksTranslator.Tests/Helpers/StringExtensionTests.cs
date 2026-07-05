using IronworksTranslator.Helpers.Extensions;

namespace IronworksTranslator.Tests.Helpers;

public class StringExtensionTests
{
    [Theory]
    [InlineData("Author: message", ":", "Author")]
    [InlineData(": message", ":", ": message")]
    [InlineData("No delimiter", ":", "No delimiter")]
    public void RemoveAfter_RemovesTextAfterFirstDelimiter(string value, string delimiter, string expected)
    {
        Assert.Equal(expected, value.RemoveAfter(delimiter));
    }

    [Theory]
    [InlineData("Author: message", ":", " message")]
    [InlineData(": message", ":", ": message")]
    [InlineData("No delimiter", ":", "No delimiter")]
    public void RemoveBefore_RemovesTextBeforeFirstDelimiter(string value, string delimiter, string expected)
    {
        Assert.Equal(expected, value.RemoveBefore(delimiter));
    }

    [Theory]
    [InlineData("안녕하세요", true)]
    [InlineData("ㄱㄴㄷ", true)]
    [InlineData("hello", false)]
    public void HasKorean_DetectsHangulRanges(string value, bool expected)
    {
        Assert.Equal(expected, value.HasKorean());
    }

    [Theory]
    [InlineData("こんにちは", true)]
    [InlineData("カタカナ", true)]
    [InlineData("漢字", true)]
    [InlineData("hello", false)]
    public void HasJapanese_DetectsJapaneseRanges(string value, bool expected)
    {
        Assert.Equal(expected, value.HasJapanese());
    }

    [Theory]
    [InlineData("hello", true)]
    [InlineData("HELLO", true)]
    [InlineData("12345", false)]
    public void HasEnglish_DetectsAsciiLetters(string value, bool expected)
    {
        Assert.Equal(expected, value.HasEnglish());
    }
}
