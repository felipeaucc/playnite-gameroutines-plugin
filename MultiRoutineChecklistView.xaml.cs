using System.Windows;
using System.Windows.Controls;

namespace GameRoutines
{
    public partial class MultiRoutineChecklistView : UserControl
    {
        public static readonly DependencyProperty ShowGameHeaderProperty = DependencyProperty.Register(
            nameof(ShowGameHeader),
            typeof(bool),
            typeof(MultiRoutineChecklistView),
            new PropertyMetadata(true));

        public static readonly DependencyProperty ShowPopOutAllButtonProperty = DependencyProperty.Register(
            nameof(ShowPopOutAllButton),
            typeof(bool),
            typeof(MultiRoutineChecklistView),
            new PropertyMetadata(false));

        public static readonly DependencyProperty ShowRoutinePopOutButtonsProperty = DependencyProperty.Register(
            nameof(ShowRoutinePopOutButtons),
            typeof(bool),
            typeof(MultiRoutineChecklistView),
            new PropertyMetadata(true));

        public static readonly DependencyProperty ShowNewChecklistButtonProperty = DependencyProperty.Register(
            nameof(ShowNewChecklistButton),
            typeof(bool),
            typeof(MultiRoutineChecklistView),
            new PropertyMetadata(false));

        public static readonly DependencyProperty ShowDeleteChecklistButtonsProperty = DependencyProperty.Register(
            nameof(ShowDeleteChecklistButtons),
            typeof(bool),
            typeof(MultiRoutineChecklistView),
            new PropertyMetadata(false));

        public bool ShowGameHeader
        {
            get => (bool)GetValue(ShowGameHeaderProperty);
            set => SetValue(ShowGameHeaderProperty, value);
        }

        public bool ShowPopOutAllButton
        {
            get => (bool)GetValue(ShowPopOutAllButtonProperty);
            set => SetValue(ShowPopOutAllButtonProperty, value);
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

        public MultiRoutineChecklistView()
        {
            InitializeComponent();
        }
    }
}
