using System.Windows;
using System.Windows.Controls;

namespace GameRoutines
{
    public partial class RoutineChecklistCard : UserControl
    {
        public static readonly DependencyProperty ShowPopOutButtonProperty = DependencyProperty.Register(
            nameof(ShowPopOutButton),
            typeof(bool),
            typeof(RoutineChecklistCard),
            new PropertyMetadata(true));

        public static readonly DependencyProperty ShowDeleteButtonProperty = DependencyProperty.Register(
            nameof(ShowDeleteButton),
            typeof(bool),
            typeof(RoutineChecklistCard),
            new PropertyMetadata(false));

        public bool ShowPopOutButton
        {
            get => (bool)GetValue(ShowPopOutButtonProperty);
            set => SetValue(ShowPopOutButtonProperty, value);
        }

        public bool ShowDeleteButton
        {
            get => (bool)GetValue(ShowDeleteButtonProperty);
            set => SetValue(ShowDeleteButtonProperty, value);
        }

        public RoutineChecklistCard()
        {
            InitializeComponent();
        }
    }
}
