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
using Velopack.Locators;
using Velopack.Sources;

namespace IronworksTranslator.Services
{
    public class AppUpdateService
    {
        private const string RepositoryUrl = "https://github.com/sappho192/IronworksTranslator";
        private const string LauncherExeName = "IronworksTranslator.Launcher.exe";
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
            var releaseChannel = BuildInfo.ReleaseChannel;
            var source = new GithubSource(
                RepositoryUrl,
                accessToken: null,
                prerelease: releaseChannel.IncludePrereleases,
                downloader: null);
            var updateManager = new UpdateManager(
                source,
                new UpdateOptions
                {
                    ExplicitChannel = releaseChannel.VelopackChannel,
                },
                CreateUpdateLocator());

            Log.Information(
                "Checking for updates. ReleaseChannel: {ReleaseChannel}, VelopackChannel: {VelopackChannel}, IncludePrereleases: {IncludePrereleases}",
                releaseChannel.DisplayName,
                releaseChannel.VelopackChannel,
                releaseChannel.IncludePrereleases);

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

        private static IVelopackLocator CreateUpdateLocator()
        {
            var locator = VelopackLocator.CreateDefaultForPlatform(null, null);
            return new LauncherRestartVelopackLocator(locator);
        }

        private sealed class LauncherRestartVelopackLocator : IVelopackLocator
        {
            private readonly IVelopackLocator _inner;

            public LauncherRestartVelopackLocator(IVelopackLocator inner)
            {
                _inner = inner;
            }

            public string AppId => _inner.AppId ?? string.Empty;

            public string RootAppDir => _inner.RootAppDir ?? string.Empty;

            public string PackagesDir => _inner.PackagesDir ?? string.Empty;

            public string AppContentDir => _inner.AppContentDir ?? string.Empty;

            public string AppTempDir => _inner.AppTempDir ?? string.Empty;

            public string UpdateExePath => _inner.UpdateExePath ?? string.Empty;

            public SemanticVersion? CurrentlyInstalledVersion => _inner.CurrentlyInstalledVersion;

            public string ThisExeRelativePath => GetLauncherRelativePath(_inner.ThisExeRelativePath);

            public string Channel => _inner.Channel ?? string.Empty;

            public string AppUserModelId => _inner.AppUserModelId ?? string.Empty;

            public Velopack.Logging.IVelopackLogger Log => _inner.Log;

            public bool IsPortable => _inner.IsPortable;

            public IProcessImpl Process => _inner.Process;

            public void AddLogger(Velopack.Logging.IVelopackLogger logger)
            {
                _inner.AddLogger(logger);
            }

            public List<VelopackAsset> GetLocalPackages()
            {
                return _inner.GetLocalPackages();
            }

            public VelopackAsset? GetLatestLocalFullPackage()
            {
                return _inner.GetLatestLocalFullPackage();
            }

            public Guid? GetOrCreateStagedUserId()
            {
                return _inner.GetOrCreateStagedUserId();
            }

            private static string GetLauncherRelativePath(string? mainExeRelativePath)
            {
                var directory = Path.GetDirectoryName(mainExeRelativePath);
                return string.IsNullOrEmpty(directory)
                    ? LauncherExeName
                    : Path.Combine(directory, LauncherExeName);
            }
        }
    }
}
