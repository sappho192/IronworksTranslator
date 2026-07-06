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

        public InitializationWindow(
            TranslatorEngine? requestedEngine = null,
            MiLMMTModelProfile? requestedMiLMMTProfile = null)
        {
            this.requestedEngine = requestedEngine;
            this.requestedMiLMMTProfile = requestedMiLMMTProfile;
            InitializeComponent();
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
            if (e.Error != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Log.Error(e.Error, "Initialization failed.");
                    txtProgress.Text = Localizer.GetString("downloader.error.initialization");
                    txtDownloaderFilename.Text = e.Error.Message;
                    txtDownloaderBytes.Text = string.Empty;
                    prWorker.Visibility = Visibility.Collapsed;
                    MessageBox.Show(Localizer.GetString("downloader.error.initialization"));
                    App.RequestShutdown();
                });
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
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
                case TranslatorEngine.MiLLMT:
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
            DownloadMiLMMTModel(profile).GetAwaiter().GetResult();
            worker.ReportProgress(90);
            Application.Current.Dispatcher.Invoke(() =>
            {
                txtProgress.Text = Localizer.GetString("downloader.worker.progress.hash.model.milmmt");
            });
            CheckMiLMMTModelIntegrity(profile);
            worker.ReportProgress(100);
        }

        [TraceMethod]
        private async Task DownloadMiLMMTModel(MiLMMTModelProfile profile)
        {
            await DownloadModelAsync(profile.DownloadUrl, profile.DirectoryPath, profile.FileName, profile.FileSize);
        }

        [TraceMethod]
        private async Task DownloadModelAsync(
            string url,
            string modelDirectory,
            string fileName,
            long? expectedBytes = null)
        {
            Directory.CreateDirectory(modelDirectory);

            var modelPath = Path.Combine(modelDirectory, fileName);
            var tempPath = $"{modelPath}.download";

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                pbDownloader.Value = 0;
                txtDownloaderFilename.Text = fileName;
                txtDownloaderBytes.Text = string.Empty;
            });

            try
            {
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? expectedBytes;
                await using (var source = await response.Content.ReadAsStreamAsync())
                await using (var destination = File.Create(tempPath))
                {
                    var buffer = new byte[1024 * 1024];
                    var progressUpdateTimer = Stopwatch.StartNew();
                    long downloadedBytes = 0;
                    int bytesRead;

                    while ((bytesRead = await source.ReadAsync(buffer)) > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
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

                if (File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                }

                File.Move(tempPath, modelPath);
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
                throw;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                pbDownloader.Value = 100;
                txtDownloaderFilename.Text = fileName;
                txtDownloaderBytes.Text = Localizer.GetString("downloader.worker.progress.download.complete");
            });
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

            return CheckMiLMMTModelFile(profile);
        }

        [TraceMethod]
        private void CheckMiLMMTModelIntegrity(MiLMMTModelProfile profile)
        {
            var modelIntegrity = File.Exists(profile.FilePath)
                && CheckMiLMMTModelFile(profile);

            if (!modelIntegrity)
            {
                MessageBox.Show(Localizer.GetString("downloader.error.milmmt.hash"));
                Application.Current.Dispatcher.Invoke(() =>
                {
                    App.RequestShutdown();
                });
            }
        }

        [TraceMethod]
        private static bool CheckMiLMMTModelFile(MiLMMTModelProfile profile)
        {
            var fileInfo = new FileInfo(profile.FilePath);
            if (fileInfo.Length != profile.FileSize)
            {
                Log.Warning(
                    "MiLLMT model size mismatch. Expected: {ExpectedSize}, Actual: {ActualSize}",
                    profile.FileSize,
                    fileInfo.Length);
                return false;
            }

            return CheckSha256(profile.FilePath, profile.Sha256);
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
            }
        }
    }
}
