using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.Utils.Translators;
using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.Views.Windows;
using Serilog;
using System.IO;
using MessageBox = System.Windows.MessageBox;

namespace IronworksTranslator.Services
{
    /// <summary>
    /// Watches Papago/DeepL outcomes and, when the active cloud engine keeps failing,
    /// switches to the other cloud engine or to the local MiLMMT model (downloading it if needed)
    /// so translation keeps working during a cloud outage.
    /// </summary>
    public sealed class TranslatorFallbackService
    {
        private static readonly TimeSpan RetryCooldown = TimeSpan.FromMinutes(10);

        private readonly CloudTranslatorFailureTracker failureTracker = new();
        private readonly object gate = new();
        private bool fallbackInProgress;
        private DateTime retryNotBeforeUtc = DateTime.MinValue;

        public TranslatorFallbackService(PapagoTranslator papagoTranslator, DeepLAPITranslator deepLTranslator)
        {
            papagoTranslator.OutcomeReported += (_, e) => OnOutcome(TranslatorEngine.Papago, e);
            deepLTranslator.OutcomeReported += (_, e) => OnOutcome(TranslatorEngine.DeepL_API, e);
        }

        private void OnOutcome(TranslatorEngine engine, TranslatorOutcomeEventArgs e)
        {
            if (e.Succeeded)
            {
                failureTracker.RecordSuccess(engine);
                return;
            }

            var failures = failureTracker.RecordFailure(engine);
            if (failures < failureTracker.FailureThreshold)
            {
                return;
            }

            var translatorSettings = IronworksSettings.Instance?.TranslatorSettings;
            var dispatcher = Application.Current?.Dispatcher;
            if (translatorSettings == null || dispatcher == null)
            {
                return;
            }

            TranslatorFallbackDecision decision;
            lock (gate)
            {
                if (fallbackInProgress || DateTime.UtcNow < retryNotBeforeUtc)
                {
                    return;
                }

                decision = TranslatorFallbackPolicy.Decide(
                    engine,
                    translatorSettings.TranslatorEngine,
                    failureTracker.IsUnavailable,
                    translatorSettings.DeeplApiKeys?.Count ?? 0,
                    MiLMMTModelProfiles.GetCurrent(),
                    HasExpectedFileSize);
                if (decision.Kind == TranslatorFallbackActionKind.None)
                {
                    return;
                }

                fallbackInProgress = true;
            }

            Log.Warning(
                "{Engine} failed {Failures} times in a row. Applying translator fallback: {Action} {TargetEngine} {Profile}",
                engine,
                failures,
                decision.Kind,
                decision.Engine,
                decision.Profile?.FileName);

            dispatcher.InvokeAsync(() => ApplyDecision(engine, decision));
        }

        private void ApplyDecision(TranslatorEngine failedEngine, TranslatorFallbackDecision decision)
        {
            var succeeded = false;
            try
            {
                succeeded = decision.Kind switch
                {
                    TranslatorFallbackActionKind.SwitchCloudEngine => SwitchEngine(failedEngine, decision.Engine!.Value, null),
                    // Both go through the model preparation window: it verifies the hash of an existing file
                    // (a manual download can leave a broken one behind) and downloads a replacement if needed.
                    TranslatorFallbackActionKind.UseLocalModel => PrepareModelAndSwitch(failedEngine, decision.Profile!),
                    TranslatorFallbackActionKind.DownloadLocalModel => PrepareModelAndSwitch(failedEngine, decision.Profile!),
                    _ => false,
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Translator fallback failed. Decision: {Decision}", decision);
            }
            finally
            {
                lock (gate)
                {
                    fallbackInProgress = false;
                    if (!succeeded)
                    {
                        retryNotBeforeUtc = DateTime.UtcNow + RetryCooldown;
                    }
                }
            }
        }

        /// <summary>
        /// Cheap pre-check used while choosing a model; the full hash check happens in <see cref="PrepareModelAndSwitch"/>.
        /// </summary>
        private static bool HasExpectedFileSize(MiLMMTModelProfile profile)
        {
            try
            {
                var file = new FileInfo(profile.FilePath);
                return file.Exists && file.Length == profile.FileSize;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to inspect MiLMMT model file {Path}", profile.FilePath);
                return false;
            }
        }

        private static bool PrepareModelAndSwitch(TranslatorEngine failedEngine, MiLMMTModelProfile profile)
        {
            var window = new InitializationWindow(TranslatorEngine.MiLMMT, profile, isAutomaticFallback: true);
            window.ShowDialog();

            if (!window.Succeeded || !File.Exists(profile.FilePath))
            {
                Log.Error("Automatic fallback could not prepare MiLMMT model {Profile}", profile.FileName);
                MessageBox.Show(
                    string.Format(
                        Localizer.GetString("translator.fallback.download.failed"),
                        (int)RetryCooldown.TotalMinutes),
                    Localizer.GetString("translator.fallback.title"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return SwitchEngine(failedEngine, TranslatorEngine.MiLMMT, profile);
        }

        private static bool SwitchEngine(
            TranslatorEngine failedEngine,
            TranslatorEngine targetEngine,
            MiLMMTModelProfile? profile)
        {
            App.GetService<SettingsViewModel>().ApplyTranslatorFallback(targetEngine, profile);
            Log.Warning(
                "Translator engine switched from {FailedEngine} to {TargetEngine} by automatic fallback. Model: {Profile}",
                failedEngine,
                targetEngine,
                profile?.FileName);

            var message = targetEngine == TranslatorEngine.MiLMMT
                ? string.Format(
                    Localizer.GetString("translator.fallback.switched.local"),
                    profile?.DisplayName ?? "MiLMMT")
                : string.Format(
                    Localizer.GetString("translator.fallback.switched.cloud"),
                    GetEngineName(failedEngine),
                    GetEngineName(targetEngine));
            MessageBox.Show(
                message,
                Localizer.GetString("translator.fallback.title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true;
        }

        private static string GetEngineName(TranslatorEngine engine)
        {
            return engine switch
            {
                TranslatorEngine.Papago => "Papago",
                TranslatorEngine.DeepL_API => "DeepL (API)",
                TranslatorEngine.MiLMMT => "MiLMMT",
                _ => engine.ToString(),
            };
        }
    }
}
