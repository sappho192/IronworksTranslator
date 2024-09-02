using PuppeteerSharp;

namespace IronworksTranslator.Utils
{
    public class WebBrowser
    {
        private Browser? browser;
        private Page? webPage;
        private readonly object lockObj = new();
        public WebBrowser()
        {
            InitWebBrowser();
        }

        ~WebBrowser()
        {
            webPage?.CloseAsync();
            browser?.CloseAsync();
            webPage?.Dispose();
            browser?.Dispose();
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
#pragma warning restore CS8602
    }
}
