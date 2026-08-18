using System.Windows;
using System.Windows.Controls;

namespace WeeklyManager
{
    public partial class WeeklyChecklistView : UserControl
    {
        public static readonly DependencyProperty ShowOpenWindowButtonProperty =
            DependencyProperty.Register(
                nameof(ShowOpenWindowButton),
                typeof(bool),
                typeof(WeeklyChecklistView),
                new PropertyMetadata(false));

        public bool ShowOpenWindowButton
        {
            get => (bool)GetValue(ShowOpenWindowButtonProperty);
            set => SetValue(ShowOpenWindowButtonProperty, value);
        }

        public WeeklyChecklistView()
        {
            InitializeComponent();
        }
    }
}
