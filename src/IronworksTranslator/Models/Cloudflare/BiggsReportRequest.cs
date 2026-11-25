using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translator;
using System.Text.Json.Serialization;

namespace IronworksTranslator.Models.Cloudflare
{
    public sealed class BiggsGlossaryTerm
    {
        [JsonPropertyName("source")]
        public string Source { get; init; } = string.Empty;

        [JsonPropertyName("target")]
        public string Target { get; init; } = string.Empty;
    }

    public sealed class BiggsReportRequest
    {
        public const int CurrentSchemaVersion = 2;

        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; init; } = CurrentSchemaVersion;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; }

        [JsonPropertyName("app_version")]
        public string AppVersion { get; init; } = string.Empty;

        [JsonPropertyName("translation_kind")]
        public string TranslationKind { get; init; } = "chat";

        [JsonPropertyName("npc_speaker")]
        public string? NpcSpeaker { get; init; }

        [JsonPropertyName("source_language")]
        public string SourceLanguage { get; init; } = string.Empty;

        [JsonPropertyName("target_language")]
        public string TargetLanguage { get; init; } = string.Empty;

        [JsonPropertyName("source_text")]
        public string SourceText { get; init; } = string.Empty;

        [JsonPropertyName("engine")]
        public string Engine { get; init; } = string.Empty;

        [JsonPropertyName("model_id")]
        public string? ModelId { get; init; }

        [JsonPropertyName("model_sha256")]
        public string? ModelSha256 { get; init; }

        [JsonPropertyName("original_translation")]
        public string OriginalTranslation { get; init; } = string.Empty;

        [JsonPropertyName("suggested_translation")]
        public string? SuggestedTranslation { get; init; }

        [JsonPropertyName("draft_source")]
        public string DraftSource { get; init; } = "current";

        [JsonPropertyName("draft_edited")]
        public bool DraftEdited { get; init; }

        [JsonPropertyName("glossary_terms")]
        public IReadOnlyList<BiggsGlossaryTerm> GlossaryTerms { get; init; } = [];

        [JsonPropertyName("comment")]
        public string? Comment { get; init; }

        public static BiggsReportRequest Create(
            string appVersion,
            MiLMMTTranslationKind translationKind,
            string? originalNpcSpeaker,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string sourceText,
            TranslatorEngine? engine,
            string? modelId,
            string? modelSha256,
            string originalTranslation,
            string? suggestedTranslation,
            string draftSource,
            bool draftEdited,
            IReadOnlyList<BiggsGlossaryTerm>? glossaryTerms,
            string? comment,
            DateTime? timestampUtc = null)
        {
            var isDialogue = translationKind == MiLMMTTranslationKind.Dialogue;
            var isMiLMMT = engine == TranslatorEngine.MiLMMT;
            var normalizedSuggested = string.IsNullOrWhiteSpace(suggestedTranslation)
                ? null
                : suggestedTranslation.Trim();
            var normalizedComment = string.IsNullOrWhiteSpace(comment)
                ? null
                : comment.Trim();
            var normalizedSpeaker = isDialogue && !string.IsNullOrWhiteSpace(originalNpcSpeaker)
                ? originalNpcSpeaker.Trim()
                : null;

            return new BiggsReportRequest
            {
                SchemaVersion = CurrentSchemaVersion,
                Timestamp = (timestampUtc ?? DateTime.UtcNow).ToUniversalTime(),
                AppVersion = appVersion.Trim(),
                TranslationKind = ToKindString(translationKind),
                NpcSpeaker = normalizedSpeaker,
                SourceLanguage = sourceLanguage.ToString(),
                TargetLanguage = targetLanguage.ToString(),
                SourceText = sourceText,
                Engine = engine?.ToString() ?? "Unknown",
                ModelId = isMiLMMT && !string.IsNullOrWhiteSpace(modelId) ? modelId.Trim() : null,
                ModelSha256 = isMiLMMT && !string.IsNullOrWhiteSpace(modelSha256) ? modelSha256.Trim() : null,
                OriginalTranslation = originalTranslation,
                SuggestedTranslation = normalizedSuggested,
                DraftSource = string.IsNullOrWhiteSpace(draftSource) ? "current" : draftSource.Trim(),
                DraftEdited = draftEdited,
                GlossaryTerms = glossaryTerms ?? [],
                Comment = normalizedComment,
            };
        }

        private static string ToKindString(MiLMMTTranslationKind kind)
        {
            return kind switch
            {
                MiLMMTTranslationKind.Chat => "chat",
                MiLMMTTranslationKind.Dialogue => "dialogue",
                MiLMMTTranslationKind.Manual => "manual",
                _ => "chat",
            };
        }
    }

    public sealed record BiggsSubmitResult(bool IsSuccess, string? ErrorMessage = null)
    {
        public static BiggsSubmitResult Ok() => new(true);
        public static BiggsSubmitResult Failed(string? errorMessage = null) => new(false, errorMessage);
    }
}
