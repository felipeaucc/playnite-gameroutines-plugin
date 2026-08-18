using System.Windows;
using System.Windows.Controls;

namespace GameRoutines
{
    public partial class BlockedManualStateWarningView : UserControl
    {
        public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;

        public BlockedManualStateWarningView()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
