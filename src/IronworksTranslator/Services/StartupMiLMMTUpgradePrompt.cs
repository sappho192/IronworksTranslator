using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;
using Serilog;
using System.IO;

namespace IronworksTranslator.Services
{
    internal enum StartupMiLMMTUpgradeAction
    {
        None,
        UpgradeFromLegacyFallback,
        DownloadMissingModel,
    }

    internal sealed record StartupMiLMMTUpgradePlan(
        StartupMiLMMTUpgradeAction Action,
        MiLMMTModelProfile? TargetProfile,
        IReadOnlyList<MiLMMTModelProfile> LegacyFallbackProfiles)
    {
        public static StartupMiLMMTUpgradePlan None { get; } = new(
            StartupMiLMMTUpgradeAction.None,
            null,
            Array.Empty<MiLMMTModelProfile>());
    }

    internal sealed class StartupMiLMMTUpgradePrompt
    {
        private int _isShowing;

        internal static StartupMiLMMTUpgradePlan Evaluate(
            TranslatorEngine activeEngine,
            MiLMMTModelProfile targetProfile,
            Func<MiLMMTModelProfile, bool> isAvailable)
        {
            ArgumentNullException.ThrowIfNull(targetProfile);
            ArgumentNullException.ThrowIfNull(isAvailable);

            if (activeEngine != TranslatorEngine.MiLMMT || isAvailable(targetProfile))
            {
                return StartupMiLMMTUpgradePlan.None;
            }

            var legacyFallbacks = MiLMMTModelProfiles.GetDownloadedFallbackProfiles(isAvailable);
            if (legacyFallbacks.Count > 0)
            {
                return new StartupMiLMMTUpgradePlan(
                    StartupMiLMMTUpgradeAction.UpgradeFromLegacyFallback,
                    targetProfile,
                    legacyFallbacks);
            }

            return new StartupMiLMMTUpgradePlan(
                StartupMiLMMTUpgradeAction.DownloadMissingModel,
                targetProfile,
                Array.Empty<MiLMMTModelProfile>());
        }

        internal async Task RunIfNeededAsync(
            TranslatorEngine activeEngine,
            MiLMMTModelProfile targetProfile,
            Func<MiLMMTModelProfile, bool> isAvailable,
            Func<MiLMMTModelProfile, IReadOnlyList<MiLMMTModelProfile>, Task<bool>> confirmUpgradeFromLegacy,
            Func<MiLMMTModelProfile, Task<bool>> confirmMissingModelDownload,
            Func<MiLMMTModelProfile, bool> downloadModel,
            Func<IReadOnlyList<MiLMMTModelProfile>, long, Task<bool>> confirmLegacyCleanup,
            Func<IReadOnlyList<MiLMMTModelProfile>, Task> deleteLegacyProfiles,
            Action fallbackToPapago,
            Func<MiLMMTModelProfile, long>? getInstalledBytes = null)
        {
            if (Interlocked.Exchange(ref _isShowing, 1) != 0)
            {
                return;
            }

            try
            {
                var plan = Evaluate(activeEngine, targetProfile, isAvailable);
                if (plan.Action == StartupMiLMMTUpgradeAction.None || plan.TargetProfile == null)
                {
                    return;
                }

                if (plan.Action == StartupMiLMMTUpgradeAction.UpgradeFromLegacyFallback)
                {
                    var shouldDownload = await confirmUpgradeFromLegacy(plan.TargetProfile, plan.LegacyFallbackProfiles);
                    if (!shouldDownload)
                    {
                        Log.Information(
                            "User postponed MiLMMT v1.0 upgrade for {TargetModel}; continuing with temporary v0.1 fallback.",
                            plan.TargetProfile.DisplayName);
                        return;
                    }

                    if (!downloadModel(plan.TargetProfile))
                    {
                        Log.Warning(
                            "MiLMMT v1.0 upgrade download for {TargetModel} did not complete; continuing with temporary v0.1 fallback.",
                            plan.TargetProfile.DisplayName);
                        return;
                    }

                    await PromptLegacyCleanupIfNeededAsync(
                        isAvailable,
                        confirmLegacyCleanup,
                        deleteLegacyProfiles,
                        getInstalledBytes);
                    return;
                }

                if (plan.Action == StartupMiLMMTUpgradeAction.DownloadMissingModel)
                {
                    var shouldDownload = await confirmMissingModelDownload(plan.TargetProfile);
                    if (!shouldDownload || !downloadModel(plan.TargetProfile))
                    {
                        Log.Information(
                            "MiLMMT model {TargetModel} is unavailable after startup prompt; falling back to Papago.",
                            plan.TargetProfile.DisplayName);
                        fallbackToPapago();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to run startup MiLMMT model upgrade prompt.");
            }
            finally
            {
                Interlocked.Exchange(ref _isShowing, 0);
            }
        }

        internal static async Task PromptLegacyCleanupIfNeededAsync(
            Func<MiLMMTModelProfile, bool> isAvailable,
            Func<IReadOnlyList<MiLMMTModelProfile>, long, Task<bool>> confirmLegacyCleanup,
            Func<IReadOnlyList<MiLMMTModelProfile>, Task> deleteLegacyProfiles,
            Func<MiLMMTModelProfile, long>? getInstalledBytes = null)
        {
            var remainingLegacy = MiLMMTModelProfiles.GetDownloadedFallbackProfiles(isAvailable);
            if (remainingLegacy.Count == 0)
            {
                return;
            }

            var sizeResolver = getInstalledBytes ?? GetInstalledFileBytes;
            var totalBytes = remainingLegacy.Sum(sizeResolver);
            if (await confirmLegacyCleanup(remainingLegacy, totalBytes))
            {
                await deleteLegacyProfiles(remainingLegacy);
            }
        }

        internal static async Task DeleteModelFilesAsync(
            IEnumerable<MiLMMTModelProfile> profiles,
            Func<string, Task> unloadIfLoadedAsync)
        {
            foreach (var profile in profiles)
            {
                await DeleteModelFileAsync(profile, unloadIfLoadedAsync);
            }
        }

        internal static async Task DeleteModelFileAsync(
            MiLMMTModelProfile profile,
            Func<string, Task> unloadIfLoadedAsync)
        {
            await unloadIfLoadedAsync(profile.FilePath);

            if (File.Exists(profile.FilePath))
            {
                File.Delete(profile.FilePath);
            }

            var tempPath = $"{profile.FilePath}.download";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            TryDeleteEmptyDirectory(profile.DirectoryPath);
        }

        internal static long GetInstalledFileBytes(MiLMMTModelProfile profile)
        {
            try
            {
                var info = new FileInfo(profile.FilePath);
                return info.Exists ? info.Length : profile.FileSize;
            }
            catch
            {
                return profile.FileSize;
            }
        }

        private static void TryDeleteEmptyDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath) && !Directory.EnumerateFileSystemEntries(directoryPath).Any())
                {
                    Directory.Delete(directoryPath);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Could not remove empty MiLMMT model directory {DirectoryPath}.", directoryPath);
            }
        }
    }
}
