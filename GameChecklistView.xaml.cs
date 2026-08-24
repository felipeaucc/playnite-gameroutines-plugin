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

        public static readonly DependencyProperty ShowRoutinePopOutButtonsProperty =
            DependencyProperty.Register(
                nameof(ShowRoutinePopOutButtons),
                typeof(bool),
                typeof(GameChecklistView),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ShowNewChecklistButtonProperty =
            DependencyProperty.Register(
                nameof(ShowNewChecklistButton),
                typeof(bool),
                typeof(GameChecklistView),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ShowDeleteChecklistButtonsProperty =
            DependencyProperty.Register(
                nameof(ShowDeleteChecklistButtons),
                typeof(bool),
                typeof(GameChecklistView),
                new PropertyMetadata(false));

        public bool ShowOpenWindowButton
        {
            get => (bool)GetValue(ShowOpenWindowButtonProperty);
            set => SetValue(ShowOpenWindowButtonProperty, value);
        }

        public bool ShowRoutinePopOutButtons
        {
            get => (bool)GetValue(ShowRoutinePopOutButtonsProperty);
            set => SetValue(ShowRoutinePopOutButtonsProperty, value);
        }

        public bool ShowNewChecklistButton
        {
            get => (bool)GetValue(ShowNewChecklistButtonProperty);
            set => SetValue(ShowNewChecklistButtonProperty, value);
        }

        public bool ShowDeleteChecklistButtons
        {
            get => (bool)GetValue(ShowDeleteChecklistButtonsProperty);
            set => SetValue(ShowDeleteChecklistButtonsProperty, value);
        }

        public GameChecklistView()
        {
            InitializeComponent();
        }
    }
}
