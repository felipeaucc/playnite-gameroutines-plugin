using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace GameRoutines
{
    public partial class RoutineStateSwitch : UserControl
    {
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            nameof(IsChecked),
            typeof(bool),
            typeof(RoutineStateSwitch),
            new PropertyMetadata(false));

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(RoutineStateSwitch),
            new PropertyMetadata(null));

        public static readonly DependencyProperty ToolTipTextProperty = DependencyProperty.Register(
            nameof(ToolTipText),
            typeof(string),
            typeof(RoutineStateSwitch),
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

        public RoutineStateSwitch()
        {
            InitializeComponent();
            InitializeThemeStyle();
        }

        private void InitializeThemeStyle()
        {
            var switchStyle = FindCompatibleStyle("SwitcherToggleButton") ??
                FindCompatibleStyle("GameRoutinesRoutineStateSwitchFallbackStyle");
            if (switchStyle != null)
            {
                Resources["GameRoutinesRoutineStateSwitchStyle"] = switchStyle;
            }
        }

        private Style FindCompatibleStyle(string resourceKey)
        {
            var style = TryFindResource(resourceKey) as Style;
            if (style == null ||
                (style.TargetType != null &&
                 !style.TargetType.IsAssignableFrom(typeof(System.Windows.Controls.Primitives.ToggleButton))))
            {
                return null;
            }

            return style;
        }

        private void Switch_Click(object sender, RoutedEventArgs args)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                Switch.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)
                    ?.UpdateTarget();
            }), DispatcherPriority.DataBind);
        }
    }
}
