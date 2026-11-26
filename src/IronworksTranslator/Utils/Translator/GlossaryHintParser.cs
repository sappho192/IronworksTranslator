using IronworksTranslator.Models.Cloudflare;

namespace IronworksTranslator.Utils.Translator
{
    public static class GlossaryHintParser
    {
        public const int MaxTerms = 20;

        private static readonly string[] Separators = ["->", "=>", "→", ":"];

        public static IReadOnlyList<BiggsGlossaryTerm> Parse(string? rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return [];
            }

            var results = new List<BiggsGlossaryTerm>();
            var seen = new HashSet<(string Source, string Target)>();
            var lines = rawInput.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                if (results.Count >= MaxTerms)
                {
                    break;
                }

                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!TrySplitPair(line, out var source, out var target))
                {
                    continue;
                }

                if (seen.Add((source, target)))
                {
                    results.Add(new BiggsGlossaryTerm
                    {
                        Source = source,
                        Target = target,
                    });
                }
            }

            return results;
        }

        private static bool TrySplitPair(string line, out string source, out string target)
        {
            foreach (var separator in Separators)
            {
                var index = line.IndexOf(separator, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                source = line[..index].Trim();
                target = line[(index + separator.Length)..].Trim();
                if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target))
                {
                    return true;
                }

                return false;
            }

            source = string.Empty;
            target = string.Empty;
            return false;
        }
    }
}
