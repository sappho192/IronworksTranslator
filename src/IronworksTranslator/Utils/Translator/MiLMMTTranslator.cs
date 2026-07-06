using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Translators;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using Serilog;
using System.IO;

namespace IronworksTranslator.Utils.Translator
{
    public sealed class MiLMMTTranslator : TranslatorBase, IDisposable
    {
        private const int ContextSize = 2048;
        private const int GpuLayerCount = 99;
        private const int BatchSize = 2048;
        private const int UBatchSize = 512;
        private const int MaxTokens = 512;
        private const int TimeoutSeconds = 30;

        private static readonly string[] StopTokens = ["<end_of_turn>", "<eos>", "</s>"];
        private static readonly object NativeConfigLock = new();
        private static bool isNativeConfigured;
        private static LocalModelDevicePriority? configuredDevicePriority;

        private readonly TranslationLanguageCode[] translationLanguages = [
            TranslationLanguageCode.Japanese,
            TranslationLanguageCode.English,
            TranslationLanguageCode.German,
            TranslationLanguageCode.French,
            TranslationLanguageCode.Korean
        ];

        private readonly SemaphoreSlim inferenceLock = new(1, 1);
        private LLamaWeights? weights;
        private StatelessExecutor? executor;
        private string? loadedModelPath;
        private LocalModelDevicePriority? loadedDevicePriority;
        private bool disposed;

        public override TranslationLanguageCode[] SupportedSourceLanguages => translationLanguages;
        public override TranslationLanguageCode[] SupportedTargetLanguages => translationLanguages;

        public override string Translate(
            string input,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage)
        {
            return TranslateAsync(input, sourceLanguage, targetLanguage).GetAwaiter().GetResult();
        }

        public override async Task<string> TranslateAsync(
            string input,
            TranslationLanguageCode sourceLanguage,
            TranslationLanguageCode targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(input) || sourceLanguage == targetLanguage)
            {
                return input;
            }

            if (!SupportedSourceLanguages.Contains(sourceLanguage))
            {
                Log.Error("Unsupported MiLLMT sourceLanguage: {SourceLanguage}", sourceLanguage);
                return input;
            }

            if (!SupportedTargetLanguages.Contains(targetLanguage))
            {
                Log.Error("Unsupported MiLLMT targetLanguage: {TargetLanguage}", targetLanguage);
                return input;
            }

            var modelProfile = MiLMMTModelProfiles.GetCurrent();
            if (!File.Exists(modelProfile.FilePath))
            {
                Log.Error("MiLLMT model file does not exist: {ModelPath}", modelProfile.FilePath);
                return input;
            }

            await inferenceLock.WaitAsync();
            try
            {
                if (!EnsureInitialized(modelProfile))
                {
                    return input;
                }

                var prompt = RenderPrompt(sourceLanguage, targetLanguage, input);
                var inferenceParams = CreateInferenceParams();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                var generated = new List<string>();

                await foreach (var chunk in executor!.InferAsync(prompt, inferenceParams, timeout.Token))
                {
                    generated.Add(chunk);
                }

                var output = StripStops(string.Concat(generated)).Trim();
                return string.IsNullOrWhiteSpace(output) ? input : output;
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Error translating with MiLLMT. SourceLanguage: {SourceLanguage}, TargetLanguage: {TargetLanguage}",
                    sourceLanguage,
                    targetLanguage);
                return input;
            }
            finally
            {
                inferenceLock.Release();
            }
        }

        private bool EnsureInitialized(MiLMMTModelProfile modelProfile)
        {
            var devicePriority = GetDevicePriority();
            if (executor != null
                && loadedModelPath == modelProfile.FilePath
                && loadedDevicePriority == devicePriority)
            {
                return true;
            }

            try
            {
                UnloadModel();
                var effectiveDevicePriority = ConfigureNativeLibrary(devicePriority);
                var useGpu = effectiveDevicePriority is LocalModelDevicePriority.Cuda
                    or LocalModelDevicePriority.Vulkan;
                var modelParams = new ModelParams(modelProfile.FilePath)
                {
                    ContextSize = ContextSize,
                    GpuLayerCount = useGpu ? GpuLayerCount : 0,
                    BatchSize = BatchSize,
                    UBatchSize = UBatchSize,
                };

                weights = LLamaWeights.LoadFromFile(modelParams);
                executor = new StatelessExecutor(weights, modelParams)
                {
                    ApplyTemplate = false,
                };

                loadedModelPath = modelProfile.FilePath;
                loadedDevicePriority = effectiveDevicePriority;
                Log.Information(
                    "MiLLMT model loaded from {ModelPath}. DevicePriority: {DevicePriority}",
                    modelProfile.FilePath,
                    effectiveDevicePriority);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize MiLLMT model.");
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
                    if (devicePriority == LocalModelDevicePriority.Cpu)
                    {
                        return LocalModelDevicePriority.Cpu;
                    }

                    if (configuredDevicePriority != devicePriority)
                    {
                        Log.Warning(
                            "LLamaSharp native backend is already configured as {ConfiguredDevicePriority}; requested {RequestedDevicePriority} will require app restart to change backend.",
                            configuredDevicePriority,
                            devicePriority);
                    }

                    return configuredDevicePriority ?? devicePriority;
                }

                var config = NativeLibraryConfig.All
                    .WithCuda(devicePriority == LocalModelDevicePriority.Cuda)
                    .WithVulkan(devicePriority == LocalModelDevicePriority.Vulkan)
                    .WithAutoFallback(true);

                config.WithLogCallback((level, message) =>
                {
                    if (ShouldLogNativeMessage(level.ToString(), message))
                    {
                        Log.Debug("LLamaSharp native:{Level}: {Message}", level, message.TrimEnd());
                    }
                });

                configuredDevicePriority = devicePriority;
                isNativeConfigured = true;
                return devicePriority;
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

        private static InferenceParams CreateInferenceParams()
        {
            return new InferenceParams
            {
                MaxTokens = MaxTokens,
                AntiPrompts = StopTokens,
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = 0.0f,
                    TopK = 1,
                    TopP = 1.0f,
                    MinP = 0.0f,
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
            var sourceName = GetLanguageName(sourceLanguage);
            var targetName = GetLanguageName(targetLanguage);
            var trimmed = text.Trim();

            return
                $"Translate this from {sourceName} to {targetName}:\n" +
                $"{sourceName}: {trimmed}\n" +
                $"{targetName}:";
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
                _ => throw new ArgumentException($"Unsupported MiLLMT language: {language}", nameof(language)),
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
        }
    }
}
