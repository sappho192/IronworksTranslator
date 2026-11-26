using IronworksTranslator.Utils;
using IronworksTranslator.Utils.Aspect;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Translator;
using Serilog;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace IronworksTranslator.Views.Windows
{
    /// <summary>
    /// InitializationWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class InitializationWindow : Window
    {
        private readonly bool isInitialized = false;
        private BackgroundWorker worker;
        private readonly HttpClient httpClient = CreateHttpClient();
        private readonly TranslatorEngine? requestedEngine;
        private readonly MiLMMTModelProfile? requestedMiLMMTProfile;
        private readonly bool isAutomaticFallback;
        private readonly CancellationTokenSource downloadCancellation = new();

        /// <summary>
        /// A download that delivers no data for this long is treated as dead instead of waiting forever.
        /// </summary>
        internal static readonly TimeSpan DownloadStallTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// True when the requested model is ready. Only meaningful after the window closes.
        /// </summary>
        public bool Succeeded { get; private set; }

        /// <param name="isAutomaticFallback">
        /// When true the download was started by the translator fallback instead of the user, so the window
        /// explains why and leaves reporting a failure to the caller. Either way, failures and cancellation are
        /// reported through <see cref="Succeeded"/> and never shut the app down.
        /// </param>
        public InitializationWindow(
            TranslatorEngine? requestedEngine = null,
            MiLMMTModelProfile? requestedMiLMMTProfile = null,
            bool isAutomaticFallback = false)
        {
            this.requestedEngine = requestedEngine;
            this.requestedMiLMMTProfile = requestedMiLMMTProfile;
            this.isAutomaticFallback = isAutomaticFallback;
            InitializeComponent();
            if (isAutomaticFallback)
            {
                txtFallbackNotice.Text = Localizer.GetString("translator.fallback.download.notice");
                txtFallbackNotice.Visibility = Visibility.Visible;
                Height += 56;
            }
            worker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
            };
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            isInitialized = true;

            worker.RunWorkerAsync();
        }

        [TraceMethod]
        private static HttpClient CreateHttpClient()
        {
            var proxy = WebRequest.GetSystemWebProxy();
            proxy.Credentials = CredentialCache.DefaultCredentials;

            var handler = new HttpClientHandler
            {
                Proxy = proxy,
                UseProxy = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IronworksTranslator/1.0");
            return client;
        }

        private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            // A close request during verification arrives after the download finished, so check it here too:
            // a cancelled run must not report success even if the model turned out to be fine.
            var cancelled = downloadCancellation.IsCancellationRequested;
            if (e.Error != null || cancelled)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Succeeded = false;
                    if (cancelled)
                    {
                        Log.Information("Model preparation was cancelled before it finished.");
                    }
                    else
                    {
                        Log.Error(e.Error, "Model preparation failed. Automatic fallback: {IsAutomaticFallback}", isAutomaticFallback);
                    }

                    // The window only opens in a running app (Settings or the fallback), so a failed download
                    // is recoverable: the caller restores a usable engine instead of the app exiting.
                    // The fallback reports its own failure, so only the user-started download needs a dialog here.
                    if (!cancelled && !isAutomaticFallback)
                    {
                        MessageBox.Show(Localizer.GetString("downloader.error.initialization"));
                    }

                    Close();
                });
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Succeeded = true;
                txtProgress.Text = Localizer.GetString("downloader.worker.progress.ready.complete");
                prWorker.Visibility = Visibility.Collapsed;
            });
            Task.Delay(2000).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(Close);
            });
        }

        private void Worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                pbWorker.Value = e.ProgressPercentage;
            });
        }

        private void Worker_DoWork(object? sender, DoWorkEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                txtProgress.Text = Localizer.GetString("downloader.worker.progress.ready.model");
            });

            var selectedEngine = requestedEngine
                ?? IronworksSettings.Instance?.TranslatorSettings?.TranslatorEngine
                ?? TranslatorEngine.Papago;
            switch (selectedEngine)
            {
                case TranslatorEngine.MiLMMT:
                    PrepareMiLMMTModel(requestedMiLMMTProfile ?? MiLMMTModelProfiles.GetCurrent());
                    break;
                default:
                    worker.ReportProgress(100);
                    break;
            }
        }

        private void PrepareMiLMMTModel(MiLMMTModelProfile profile)
        {
            worker.ReportProgress(30);
            if (IsMiLMMTModelExists(profile))
            {
                worker.ReportProgress(100);
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                txtProgress.Text = Localizer.GetString("downloader.worker.progress.download.milmmt");
            });
            DownloadMiLMMTModel(profile, downloadCancellation.Token).GetAwaiter().GetResult();
            worker.ReportProgress(100);
        }

        [TraceMethod]
        private async Task DownloadMiLMMTModel(MiLMMTModelProfile profile, CancellationToken cancellationToken)
        {
            await DownloadModelAsync(
                profile.DownloadUrl,
                profile.DirectoryPath,
                profile.FileName,
                cancellationToken,
                downloadedPath =>
                {
                    worker.ReportProgress(90);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        txtProgress.Text = Localizer.GetString("downloader.worker.progress.hash.model.milmmt");
                    });
                    return CheckMiLMMTModelFile(profile, downloadedPath);
                },
                profile.FileSize,
                profile.DisplayName);
        }

        [TraceMethod]
        private async Task DownloadModelAsync(
            string url,
            string modelDirectory,
            string fileName,
            CancellationToken cancellationToken,
            Func<string, bool> verifyDownloadedFile,
            long? expectedBytes = null,
            string? displayName = null)
        {
            Directory.CreateDirectory(modelDirectory);

            var modelPath = Path.Combine(modelDirectory, fileName);
            var tempPath = $"{modelPath}.download";
            var downloadDisplayName = displayName ?? fileName;

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                pbDownloader.Value = 0;
                txtDownloaderFilename.Text = downloadDisplayName;
                txtDownloaderBytes.Text = string.Empty;
            });

            try
            {
                HttpResponseMessage response;
                using (var headersTimeout = CreateStallTimeout(cancellationToken))
                {
                    response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, headersTimeout.Token);
                }

                using var responseScope = response;
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? expectedBytes;
                await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var destination = File.Create(tempPath))
                {
                    var buffer = new byte[1024 * 1024];
                    var progressUpdateTimer = Stopwatch.StartNew();
                    long downloadedBytes = 0;
                    int bytesRead;

                    while ((bytesRead = await ReadWithStallTimeoutAsync(source, buffer, cancellationToken)) > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        downloadedBytes += bytesRead;

                        if (progressUpdateTimer.ElapsedMilliseconds < 100)
                        {
                            continue;
                        }

                        progressUpdateTimer.Restart();
                        if (totalBytes is > 0)
                        {
                            var progress = Math.Min(100, downloadedBytes * 100d / totalBytes.Value);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                pbDownloader.Value = progress;
                                txtDownloaderBytes.Text = string.Format(
                                    Localizer.GetString("downloader.worker.progress.download.bytes.total"),
                                    ToMiB(downloadedBytes),
                                    ToMiB(totalBytes.Value));
                            });
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                txtDownloaderBytes.Text = string.Format(
                                    Localizer.GetString("downloader.worker.progress.download.bytes"),
                                    ToMiB(downloadedBytes));
                            });
                        }
                    }
                }

                // Verify while the file is still at the temporary path: the installed path counts as a usable
                // model elsewhere (e.g. Settings recovery), so only a verified file may ever be moved there.
                if (!verifyDownloadedFile(tempPath))
                {
                    throw new InvalidDataException($"Downloaded model failed the integrity check: {url}");
                }

                // Cancelled before installing: discard the file (the catch below) instead of installing it.
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                }

                File.Move(tempPath, modelPath);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                TryDeleteFile(tempPath);
                throw new TimeoutException(
                    $"Model download delivered no data for {DownloadStallTimeout.TotalSeconds:0} seconds: {url}", ex);
            }
            catch
            {
                TryDeleteFile(tempPath);
                throw;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                pbDownloader.Value = 100;
                txtDownloaderFilename.Text = downloadDisplayName;
                txtDownloaderBytes.Text = Localizer.GetString("downloader.worker.progress.download.complete");
            });
        }

        private static CancellationTokenSource CreateStallTimeout(CancellationToken cancellationToken)
        {
            var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DownloadStallTimeout);
            return timeout;
        }

        private static async Task<int> ReadWithStallTimeoutAsync(
            Stream source,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            using var readTimeout = CreateStallTimeout(cancellationToken);
            return await source.ReadAsync(buffer.AsMemory(), readTimeout.Token);
        }

        private static double ToMiB(long bytes)
        {
            return bytes / 1024d / 1024d;
        }

        [TraceMethod]
        private bool IsMiLMMTModelExists(MiLMMTModelProfile profile)
        {
            if (!File.Exists(profile.FilePath))
            {
                return false;
            }

            return CheckMiLMMTModelFile(profile, profile.FilePath);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to delete file {Path}", path);
            }
        }

        [TraceMethod]
        private static bool CheckMiLMMTModelFile(MiLMMTModelProfile profile, string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length != profile.FileSize)
            {
                Log.Warning(
                    "MiLMMT model size mismatch. Expected: {ExpectedSize}, Actual: {ActualSize}",
                    profile.FileSize,
                    fileInfo.Length);
                return false;
            }

            return CheckSha256(filePath, profile.Sha256);
        }

        [TraceMethod]
        private static bool CheckSha256(string filePath, string hash)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return hashStr == hash;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!isInitialized)
            {
                e.Cancel = true;
                return;
            }

            // Closing the window (e.g. Alt+F4) cancels the download, but the window stays open until the worker
            // has unwound and removed the partial file; RunWorkerCompleted then closes it and reports failure.
            // BackgroundWorker clears IsBusy before RunWorkerCompleted, so the window's own Close() still works.
            if (worker.IsBusy)
            {
                e.Cancel = true;
                if (!downloadCancellation.IsCancellationRequested)
                {
                    Log.Information("Model download was cancelled by the user.");
                    downloadCancellation.Cancel();
                }
            }
        }
    }
}
