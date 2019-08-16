using IronworksTranslator.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
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
