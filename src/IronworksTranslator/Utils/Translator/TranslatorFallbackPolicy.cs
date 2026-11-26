using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;

namespace IronworksTranslator.Utils.Translator
{
    internal enum TranslatorFallbackActionKind
    {
        None,
        SwitchCloudEngine,
        UseLocalModel,
        DownloadLocalModel,
    }

    internal sealed record TranslatorFallbackDecision(
        TranslatorFallbackActionKind Kind,
        TranslatorEngine? Engine = null,
        MiLMMTModelProfile? Profile = null)
    {
        public static TranslatorFallbackDecision None { get; } = new(TranslatorFallbackActionKind.None);
    }

    /// <summary>
    /// Counts consecutive failures per cloud translator. An engine is considered unavailable
    /// once it fails <see cref="FailureThreshold"/> times in a row; any success resets it.
    /// </summary>
    internal sealed class CloudTranslatorFailureTracker
    {
        public const int DefaultFailureThreshold = 3;

        private readonly Dictionary<TranslatorEngine, int> consecutiveFailures = [];
        private readonly object gate = new();

        public CloudTranslatorFailureTracker(int failureThreshold = DefaultFailureThreshold)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(failureThreshold, 1);
            FailureThreshold = failureThreshold;
        }

        public int FailureThreshold { get; }

        public int RecordFailure(TranslatorEngine engine)
        {
            lock (gate)
            {
                consecutiveFailures.TryGetValue(engine, out var count);
                count++;
                consecutiveFailures[engine] = count;
                return count;
            }
        }

        public void RecordSuccess(TranslatorEngine engine)
        {
            lock (gate)
            {
                consecutiveFailures[engine] = 0;
            }
        }

        public bool IsUnavailable(TranslatorEngine engine)
        {
            lock (gate)
            {
                return consecutiveFailures.TryGetValue(engine, out var count)
                    && count >= FailureThreshold;
            }
        }
    }

    /// <summary>
    /// Decides what to do when the active cloud translator keeps failing:
    /// try the other cloud engine if it is usable, otherwise fall back to the local MiLMMT model,
    /// downloading the default 1B model when no local model exists yet.
    /// </summary>
    internal static class TranslatorFallbackPolicy
    {
        public static TranslatorFallbackDecision Decide(
            TranslatorEngine failedEngine,
            TranslatorEngine activeEngine,
            Func<TranslatorEngine, bool> isUnavailable,
            int deeplApiKeyCount,
            MiLMMTModelProfile currentProfile,
            Func<MiLMMTModelProfile, bool> isModelAvailable)
        {
            ArgumentNullException.ThrowIfNull(isUnavailable);
            ArgumentNullException.ThrowIfNull(currentProfile);
            ArgumentNullException.ThrowIfNull(isModelAvailable);

            if (!IsCloudEngine(failedEngine)
                || failedEngine != activeEngine
                || !isUnavailable(failedEngine))
            {
                return TranslatorFallbackDecision.None;
            }

            var alternative = failedEngine == TranslatorEngine.Papago
                ? TranslatorEngine.DeepL_API
                : TranslatorEngine.Papago;
            if (IsConfigured(alternative, deeplApiKeyCount) && !isUnavailable(alternative))
            {
                return new TranslatorFallbackDecision(
                    TranslatorFallbackActionKind.SwitchCloudEngine,
                    alternative);
            }

            var localProfile = FindAvailableLocalProfile(currentProfile, isModelAvailable);
            return localProfile != null
                ? new TranslatorFallbackDecision(
                    TranslatorFallbackActionKind.UseLocalModel,
                    TranslatorEngine.MiLMMT,
                    localProfile)
                : new TranslatorFallbackDecision(
                    TranslatorFallbackActionKind.DownloadLocalModel,
                    TranslatorEngine.MiLMMT,
                    GetDownloadProfile(currentProfile));
        }

        internal static MiLMMTModelProfile? FindAvailableLocalProfile(
            MiLMMTModelProfile currentProfile,
            Func<MiLMMTModelProfile, bool> isModelAvailable)
        {
            if (isModelAvailable(currentProfile))
            {
                return currentProfile;
            }

            return MiLMMTModelProfiles.All
                .OrderBy(profile => profile.Size == currentProfile.Size ? 0 : 1)
                .ThenBy(profile => profile.FileSize)
                .FirstOrDefault(isModelAvailable);
        }

        /// <summary>
        /// Emergency downloads stay on the 1B model so they finish quickly and run on any PC.
        /// The user's quantization choice is kept when they already picked a 1B profile.
        /// </summary>
        internal static MiLMMTModelProfile GetDownloadProfile(MiLMMTModelProfile currentProfile)
        {
            return currentProfile.Size == MiLMMTModelSize.MiLMMT_1B
                ? currentProfile
                : MiLMMTModelProfiles.Get(MiLMMTModelSize.MiLMMT_1B, MiLMMTQuantization.Q8_0);
        }

        private static bool IsCloudEngine(TranslatorEngine engine)
        {
            return engine is TranslatorEngine.Papago or TranslatorEngine.DeepL_API;
        }

        private static bool IsConfigured(TranslatorEngine engine, int deeplApiKeyCount)
        {
            return engine switch
            {
                TranslatorEngine.DeepL_API => deeplApiKeyCount > 0,
                _ => true,
            };
        }
    }
}
