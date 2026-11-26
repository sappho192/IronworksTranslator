using IronworksTranslator.Utils.Translator;

namespace IronworksTranslator.Tests.Utils;

public class GlossaryHintParserTests
{
    [Fact]
    public void Parse_ReturnsEmptyForBlankInput()
    {
        Assert.Empty(GlossaryHintParser.Parse(null));
        Assert.Empty(GlossaryHintParser.Parse(""));
        Assert.Empty(GlossaryHintParser.Parse("   \r\n  "));
    }

    [Fact]
    public void Parse_SupportsCommonSeparatorsAndDeduplicates()
    {
        const string input = """
            暁の血盟 -> 새벽의 혈맹
            G'raha Tia => 그라하 티아
            Eorzea → 에오르제아
            Scion: 혈맹원
            暁の血盟 -> 새벽의 혈맹
            invalid line without separator
            -> missing source
            missing target ->
            """;

        var terms = GlossaryHintParser.Parse(input);

        Assert.Equal(4, terms.Count);
        Assert.Equal(("暁の血盟", "새벽의 혈맹"), (terms[0].Source, terms[0].Target));
        Assert.Equal(("G'raha Tia", "그라하 티아"), (terms[1].Source, terms[1].Target));
        Assert.Equal(("Eorzea", "에오르제아"), (terms[2].Source, terms[2].Target));
        Assert.Equal(("Scion", "혈맹원"), (terms[3].Source, terms[3].Target));
    }

    [Fact]
    public void Parse_CapsAtMaxTerms()
    {
        var lines = Enumerable.Range(1, GlossaryHintParser.MaxTerms + 5)
            .Select(i => $"term{i} -> 번역{i}");

        var terms = GlossaryHintParser.Parse(string.Join("\n", lines));

        Assert.Equal(GlossaryHintParser.MaxTerms, terms.Count);
        Assert.Equal("term1", terms[0].Source);
        Assert.Equal($"term{GlossaryHintParser.MaxTerms}", terms[^1].Source);
    }
}
