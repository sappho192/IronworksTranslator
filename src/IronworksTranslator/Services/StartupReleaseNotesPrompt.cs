using IronworksTranslator.Models.ReleaseNotes;
using IronworksTranslator.Models.Settings;
using Serilog;

namespace IronworksTranslator.Services
{
    internal sealed class StartupReleaseNotesPrompt
    {
        private int _isShowing;

        internal async Task ShowIfNeededAsync(
            UISettings settings,
            string appVersion,
            Func<Task<GitHubReleaseNote?>> loadLatest,
            Func<GitHubReleaseNote, Task> show)
        {
            if (string.IsNullOrWhiteSpace(appVersion)
                || string.Equals(settings.LastReleaseNotesAppVersion, appVersion, StringComparison.Ordinal)
                || Interlocked.Exchange(ref _isShowing, 1) != 0)
            {
                return;
            }

            try
            {
                var release = await loadLatest();
                if (release == null)
                    return;

                await show(release);
                settings.LastReleaseNotesAppVersion = appVersion;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not show startup release notes for app version {Version}; will retry on next launch.", appVersion);
            }
            finally
            {
                Interlocked.Exchange(ref _isShowing, 0);
            }
        }
    }
}
