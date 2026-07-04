using IronworksTranslator.Utils;
using MdXaml;
using Serilog;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace IronworksTranslator.Services
{
    public class AppUpdateService
    {
        private const string RepositoryUrl = "https://github.com/sappho192/IronworksTranslator";
        private int _isChecking;

        public async Task CheckForUpdatesWithPromptAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _isChecking, 1) != 0)
            {
                Log.Information("Update check skipped because another update check is already running.");
                return;
            }

            try
            {
                await CheckForUpdatesCoreAsync(cancellationToken);
            }
            catch (NotInstalledException ex)
            {
                Log.Information(ex, "Update check skipped because this process is not a Velopack install.");
            }
            catch (AcquireLockFailedException ex)
            {
                Log.Warning(ex, "Update check skipped because another Velopack operation owns the update lock.");
            }
            catch (ChecksumFailedException ex)
            {
                Log.Error(ex, "Downloaded update failed checksum validation.");
                ShowUpdateFailedMessage();
            }
            catch (OperationCanceledException ex)
            {
                Log.Information(ex, "Update check was cancelled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check for or apply updates.");
                ShowUpdateFailedMessage();
            }
            finally
            {
                Interlocked.Exchange(ref _isChecking, 0);
            }
        }

        private static async Task CheckForUpdatesCoreAsync(CancellationToken cancellationToken)
        {
            var source = new GithubSource(
                RepositoryUrl,
                accessToken: null,
                prerelease: false,
                downloader: null);
            var updateManager = new UpdateManager(source);

            var updateInfo = await updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
            {
                Log.Information("IronworksTranslator is up to date.");
                return;
            }

            Log.Information(
                "IronworksTranslator update is available. Current: {CurrentVersion}, Latest: {LatestVersion}",
                updateManager.CurrentVersion,
                updateInfo.TargetFullRelease.Version);

            if (!await AskToUpdateAsync(updateManager, updateInfo))
            {
                Log.Information("User postponed the update.");
                return;
            }

            var lastLoggedProgress = -1;
            await updateManager.DownloadUpdatesAsync(
                updateInfo,
                progress =>
                {
                    if (progress == 100 || progress >= lastLoggedProgress + 10)
                    {
                        lastLoggedProgress = progress;
                        Log.Information("Update download progress: {Progress}%", progress);
                    }
                },
                cancellationToken);

            Log.Information(
                "Update downloaded. Applying version {Version} and restarting.",
                updateInfo.TargetFullRelease.Version);

            Log.CloseAndFlush();
            updateManager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease, Array.Empty<string>());
        }

        private static async Task<bool> AskToUpdateAsync(UpdateManager updateManager, UpdateInfo updateInfo)
        {
            var updateContent = BuildUpdateContent(updateManager, updateInfo);
            var markdownEngine = new Markdown();
            FlowDocument document = markdownEngine.Transform(updateContent);
            document.FontFamily = new System.Windows.Media.FontFamily("sans-serif");

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 500,
                Content = new RichTextBox
                {
                    Document = document,
                    IsReadOnly = true,
                },
            };

            var updateMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = Localizer.GetString("main.update.title"),
                Content = scrollViewer,
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = Localizer.GetString("main.update.restart"),
                CloseButtonText = Localizer.GetString("main.update.later"),
            };

            var result = await updateMessageBox.ShowDialogAsync();
            return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
        }

        private static string BuildUpdateContent(UpdateManager updateManager, UpdateInfo updateInfo)
        {
            var currentVersion = updateManager.CurrentVersion?.ToString()
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                ?? "unknown";
            var latestVersion = updateInfo.TargetFullRelease.Version.ToString();

            var sb = new StringBuilder();
            sb.AppendLine(Localizer.GetString("main.update.description"));
            sb.AppendLine();
            sb.AppendLine($"{Localizer.GetString("main.update.current_version")}{currentVersion}");
            sb.AppendLine($"{Localizer.GetString("main.update.latest_version")}**{latestVersion}**");

            var releaseNotes = updateInfo.TargetFullRelease.NotesMarkdown;
            if (!string.IsNullOrWhiteSpace(releaseNotes))
            {
                sb.AppendLine();
                sb.AppendLine(releaseNotes);
            }

            return sb.ToString();
        }

        private static void ShowUpdateFailedMessage()
        {
            MessageBox.Show(
                Localizer.GetString("main.update.failed.description"),
                Localizer.GetString("main.update.failed.title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
