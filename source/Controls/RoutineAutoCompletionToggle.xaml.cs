using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace GameRoutines
{
    public partial class RoutineAutoCompletionToggle : UserControl
    {
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            nameof(IsChecked),
            typeof(bool),
            typeof(RoutineAutoCompletionToggle),
            new PropertyMetadata(false));

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(RoutineAutoCompletionToggle),
            new PropertyMetadata(null));

        public static readonly DependencyProperty ToolTipTextProperty = DependencyProperty.Register(
            nameof(ToolTipText),
            typeof(string),
            typeof(RoutineAutoCompletionToggle),
            new PropertyMetadata(string.Empty));

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public string ToolTipText
        {
            get => (string)GetValue(ToolTipTextProperty);
            set => SetValue(ToolTipTextProperty, value);
        }

        public RoutineAutoCompletionToggle()
        {
            InitializeComponent();
        }

        private void AutomaticCompletionButton_Click(object sender, RoutedEventArgs args)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                AutomaticCompletionButton.GetBindingExpression(
                    System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateTarget();
            }), DispatcherPriority.DataBind);
        }
    }
}
