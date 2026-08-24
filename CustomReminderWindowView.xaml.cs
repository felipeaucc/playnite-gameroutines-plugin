using System.Windows;
using System.Windows.Controls;

namespace GameRoutines
{
    public partial class CustomReminderWindowView : UserControl
    {
        public CustomReminderWindowView()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs args)
        {
            if (DataContext is CustomReminderViewModel viewModel && viewModel.TrySave())
            {
                Window.GetWindow(this)?.Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs args)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
