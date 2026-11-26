using PuppeteerSharp;
using Serilog;

namespace IronworksTranslator.Utils
{
    /// <summary>
    /// A single headless Chromium page. Not thread-safe: callers must serialize access.
    /// </summary>
    public sealed class WebBrowser : IDisposable
    {
        private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(15);

        private IBrowser? browser;
        private IPage? webPage;
        private readonly object lockObj = new();
        private bool disposed;

        private WebBrowser()
        {
        }

        ~WebBrowser()
        {
            Dispose();
        }

        public static async Task<WebBrowser> CreateAsync()
        {
            var webBrowser = new WebBrowser();
            try
            {
                // DefaultBuildId will be updated when upgrading the PuppeteerSharp.
                await new BrowserFetcher()
                    .DownloadAsync(PuppeteerSharp.BrowserData.Chrome.DefaultBuildId)
                    .ConfigureAwait(false);
                webBrowser.browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Args = [
                        "--js-flags=\"--max-old-space-size=128\"" // Limits memory usage to 128MB
                    ],
                }).ConfigureAwait(false);
                webBrowser.webPage = await webBrowser.browser.NewPageAsync().ConfigureAwait(false);
                // Never wait forever: a page that never settles must fail instead of hanging translation.
                webBrowser.webPage.DefaultTimeout = (int)DefaultOperationTimeout.TotalMilliseconds;
                webBrowser.webPage.DefaultNavigationTimeout = (int)DefaultOperationTimeout.TotalMilliseconds;
                return webBrowser;
            }
            catch
            {
                // The caller never receives this instance, so close the launched Chromium now
                // instead of leaking it until the finalizer runs.
                webBrowser.Dispose();
                throw;
            }
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

        /// <summary>
        /// Navigates and returns once the DOM is parsed. Pages with long-lived connections
        /// (like Papago) never reach network idle, so callers should wait for a selector instead.
        /// </summary>
        public async Task NavigateAsync(string url, TimeSpan timeout)
        {
            await GetPage().GoToAsync(url, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
                Timeout = (int)timeout.TotalMilliseconds,
            }).ConfigureAwait(false);
        }

        public async Task<bool> WaitForSelectorAsync(string selector, TimeSpan timeout)
        {
            try
            {
                await GetPage().WaitForSelectorAsync(selector, new WaitForSelectorOptions
                {
                    Timeout = (int)timeout.TotalMilliseconds,
                }).ConfigureAwait(false);
                return true;
            }
            catch (WaitTaskTimeoutException)
            {
                return false;
            }
        }

        public async Task<string> EvaluateExpressionAsync(string expression)
        {
            try
            {
                return await GetPage().EvaluateExpressionAsync<string?>(expression).ConfigureAwait(false)
                    ?? string.Empty;
            }
            catch (Exception ex) when (ex is EvaluationFailedException or PuppeteerException)
            {
                // The page may be mid-navigation (execution context destroyed); treat as "nothing yet".
                Log.Debug(ex, "Browser expression evaluation failed.");
                return string.Empty;
            }
        }

        public Task<string> GetContentAsync()
        {
            return GetPage().GetContentAsync();
        }

        /// <summary>
        /// Replaces the content of an editable element the way a user would (select all, delete, type),
        /// so rich editors such as Lexical receive real input events.
        /// </summary>
        public async Task ReplaceEditorTextAsync(string selector, string text, TimeSpan timeout)
        {
            var page = GetPage();
            // Wait instead of querying once: client-side re-renders can briefly remove the element.
            var editor = await page.WaitForSelectorAsync(selector, new WaitForSelectorOptions
            {
                Timeout = (int)timeout.TotalMilliseconds,
            }).ConfigureAwait(false);
            await editor.FocusAsync().ConfigureAwait(false);
            await page.Keyboard.DownAsync("Control").ConfigureAwait(false);
            await page.Keyboard.PressAsync("A").ConfigureAwait(false);
            await page.Keyboard.UpAsync("Control").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Backspace").ConfigureAwait(false);
            if (!string.IsNullOrEmpty(text))
            {
                // Sends CDP Input.insertText, which inserts the whole string as one input event.
                await page.Keyboard.SendCharacterAsync(text).ConfigureAwait(false);
            }
        }

        private IPage GetPage()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return webPage ?? throw new InvalidOperationException("Browser page is not initialized.");
        }
    }
}
