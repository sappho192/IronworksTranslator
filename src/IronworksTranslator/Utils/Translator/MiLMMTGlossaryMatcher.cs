using IronworksTranslator.Models.Enums;
using System.Text.Json;

namespace IronworksTranslator.Utils.Translator
{
    public sealed record MiLMMTGlossaryTerm(string Source, string Target);

    /// <summary>
    /// Fast, immutable, boundary-safe runtime glossary matcher for MiLMMT prompts.
    /// Matches the exact longest-match, Latin word-boundary, and Katakana boundary rules
    /// used during Glossary-Conditioned LoRA SFT dataset construction.
    /// </summary>
    public sealed class MiLMMTGlossaryMatcher
    {
        public const int DefaultMaxTerms = 8;

        public static MiLMMTGlossaryMatcher Empty { get; } = new(
            string.Empty,
            new Dictionary<(TranslationLanguageCode, TranslationLanguageCode), PairIndex>(),
            0);

        private readonly IReadOnlyDictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), PairIndex> _indices;

        private MiLMMTGlossaryMatcher(
            string version,
            IReadOnlyDictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), PairIndex> indices,
            int termCount)
        {
            Version = version;
            _indices = indices;
            TermCount = termCount;
        }

        public string Version { get; }
        public int TermCount { get; }

        public static bool TryFromJson(string json, out MiLMMTGlossaryMatcher matcher)
        {
            matcher = Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                matcher = FromJson(json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static MiLMMTGlossaryMatcher FromJson(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Glossary root must be a JSON object.");
            }

            var version = root.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == JsonValueKind.String
                ? versionProp.GetString()?.Trim() ?? string.Empty
                : string.Empty;

            if (!root.TryGetProperty("pairs", out var pairsProp) || pairsProp.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Glossary JSON must contain a 'pairs' object.");
            }

            var indices = new Dictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), PairIndex>();
            var totalTerms = 0;

            foreach (var pairProperty in pairsProp.EnumerateObject())
            {
                if (!TryParsePairKey(pairProperty.Name, out var sourceLanguage, out var targetLanguage))
                {
                    continue;
                }

                if (pairProperty.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var caseInsensitive = IsLatinScriptLanguage(sourceLanguage);
                var minSourceLength = sourceLanguage is TranslationLanguageCode.Japanese or TranslationLanguageCode.Korean ? 2 : 3;
                var buckets = new Dictionary<string, List<IndexedTerm>>(StringComparer.Ordinal);
                var pairCount = 0;

                foreach (var item in pairProperty.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var source = item.TryGetProperty("source", out var srcProp) && srcProp.ValueKind == JsonValueKind.String
                        ? srcProp.GetString()?.Trim()
                        : null;
                    var target = item.TryGetProperty("target", out var tgtProp) && tgtProp.ValueKind == JsonValueKind.String
                        ? tgtProp.GetString()?.Trim()
                        : null;

                    if (string.IsNullOrWhiteSpace(source)
                        || string.IsNullOrWhiteSpace(target)
                        || source.Length < minSourceLength
                        || string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var matchKey = caseInsensitive ? source.ToLowerInvariant() : source;
                    var prefix = matchKey[..2];
                    if (!buckets.TryGetValue(prefix, out var list))
                    {
                        list = [];
                        buckets[prefix] = list;
                    }

                    list.Add(new IndexedTerm(
                        Source: source,
                        Target: target,
                        MatchKey: matchKey,
                        StartsWithLatinAlnum: IsLatinAlnum(matchKey[0]),
                        EndsWithLatinAlnum: IsLatinAlnum(matchKey[^1]),
                        StartsWithKatakana: IsKatakana(matchKey[0]),
                        EndsWithKatakana: IsKatakana(matchKey[^1])));
                    pairCount++;
                }

                if (pairCount > 0)
                {
                    foreach (var bucket in buckets.Values)
                    {
                        bucket.Sort((a, b) => b.MatchKey.Length.CompareTo(a.MatchKey.Length));
                    }

                    indices[(sourceLanguage, targetLanguage)] = new PairIndex(caseInsensitive, buckets);
                    totalTerms += pairCount;
                }
            }

            return new MiLMMTGlossaryMatcher(version, indices, totalTerms);
        }

        public IReadOnlyList<MiLMMTGlossaryTerm> Match(
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string? input,
            int maxTerms = DefaultMaxTerms)
        {
            if (maxTerms <= 0
                || string.IsNullOrWhiteSpace(input)
                || input.Length < 2
                || !_indices.TryGetValue((sourceLanguage, targetLanguage), out var pairIndex))
            {
                return [];
            }

            var haystack = pairIndex.CaseInsensitive ? input.ToLowerInvariant() : input;
            var candidates = new List<MatchCandidate>();

            for (var i = 0; i <= haystack.Length - 2; i++)
            {
                var prefix = haystack.Substring(i, 2);
                if (!pairIndex.Buckets.TryGetValue(prefix, out var bucket))
                {
                    continue;
                }

                foreach (var term in bucket)
                {
                    var length = term.MatchKey.Length;
                    if (i + length > haystack.Length)
                    {
                        continue;
                    }

                    if (!haystack.AsSpan(i, length).Equals(term.MatchKey.AsSpan(), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!PassesBoundaryCheck(haystack, i, length, term))
                    {
                        continue;
                    }

                    candidates.Add(new MatchCandidate(i, i + length, length, term.Source, term.Target));
                }
            }

            if (candidates.Count == 0)
            {
                return [];
            }

            // Longest match first, then earliest position; resolve overlaps greedily.
            candidates.Sort((a, b) =>
            {
                var lenCmp = b.Length.CompareTo(a.Length);
                return lenCmp != 0 ? lenCmp : a.Start.CompareTo(b.Start);
            });

            var selected = new List<MatchCandidate>(Math.Min(maxTerms, candidates.Count));
            var seenSources = new HashSet<string>(
                pairIndex.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            foreach (var candidate in candidates)
            {
                if (selected.Count >= maxTerms)
                {
                    break;
                }

                if (OverlapsAny(selected, candidate))
                {
                    continue;
                }

                if (!seenSources.Add(candidate.Source))
                {
                    continue;
                }

                selected.Add(candidate);
            }

            // Preserve left-to-right appearance order in the prompt Glossary block.
            selected.Sort((a, b) => a.Start.CompareTo(b.Start));

            var results = new MiLMMTGlossaryTerm[selected.Count];
            for (var i = 0; i < selected.Count; i++)
            {
                results[i] = new MiLMMTGlossaryTerm(selected[i].Source, selected[i].Target);
            }

            return results;
        }

        private static bool OverlapsAny(List<MatchCandidate> selected, MatchCandidate candidate)
        {
            foreach (var existing in selected)
            {
                if (candidate.Start < existing.End && existing.Start < candidate.End)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PassesBoundaryCheck(string text, int start, int length, IndexedTerm term)
        {
            var end = start + length;

            if (term.StartsWithLatinAlnum && start > 0 && IsLatinAlnum(text[start - 1]))
            {
                return false;
            }

            if (term.EndsWithLatinAlnum && end < text.Length && IsLatinAlnum(text[end]))
            {
                return false;
            }

            if (term.StartsWithKatakana && start > 0 && IsKatakana(text[start - 1]))
            {
                return false;
            }

            if (term.EndsWithKatakana && end < text.Length && IsKatakana(text[end]))
            {
                return false;
            }

            return true;
        }

        private static bool IsLatinScriptLanguage(TranslationLanguageCode language)
        {
            return language is TranslationLanguageCode.English
                or TranslationLanguageCode.German
                or TranslationLanguageCode.French;
        }

        private static bool IsLatinAlnum(char ch)
        {
            return char.IsAsciiLetterOrDigit(ch)
                || (ch >= '\u00C0' && ch <= '\u024F' && char.IsLetter(ch));
        }

        private static bool IsKatakana(char ch)
        {
            return (ch >= '\u30A0' && ch <= '\u30FF') || ch == '\u30FC';
        }

        private static bool TryParsePairKey(
            string key,
            out TranslationLanguageCode sourceLanguage,
            out TranslationLanguageCode targetLanguage)
        {
            sourceLanguage = default;
            targetLanguage = default;

            var separatorIndex = key.IndexOf("->", StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex >= key.Length - 2)
            {
                return false;
            }

            var sourcePart = key[..separatorIndex].Trim();
            var targetPart = key[(separatorIndex + 2)..].Trim();
            return TryParseLanguageName(sourcePart, out sourceLanguage)
                && TryParseLanguageName(targetPart, out targetLanguage);
        }

        private static bool TryParseLanguageName(string name, out TranslationLanguageCode language)
        {
            switch (name)
            {
                case "Japanese":
                case "ja":
                    language = TranslationLanguageCode.Japanese;
                    return true;
                case "English":
                case "en":
                    language = TranslationLanguageCode.English;
                    return true;
                case "German":
                case "de":
                    language = TranslationLanguageCode.German;
                    return true;
                case "French":
                case "fr":
                    language = TranslationLanguageCode.French;
                    return true;
                case "Korean":
                case "ko":
                    language = TranslationLanguageCode.Korean;
                    return true;
                default:
                    language = default;
                    return false;
            }
        }

        private sealed record PairIndex(
            bool CaseInsensitive,
            Dictionary<string, List<IndexedTerm>> Buckets);

        private readonly record struct IndexedTerm(
            string Source,
            string Target,
            string MatchKey,
            bool StartsWithLatinAlnum,
            bool EndsWithLatinAlnum,
            bool StartsWithKatakana,
            bool EndsWithKatakana);

        private readonly record struct MatchCandidate(
            int Start,
            int End,
            int Length,
            string Source,
            string Target);
    }
}
