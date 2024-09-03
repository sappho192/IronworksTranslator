using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using Serilog;

namespace IronworksTranslator.Utils.UI
{
    /// <summary>
    /// Based on https://stackoverflow.com/a/53868024/4183595 , little bit optimized
    /// </summary>
    public class DragMoveBehavior : Behavior<Window>
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.Register("IsEnabled", typeof(bool), typeof(DragMoveBehavior), new PropertyMetadata(true));

        public bool IsEnabled
        {
            get => (bool)GetValue(IsEnabledProperty);
            set => SetValue(IsEnabledProperty, value);
        }

        protected override void OnAttached()
        {
            AssociatedObject.MouseLeftButtonDown += AssociatedObject_MouseLeftButtonDown;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.MouseLeftButtonDown -= AssociatedObject_MouseLeftButtonDown;
        }

        private void AssociatedObject_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Window window) return;
            // In maximum window state case, window will return normal state and
            // continue moving follow cursor
            if (window.WindowState == WindowState.Maximized)
            {
                window.WindowState = WindowState.Normal;

                // 3 or any where you want to set window location after
                // return from maximum state
                Application.Current.MainWindow.Top = 3;
            }

            e.Handled = true;

            if (!IsEnabled) return;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    window.DragMove();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "DragMoveBehavior");
                }
            }));
        }
    }
}
