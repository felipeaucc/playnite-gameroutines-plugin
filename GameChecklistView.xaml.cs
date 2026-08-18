using System.Windows;
using System.Windows.Controls;

namespace GameRoutines
{
    public partial class GameChecklistView : UserControl
    {
        public static readonly DependencyProperty ShowOpenWindowButtonProperty =
            DependencyProperty.Register(
                nameof(ShowOpenWindowButton),
                typeof(bool),
                typeof(GameChecklistView),
                new PropertyMetadata(false));

        public bool ShowOpenWindowButton
        {
            get => (bool)GetValue(ShowOpenWindowButtonProperty);
            set => SetValue(ShowOpenWindowButtonProperty, value);
        }

        public GameChecklistView()
        {
            InitializeComponent();
        }
    }
}
