﻿using IronworksTranslator.Core;
using System.Windows;

namespace IronworksTranslator
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            IronworksContext.driver.Dispose();
            IronworksContext.chatFinder.CancelAsync();
            IronworksContext.chatFinder.Dispose();
        }
    }
}
