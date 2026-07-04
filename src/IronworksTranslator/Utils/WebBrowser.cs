using PuppeteerSharp;

namespace IronworksTranslator.Utils
{
    public class WebBrowser : IDisposable
    {
        private Browser? browser;
        private Page? webPage;
        private readonly object lockObj = new();
        private bool disposed;

        public WebBrowser()
        {
            InitWebBrowser();
        }

        ~WebBrowser()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            lock (lockObj)
            {
                try
                {
                    webPage?.CloseAsync().GetAwaiter().GetResult();
                    browser?.CloseAsync().GetAwaiter().GetResult();
                }
                catch
                {
                }

                webPage?.Dispose();
                browser?.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        public string Navigate(string url)
        {
            string content = string.Empty;
            lock (lockObj)
            {
                try
                {
                    var navigateTask = Task.Run(async () => await RequestNavigate(url));
                    content = navigateTask.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    content = ex.Message;
                }
            }
            return content;
        }

        public string EvaluateExpression(string expression)
        {
            string result = string.Empty;
            lock (lockObj)
            {
                try
                {
                    var evaluateTask = Task.Run(async () => await RequestEvaluateExpression(expression));
                    result = evaluateTask.GetAwaiter().GetResult() ?? string.Empty;
                }
                catch
                {
                    result = string.Empty;
                }
            }
            return result;
        }

        private async Task<Browser> InitBrowser()
        {
            // DefaultBuildId will be updated when upgrading the PuppeteerSharp.
            await new BrowserFetcher().DownloadAsync(PuppeteerSharp.BrowserData.Chrome.DefaultBuildId);
            var _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Args = [
                    "--js-flags=\"--max-old-space-size=128\"" // Limits memory usage to 128MB
                ],
            });
            return (Browser)_browser; // Cast IBrowser to Browser
        }

        private void InitWebBrowser()
        {
            var browserTask = Task.Run(InitBrowser);
            browser = browserTask.GetAwaiter().GetResult();
            var pageTask = Task.Run(async () => await browser.NewPageAsync());
            webPage = (Page)pageTask.GetAwaiter().GetResult(); // Cast IPage to Page
            webPage.DefaultTimeout = 0;
        }

#pragma warning disable CS8602
        private async Task<string> RequestNavigate(string url)
        {
            await webPage.GoToAsync(url, WaitUntilNavigation.Networkidle2);
            var content = await webPage.GetContentAsync();

            return content;
        }

        private async Task<string?> RequestEvaluateExpression(string expression)
        {
            return await webPage.EvaluateExpressionAsync<string?>(expression);
        }
#pragma warning restore CS8602
    }
}
