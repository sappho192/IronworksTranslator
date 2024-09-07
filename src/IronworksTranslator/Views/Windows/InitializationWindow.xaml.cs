using Downloader;
using Serilog;
using System.ComponentModel;
using System.IO;
using System.Net;
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
        private readonly DownloadService downloader = new(new DownloadConfiguration
        {
            RequestConfiguration =
            {
                Proxy = WebRequest.GetSystemWebProxy()
            }
        });
        private readonly string modelDir = Path.Combine("data", "model", "aihub-ja-ko-translator");

        public InitializationWindow()
        {
            InitializeComponent();
            worker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
            };
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            InitDownloader();
            isInitialized = true;

            worker.RunWorkerAsync();
        }

        private void InitDownloader()
        {
            downloader.DownloadStarted += Downloader_DownloadStarted;
            downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
            downloader.DownloadFileCompleted += Downloader_DownloadFileCompleted;
        }

        private void Downloader_DownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    txtDownloaderFilename.Text = $"Cancelled!";
                    Log.Fatal("Download cancelled");
                    App.RequestShutdown();
                });
            }
            else if (e.Error != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Log.Error(e.Error.Message);
                    App.RequestShutdown();
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    txtDownloaderFilename.Text = $"Downloaded!";
                });
            }
        }

        private void Downloader_DownloadProgressChanged(object? sender, Downloader.DownloadProgressChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                pbDownloader.Value = e.ProgressPercentage;
            });
        }

        private void Downloader_DownloadStarted(object? sender, DownloadStartedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                txtDownloaderFilename.Text = $"Downloading {e.FileName}";
            });
        }

        private void Worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                txtProgress.Text = "준비 완료";
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
                txtProgress.Text = "번역 모델 준비";
            });
            // Check translation model exists
            (var encoderExists, var decoderExists) = IsModelExists();
            worker.ReportProgress(30);
            if (!encoderExists)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    txtProgress.Text = "Ironworks (Ja→Ko) Encoder 다운로드";
                });
                DownloadEncoderModel().Wait();
                worker.ReportProgress(60);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    txtProgress.Text = "인코더 모델 파일 검사";
                });
                CheckEncoderModelIntegrity();
            }
            if (!decoderExists)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    txtProgress.Text = "Ironworks (Ja→Ko) Decoder 다운로드";
                });
                DownloadDecoderModel().Wait();
                worker.ReportProgress(90);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    txtProgress.Text = "인코더 모델 파일 검사";
                });
                CheckEncoderModelIntegrity();
            }

            worker.ReportProgress(100);
        }

        private const string MODEL_ENCODER_URL = "https://huggingface.co/sappho192/aihub-ja-ko-translator/resolve/main/onnx/encoder_model.onnx";
        private const string MODEL_DECODER_URL = "https://huggingface.co/sappho192/aihub-ja-ko-translator/resolve/main/onnx/decoder_model_merged.onnx";
        private const string MODEL_ENCODER_HASH = "1e39281ac696b2919ae65fa81e71849e";
        private const string MODEL_DECODER_HASH = "cee4c3c306fae640f6a11f9795ea4be3";
        private const string MODEL_ENCODER_FILENAME = "encoder_model.onnx";
        private const string MODEL_DECODER_FILENAME = "decoder_model_merged.onnx";

        private void CheckEncoderModelIntegrity()
        {
            string encoderPath = Path.Combine(modelDir, "encoder_model.onnx");
            bool encoderIntegrity = false;
            // Check encoder file
            if (File.Exists(encoderPath))
            {
                if (CheckHash(encoderPath, MODEL_ENCODER_HASH))
                {
                    encoderIntegrity = true;
                }
            }

            if (!encoderIntegrity)
            {
                MessageBox.Show("인코더 모델 다운로드에 실패했습니다. 프로그램을 다시 실행해주세요.");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    App.RequestShutdown();
                });
            }
        }

        private void CheckDecoderModelIntegrity()
        {
            string decoderPath = Path.Combine(modelDir, "decoder_model_merged.onnx");
            bool decoderIntegrity = false;
            if (File.Exists(decoderPath))
            {
                if (CheckHash(decoderPath, MODEL_DECODER_HASH))
                {
                    decoderIntegrity = true;
                }
            }
            if (!decoderIntegrity)
            {
                MessageBox.Show("디코더 모델 다운로드에 실패했습니다. 프로그램을 다시 실행해주세요.");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    App.RequestShutdown();
                });
            }
        }

        private async Task DownloadEncoderModel()
        {
            var modelArchivePath = Path.Combine(modelDir, MODEL_ENCODER_FILENAME);
            if (File.Exists(modelArchivePath))
            {
                File.Delete(modelArchivePath);
            }

            // Get current directoryinfo
            var directoryInfo = new DirectoryInfo(modelDir);
            downloader.DownloadFileTaskAsync(MODEL_ENCODER_URL, directoryInfo).Wait();
        }
        private async Task DownloadDecoderModel()
        {
            var modelArchivePath = Path.Combine(modelDir, MODEL_DECODER_FILENAME);
            if (File.Exists(modelArchivePath))
            {
                File.Delete(modelArchivePath);
            }

            // Get current directoryinfo
            var directoryInfo = new DirectoryInfo(modelDir);
            downloader.DownloadFileTaskAsync(MODEL_DECODER_URL, directoryInfo).Wait();
        }

        private (bool, bool) IsModelExists()
        {
            string encoderPath = Path.Combine(modelDir, "encoder_model.onnx");
            string decoderPath = Path.Combine(modelDir, "decoder_model_merged.onnx");
            bool encoderExists = false;
            bool decoderExists = false;

            // Check encoder file
            if (File.Exists(encoderPath))
            {
                if (CheckHash(encoderPath, MODEL_ENCODER_HASH))
                {
                    encoderExists = true;
                }
            }
            if (File.Exists(decoderPath))
            {
                if (CheckHash(decoderPath, MODEL_DECODER_HASH))
                {
                    decoderExists = true;
                }
            }

            return (encoderExists, decoderExists);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!isInitialized)
            {
                e.Cancel = true;
            }
        }

        private static bool CheckHash(string filePath, string hash)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = md5.ComputeHash(stream);
            var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return hashStr == hash;
        }
    }
}
