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
            new Dictionary<(TranslationLanguageCode, TranslationLanguageCode), Dictionary<string, string>>(),
            new Dictionary<(TranslationLanguageCode, TranslationLanguageCode), Dictionary<string, string>>(),
            0,
            0,
            0);

        private readonly IReadOnlyDictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), PairIndex> _indices;
        private readonly IReadOnlyDictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), Dictionary<string, string>> _npcNames;
        private readonly IReadOnlyDictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), Dictionary<string, string>> _npcStyles;

        private MiLMMTGlossaryMatcher(
            string version,
            IReadOnlyDictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), PairIndex> indices,
            IReadOnlyDictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), Dictionary<string, string>> npcNames,
            IReadOnlyDictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), Dictionary<string, string>> npcStyles,
            int termCount,
            int npcNameCount,
            int npcStyleCount)
        {
            Version = version;
            _indices = indices;
            _npcNames = npcNames;
            _npcStyles = npcStyles;
            TermCount = termCount;
            NpcNameCount = npcNameCount;
            NpcStyleCount = npcStyleCount;
        }

        public string Version { get; }
        public int TermCount { get; }
        public int NpcNameCount { get; }
        public int NpcStyleCount { get; }

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

                    var matchKey = NormalizeApostrophes(caseInsensitive ? source.ToLowerInvariant() : source);
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
                        EndsWithKatakana: IsKatakana(matchKey[^1]),
                        StartsWithHangul: IsHangulSyllable(matchKey[0]),
                        EndsWithHangul: IsHangulSyllable(matchKey[^1])));
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

            var (npcNames, totalNpcNames) = ParseNpcLookupMaps(root, "npc_names");
            var (npcStyles, totalNpcStyles) = ParseNpcLookupMaps(root, "npc_styles");

            return new MiLMMTGlossaryMatcher(version, indices, npcNames, npcStyles, totalTerms, totalNpcNames, totalNpcStyles);
        }

        private static (Dictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), Dictionary<string, string>> Maps, int TotalCount) ParseNpcLookupMaps(
            JsonElement root,
            string propertyName)
        {
            var maps = new Dictionary<(TranslationLanguageCode Source, TranslationLanguageCode Target), Dictionary<string, string>>();
            var totalCount = 0;

            if (!root.TryGetProperty(propertyName, out var sectionProp) || sectionProp.ValueKind != JsonValueKind.Object)
            {
                return (maps, totalCount);
            }

            foreach (var pairProperty in sectionProp.EnumerateObject())
            {
                if (!TryParsePairKey(pairProperty.Name, out var sourceLanguage, out var targetLanguage))
                {
                    continue;
                }

                if (pairProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var comparer = IsLatinScriptLanguage(sourceLanguage)
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;
                var map = new Dictionary<string, string>(comparer);

                foreach (var entry in pairProperty.Value.EnumerateObject())
                {
                    if (entry.Value.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var source = NormalizeNpcLookupKey(entry.Name);
                    var target = entry.Value.GetString()?.Trim();
                    if (string.IsNullOrEmpty(source) || string.IsNullOrWhiteSpace(target))
                    {
                        continue;
                    }

                    map.TryAdd(source, target);
                }

                if (map.Count > 0)
                {
                    maps[(sourceLanguage, targetLanguage)] = map;
                    totalCount += map.Count;
                }
            }

            return (maps, totalCount);
        }

        public bool TryResolveNpcName(
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string? speaker,
            out string officialName)
        {
            officialName = string.Empty;
            var key = NormalizeNpcLookupKey(speaker);
            if (key.Length == 0
                || !_npcNames.TryGetValue((sourceLanguage, targetLanguage), out var map)
                || !map.TryGetValue(key, out var resolved))
            {
                return false;
            }

            officialName = resolved;
            return true;
        }

        public bool TryResolveNpcStyle(
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string? speaker,
            out string speechStyle)
        {
            speechStyle = string.Empty;
            var key = NormalizeNpcLookupKey(speaker);
            if (key.Length == 0
                || !_npcStyles.TryGetValue((sourceLanguage, targetLanguage), out var map)
                || !map.TryGetValue(key, out var resolved))
            {
                return false;
            }

            speechStyle = resolved;
            return true;
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

            var haystack = NormalizeApostrophes(pairIndex.CaseInsensitive ? input.ToLowerInvariant() : input);
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

                    if (!TryPassBoundaryCheck(haystack, i, length, term, sourceLanguage, out var matchedLength))
                    {
                        continue;
                    }

                    candidates.Add(new MatchCandidate(i, i + matchedLength, matchedLength, term.Source, term.Target));
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

        private static readonly string[] KoreanTrailingSuffixes =
        [
            // 3-char particles / copula (checked longest-first)
            "에서는", "에서도", "에서만", "에게는", "에게도", "한테는", "한테도",
            "으로는", "으로도", "으로만", "까지는", "까지도", "부터는", "부터도",
            "입니다", "이에요", "입니까", "이라면", "이라도", "이라고", "이라는",
            "이네요", "이군요", "이지요", "인가요", "인데요", "이니까",
            // 2-char particles / copula
            "에서", "에게", "한테", "으로", "까지", "부터", "보다", "처럼", "만큼",
            "이랑", "이나", "라도", "마저", "조차", "밖에", "하고",
            "이다", "이야", "예요", "이요", "인가", "인지", "인데", "이면", "이라", "이죠",
            "에도", "에는", "에만", "로는", "로도", "로만", "과는", "와는",
            // 1-char particles
            "은", "는", "이", "가", "을", "를", "의", "에", "로", "와", "과", "도", "만", "랑", "께", "야", "요",
        ];

        private static bool TryPassBoundaryCheck(
            string text,
            int start,
            int length,
            IndexedTerm term,
            TranslationLanguageCode sourceLanguage,
            out int matchedLength)
        {
            matchedLength = length;
            var end = start + length;

            if (term.StartsWithLatinAlnum && start > 0 && IsLatinAlnum(text[start - 1]))
            {
                return false;
            }

            if (term.EndsWithLatinAlnum && end < text.Length && IsLatinAlnum(text[end]))
            {
                // Allow simple English regular plurals (-s, sibilant -es) on 4+ char terms
                // (e.g., "aetherytes" -> "Aetheryte", "alchemists" -> "alchemist").
                if (sourceLanguage == TranslationLanguageCode.English && length >= 4)
                {
                    if (text[end] == 's' && (end + 1 == text.Length || !IsLatinAlnum(text[end + 1])))
                    {
                        matchedLength = length + 1;
                    }
                    else if (text[end] == 'e'
                        && end + 1 < text.Length
                        && text[end + 1] == 's'
                        && (end + 2 == text.Length || !IsLatinAlnum(text[end + 2]))
                        && term.MatchKey[^1] is 's' or 'x' or 'z' or 'h')
                    {
                        matchedLength = length + 2;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (term.StartsWithKatakana && start > 0 && IsKatakana(text[start - 1]))
            {
                return false;
            }

            if (term.EndsWithKatakana && end < text.Length && IsKatakana(text[end]))
            {
                return false;
            }

            if (term.StartsWithHangul && start > 0 && IsHangulSyllable(text[start - 1]))
            {
                return false;
            }

            if (term.EndsWithHangul && end < text.Length && IsHangulSyllable(text[end]))
            {
                return HasAllowedKoreanSuffixBoundary(text, end);
            }

            return true;
        }

        private static bool HasAllowedKoreanSuffixBoundary(string text, int suffixStart)
        {
            var remaining = text.AsSpan(suffixStart);
            foreach (var suffix in KoreanTrailingSuffixes)
            {
                if (!remaining.StartsWith(suffix.AsSpan(), StringComparison.Ordinal))
                {
                    continue;
                }

                var afterSuffix = suffixStart + suffix.Length;
                if (afterSuffix == text.Length || !IsHangulSyllable(text[afterSuffix]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHangulSyllable(char ch)
        {
            return ch >= '\uAC00' && ch <= '\uD7A3';
        }

        private static bool IsLatinScriptLanguage(TranslationLanguageCode language)
        {
            return language is TranslationLanguageCode.English
                or TranslationLanguageCode.German
                or TranslationLanguageCode.French;
        }

        private static string NormalizeApostrophes(string text)
        {
            return text.IndexOfAny(['\u2019', '\u2018', '\uFF07', '\uFF1A']) >= 0
                ? text.Replace('\u2019', '\'').Replace('\u2018', '\'').Replace('\uFF07', '\'').Replace('\uFF1A', ':')
                : text;
        }

        private static string NormalizeNpcLookupKey(string? speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker))
            {
                return string.Empty;
            }

            var trimmed = NormalizeApostrophes(speaker.Trim());
            if (trimmed.IndexOf("  ", StringComparison.Ordinal) < 0
                && trimmed.IndexOf('\t') < 0
                && trimmed.IndexOf('\n') < 0
                && trimmed.IndexOf('\r') < 0)
            {
                return trimmed;
            }

            return string.Join(' ', trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
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
            bool EndsWithKatakana,
            bool StartsWithHangul,
            bool EndsWithHangul);

        private readonly record struct MatchCandidate(
            int Start,
            int End,
            int Length,
            string Source,
            string Target);
    }
}
