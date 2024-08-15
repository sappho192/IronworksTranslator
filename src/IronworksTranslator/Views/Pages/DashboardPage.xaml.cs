﻿using IronworksTranslator.ViewModels.Pages;
using IronworksTranslator.Views.Windows;
using Wpf.Ui.Controls;

namespace IronworksTranslator.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        private void tsShowChatWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ui)
            {
                var chatWindow = App.GetService<ChatWindow>();
                if (ui.IsChecked == true)
                {
                    chatWindow.Show();
                }
                else
                {
                    chatWindow.Hide();
                }
            }
        }

        private void tsDragChatWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ui)
            {
                var chatWindow = App.GetService<ChatWindow>();
                if (ui.IsChecked == true)
                {
                    chatWindow.ViewModel.IsDraggable = true;
                    
                }
                else
                {
                    chatWindow.ViewModel.IsDraggable = false;
                }
            }
        }

        private void tsShowWindowGrip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch ui)
            {
                var chatWindow = App.GetService<ChatWindow>();
                if (ui.IsChecked == true)
                {
                    chatWindow.ResizeMode = ResizeMode.CanResizeWithGrip;
                }
                else
                {
                    chatWindow.ResizeMode = ResizeMode.NoResize;
                }
            }
        }
    }
}
