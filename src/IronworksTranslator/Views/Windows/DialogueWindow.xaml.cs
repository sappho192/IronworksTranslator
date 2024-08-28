using IronworksTranslator.Models.Settings;
using IronworksTranslator.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace IronworksTranslator.Views.Windows
{
    /// <summary>
    /// DialogueWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DialogueWindow : Window
    {
        public DialogueWindowViewModel ViewModel { get; }

        public DialogueWindow(DialogueWindowViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();

#pragma warning disable CS8602
            if (IronworksSettings.Instance.UiSettings.DialogueWindowVisible)
            {
                Show();
            }
            else
            {
                Hide();
            }
            if (IronworksSettings.Instance.ChatUiSettings.IsResizable)
            {
                ResizeMode = ResizeMode.CanResizeWithGrip;
            } else
            {
                ResizeMode = ResizeMode.NoResize;
            }
#pragma warning restore CS8602
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
