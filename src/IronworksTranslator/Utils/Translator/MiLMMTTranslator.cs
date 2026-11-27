using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Services;
using IronworksTranslator.Utils.Translators;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using Serilog;
using System.IO;
using System.Text;

namespace IronworksTranslator.Utils.Translator
{
    public sealed class MiLMMTTranslator : TranslatorBase, IDisposable
    {
        private const int TimeoutSeconds = 30;
        private const int TranslationCacheCapacity = 256;

        private static readonly string[] StopTokens = ["<end_of_turn>", "<eos>", "</s>"];
        private static readonly object NativeConfigLock = new();
        private static readonly MiLMMTNativeBackendSession NativeBackendSession = new();
        private static bool isNativeConfigured;
        private static LocalModelDevicePriority? configuredDevicePriority;

        private readonly TranslationLanguageCode[] translationLanguages = [
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.English,
            TranslationLanguageCode.German,
            TranslationLanguageCode.French,
            TranslationLanguageCode.Korean
        ];

        private readonly MiLMMTGlossaryService? glossaryService;
        private readonly SemaphoreSlim inferenceLock = new(1, 1);
        private readonly BoundedTranslationCache<TranslationCacheKey> translationCache = new(
            TranslationCacheCapacity,
            TimeSpan.FromMinutes(5));
        private LLamaWeights? weights;
        private StatelessExecutor? executor;
        private string? loadedModelPath;
        private LocalModelDevicePriority? loadedDevicePriority;
        private MiLMMTInferenceProfile? loadedInferenceProfile;
        private bool disposed;

        public MiLMMTTranslator() : this(null)
        {
        }

        public MiLMMTTranslator(MiLMMTGlossaryService? glossaryService)
        {
            this.glossaryService = glossaryService;
        }

        public override TranslationLanguageCode[] SupportedSourceLanguages => translationLanguages;
        public override TranslationLanguageCode[] SupportedTargetLanguages => translationLanguages;

        internal void ConfigureNativeBackendAtStartup()
        {
            try
            {
                _ = ConfigureNativeLibrary(GetDevicePriority());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure the MiLMMT native backend at startup.");
            }
        }

        public override string Translate(
            string input,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage)
        {
            return Translate(
                input,
                sourceLanguage,
                targetLanguage,
                MiLMMTTranslationKind.Manual);
        }

        public string Translate(
            string input,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            MiLMMTTranslationKind requestKind,
            string? speakerName = null)
        {
            return TranslateAsync(
                input,
                sourceLanguage,
                targetLanguage,
                requestKind,
                cancellationToken: default,
                speakerName: speakerName).GetAwaiter().GetResult();
        }

        public override async Task<string> TranslateAsync(
            string input,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage)
        {
            return await TranslateAsync(
                input,
                sourceLanguage,
                targetLanguage,
                MiLMMTTranslationKind.Manual);
        }

        public async Task<string> TranslateAsync(
            string input,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            MiLMMTTranslationKind requestKind,
            CancellationToken cancellationToken = default,
            string? speakerName = null)
        {
            if (string.IsNullOrWhiteSpace(input) || sourceLanguage == targetLanguage)
            {
                return input;
            }

            if (!SupportedSourceLanguages.Contains(sourceLanguage))
            {
                Log.Error("Unsupported MiLMMT sourceLanguage: {SourceLanguage}", sourceLanguage);
                return input;
            }

            if (!SupportedTargetLanguages.Contains(targetLanguage))
            {
                Log.Error("Unsupported MiLMMT targetLanguage: {TargetLanguage}", targetLanguage);
                return input;
            }

            var configuredProfile = MiLMMTModelProfiles.GetCurrent();
            var modelProfile = MiLMMTModelProfiles.ResolveEffectiveProfile(
                configuredProfile,
                profile => File.Exists(profile.FilePath));
            if (modelProfile == null)
            {
                Log.Error("MiLMMT model file does not exist: {ModelPath}", configuredProfile.FilePath);
                return input;
            }

            if (!modelProfile.Supports(sourceLanguage) || !modelProfile.Supports(targetLanguage))
            {
                Log.Warning(
                    "MiLMMT model {ModelName} only supports {SupportedLanguages}; requested {SourceLanguage} -> {TargetLanguage}. Returning original input.",
                    modelProfile.DisplayName,
                    modelProfile.SupportedLanguageNames,
                    sourceLanguage,
                    targetLanguage);
                return input;
            }

            if (modelProfile != configuredProfile
                && !string.Equals(loadedModelPath, modelProfile.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                Log.Information(
                    "Selected MiLMMT model {ConfiguredModel} is not downloaded yet; temporarily using installed fallback model {FallbackModel}.",
                    configuredProfile.DisplayName,
                    modelProfile.DisplayName);
            }

            var inferenceProfile = MiLMMTInferenceProfile.GameSafe;
            var maxTokens = inferenceProfile.GetMaxTokens(requestKind);
            var activeMatcher = glossaryService?.CurrentMatcher;
            var glossaryMatcher = modelProfile.SupportsGlossaryPrompt
                ? activeMatcher
                : null;
            var styleHint = ResolveNpcStyleHint(
                modelProfile,
                requestKind,
                sourceLanguage,
                targetLanguage,
                speakerName,
                activeMatcher);
            var glossaryVersion = glossaryMatcher?.Version
                ?? (styleHint != null ? activeMatcher?.Version : null)
                ?? string.Empty;
            var cacheKey = new TranslationCacheKey(
                modelProfile.Sha256,
                sourceLanguage,
                targetLanguage,
                maxTokens,
                glossaryVersion,
                styleHint ?? string.Empty,
                NormalizeForCache(input));
            if (translationCache.TryGet(cacheKey, out var cachedOutput))
            {
                return cachedOutput;
            }

            var lockAcquired = false;
            try
            {
                await inferenceLock.WaitAsync(cancellationToken);
                lockAcquired = true;

                if (translationCache.TryGet(cacheKey, out cachedOutput))
                {
                    return cachedOutput;
                }

                if (!EnsureInitialized(modelProfile, inferenceProfile))
                {
                    return input;
                }

                var matchedTerms = glossaryMatcher != null
                    ? glossaryMatcher.Match(sourceLanguage, targetLanguage, input)
                    : [];
                if (matchedTerms.Count > 0 || styleHint != null)
                {
                    Log.Debug(
                        "MiLMMT prompt conditioning: {MatchedCount} glossary term(s), style='{StyleHint}' (version: {GlossaryVersion}) for {SourceLanguage} -> {TargetLanguage}.",
                        matchedTerms.Count,
                        styleHint ?? string.Empty,
                        glossaryVersion,
                        sourceLanguage,
                        targetLanguage);
                }

                var prompt = RenderPrompt(sourceLanguage, targetLanguage, input, matchedTerms, styleHint);
                var inferenceParams = CreateInferenceParams(maxTokens);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
                var generated = new List<string>();

                await foreach (var chunk in executor!.InferAsync(prompt, inferenceParams, timeout.Token))
                {
                    generated.Add(chunk);
                }

                var output = StripStops(string.Concat(generated)).Trim();
                if (string.IsNullOrWhiteSpace(output))
                {
                    return input;
                }

                if (!string.Equals(output, input, StringComparison.Ordinal))
                {
                    translationCache.Set(cacheKey, output);
                }

                return output;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Log.Debug(
                    "MiLMMT {RequestKind} translation was cancelled before completion.",
                    requestKind);
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Error translating with MiLMMT. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}",
                    sourceLanguage,
                    targetLanguage);
                return input;
            }
            finally
            {
                if (lockAcquired)
                {
                    inferenceLock.Release();
                }
            }
        }

        private bool EnsureInitialized(
            MiLMMTModelProfile modelProfile,
            MiLMMTInferenceProfile inferenceProfile)
        {
            var effectiveDevicePriority = ConfigureNativeLibrary(GetDevicePriority());
            if (executor != null
                && loadedModelPath == modelProfile.FilePath
                && loadedDevicePriority == effectiveDevicePriority
                && loadedInferenceProfile == inferenceProfile)
            {
                return true;
            }

            try
            {
                UnloadModel();
                var useGpu = effectiveDevicePriority is LocalModelDevicePriority.Cuda
                    or LocalModelDevicePriority.Vulkan;
                var modelParams = new ModelParams(modelProfile.FilePath)
                {
                    ContextSize = checked((uint)inferenceProfile.ContextSize),
                    GpuLayerCount = useGpu ? inferenceProfile.GpuLayerCount : 0,
                    BatchSize = checked((uint)inferenceProfile.BatchSize),
                    UBatchSize = checked((uint)inferenceProfile.UBatchSize),
                };

                weights = LLamaWeights.LoadFromFile(modelParams);
                executor = new StatelessExecutor(weights, modelParams)
                {
                    ApplyTemplate = false,
                };

                loadedModelPath = modelProfile.FilePath;
                loadedDevicePriority = effectiveDevicePriority;
                loadedInferenceProfile = inferenceProfile;
                Log.Information(
                    "MiLMMT model loaded from {ModelPath}. DevicePriority: {DevicePriority}. InferenceProfile: {InferenceProfile}",
                    modelProfile.FilePath,
                    effectiveDevicePriority,
                    inferenceProfile.Name);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize MiLMMT model.");
                UnloadModel();
                return false;
            }
        }

        private static LocalModelDevicePriority ConfigureNativeLibrary(LocalModelDevicePriority devicePriority)
        {
            lock (NativeConfigLock)
            {
                if (isNativeConfigured)
                {
                    if (configuredDevicePriority != devicePriority)
                    {
                        Log.Warning(
                            "LLamaSharp native backend is already configured as {ConfiguredDevicePriority}; requested {RequestedDevicePriority} will require app restart to change backend.",
                            configuredDevicePriority,
                            devicePriority);
                    }

                    return configuredDevicePriority ?? LocalModelDevicePriority.Cpu;
                }

                var selectedDevicePriority = NativeBackendSession.Select(
                    devicePriority,
                    System.Runtime.Intrinsics.X86.Avx2.IsSupported,
                    MiLMMTNativeBackendSelector.ProbeGpuBackend);

                if (selectedDevicePriority is LocalModelDevicePriority.Cuda
                    or LocalModelDevicePriority.Vulkan)
                {
                    var llamaPath = MiLMMTNativeBackendSelector.GetLlamaPath(
                        selectedDevicePriority,
                        AppContext.BaseDirectory);
                    if (!File.Exists(llamaPath))
                    {
                        throw new FileNotFoundException("MiLMMT native runtime pack is missing.", llamaPath);
                    }

                    NativeLibraryConfig.LLama.WithLibrary(llamaPath);
                }
                else
                {
                    NativeLibraryConfig.All
                        .WithCuda(false)
                        .WithVulkan(false)
                        .WithAutoFallback(false);
                }

                NativeLibraryConfig.All.WithLogCallback((level, message) =>
                {
                    if (ShouldLogNativeMessage(level.ToString(), message))
                    {
                        Log.Debug("LLamaSharp native:{Level}: {Message}", level, message.TrimEnd());
                    }
                });

                configuredDevicePriority = selectedDevicePriority;
                isNativeConfigured = true;
                Log.Information(
                    "MiLMMT native backend configured as {SelectedDevicePriority} for requested {RequestedDevicePriority}.",
                    selectedDevicePriority,
                    devicePriority);
                return selectedDevicePriority;
            }
        }

        private static bool ShouldLogNativeMessage(string level, string message)
        {
            return level is "Warning" or "Error"
                || message.Contains("ggml_cuda_init", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ggml_vulkan", StringComparison.OrdinalIgnoreCase)
                || message.Contains("using device CUDA", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Vulkan", StringComparison.OrdinalIgnoreCase);
        }

        internal static InferenceParams CreateInferenceParams(int maxTokens)
        {
            return new InferenceParams
            {
                MaxTokens = maxTokens,
                AntiPrompts = StopTokens,
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = 0.0f,
                    TopK = 1,
                    TopP = 1.0f,
                    MinP = 0.0f,
                    // Keep RepeatPenalty neutral (1.0) so prompt tokens like numbers/punctuation
                    // are not distorted, while FrequencyPenalty over a short window terminates
                    // greedy single-token repetition loops on short exclamations.
                    RepeatPenalty = 1.0f,
                    FrequencyPenalty = 0.30f,
                    PresencePenalty = 0.0f,
                    PenaltyCount = 16,
                    Seed = 1,
                },
            };
        }

        private static LocalModelDevicePriority GetDevicePriority()
        {
            return IronworksSettings.Instance?.TranslatorSettings?.LocalModelDevicePriority
                ?? LocalModelDevicePriority.Cuda;
        }

        internal static string RenderPrompt(
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string text)
        {
            return RenderPrompt(sourceLanguage, targetLanguage, text, glossaryTerms: null);
        }

        internal static string RenderPrompt(
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string text,
            IReadOnlyList<MiLMMTGlossaryTerm>? glossaryTerms,
            string? styleHint = null)
        {
            var sourceName = GetLanguageName(sourceLanguage);
            var targetName = GetLanguageName(targetLanguage);
            var trimmed = text.Trim();

            var core =
                $"Translate this from {sourceName} to {targetName}:\n" +
                $"{sourceName}: {trimmed}\n" +
                $"{targetName}:";

            var trimmedStyle = string.IsNullOrWhiteSpace(styleHint) ? null : styleHint.Trim();
            var hasGlossary = glossaryTerms != null && glossaryTerms.Count > 0;
            if (trimmedStyle == null && !hasGlossary)
            {
                return core;
            }

            var sb = new StringBuilder();
            if (trimmedStyle != null)
            {
                sb.Append("Style: ");
                sb.Append(trimmedStyle);
                if (hasGlossary)
                {
                    sb.Append('\n');
                }
            }

            if (hasGlossary)
            {
                sb.Append("Glossary:\n");
                for (var i = 0; i < glossaryTerms!.Count; i++)
                {
                    var term = glossaryTerms[i];
                    if (i > 0)
                    {
                        sb.Append('\n');
                    }

                    sb.Append(term.Source);
                    sb.Append(" => ");
                    sb.Append(term.Target);
                }
            }

            sb.Append("\n\n");
            sb.Append(core);
            return sb.ToString();
        }

        internal static bool ShouldApplyNpcStyle(
            MiLMMTModelProfile modelProfile,
            MiLMMTTranslationKind requestKind,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage)
        {
            return modelProfile.SupportsStylePrompt
                && requestKind == MiLMMTTranslationKind.Dialogue
                && targetLanguage == TranslationLanguageCode.Korean
                && sourceLanguage is TranslationLanguageCode.English
                    or TranslationLanguageCode.German
                    or TranslationLanguageCode.French;
        }

        internal static string? ResolveNpcStyleHint(
            MiLMMTModelProfile modelProfile,
            MiLMMTTranslationKind requestKind,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string? speakerName,
            MiLMMTGlossaryMatcher? matcher)
        {
            if (matcher == null
                || string.IsNullOrWhiteSpace(speakerName)
                || !ShouldApplyNpcStyle(modelProfile, requestKind, sourceLanguage, targetLanguage))
            {
                return null;
            }

            return matcher.TryResolveNpcStyle(sourceLanguage, targetLanguage, speakerName, out var style)
                ? style
                : null;
        }

        internal static string BuildPromptForProfile(
            MiLMMTModelProfile modelProfile,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage,
            string text,
            MiLMMTGlossaryMatcher? matcher,
            string? speakerName = null,
            MiLMMTTranslationKind requestKind = MiLMMTTranslationKind.Manual)
        {
            var matched = modelProfile.SupportsGlossaryPrompt && matcher != null
                ? matcher.Match(sourceLanguage, targetLanguage, text)
                : null;
            var styleHint = ResolveNpcStyleHint(
                modelProfile,
                requestKind,
                sourceLanguage,
                targetLanguage,
                speakerName,
                matcher);
            return RenderPrompt(sourceLanguage, targetLanguage, text, matched, styleHint);
        }

        private static string GetLanguageName(TranslationLanguageCode language)
        {
            return language switch
            {
                TranslationLanguageCode.Japanese => "Japanese",
                TranslationLanguageCode.English => "English",
                TranslationLanguageCode.German => "German",
                TranslationLanguageCode.French => "French",
                TranslationLanguageCode.Korean => "Korean",
                _ => throw new ArgumentException($"Unsupported MiLMMT language: {language}", nameof(language)),
            };
        }

        internal static string StripStops(string text)
        {
            foreach (var stop in StopTokens)
            {
                var index = text.IndexOf(stop, StringComparison.Ordinal);
                if (index >= 0)
                {
                    text = text[..index];
                }
            }

            return text;
        }

        internal static string NormalizeForCache(string input)
        {
            return string.Join(
                ' ',
                input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        public async Task UnloadModelIfLoadedAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (disposed || string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            await inferenceLock.WaitAsync(cancellationToken);
            try
            {
                if (string.Equals(loadedModelPath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("Unloading MiLMMT model before file removal: {ModelPath}", filePath);
                    UnloadModel();
                }
            }
            finally
            {
                inferenceLock.Release();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            inferenceLock.Dispose();
            UnloadModel();
            disposed = true;
        }

        private void UnloadModel()
        {
            weights?.Dispose();
            weights = null;
            executor = null;
            loadedModelPath = null;
            loadedDevicePriority = null;
            loadedInferenceProfile = null;
        }

        private readonly record struct TranslationCacheKey(
            string ModelSha256,
            TranslationLanguageCode SourceLanguage,
            TranslationLanguageCode TargetLanguage,
            int MaxTokens,
            string GlossaryVersion,
            string StyleHint,
            string NormalizedInput);
    }
}
