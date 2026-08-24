using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameRoutines
{
    public partial class RoutineScheduleEditor : UserControl
    {
        public static readonly DependencyProperty LabelColumnWidthProperty = DependencyProperty.Register(
            nameof(LabelColumnWidth),
            typeof(GridLength),
            typeof(RoutineScheduleEditor),
            new PropertyMetadata(new GridLength(190)));

        public GridLength LabelColumnWidth
        {
            get => (GridLength)GetValue(LabelColumnWidthProperty);
            set => SetValue(LabelColumnWidthProperty, value);
        }

        public IReadOnlyList<ResetCadence> ResetCadences { get; } =
            Enum.GetValues(typeof(ResetCadence)).Cast<ResetCadence>().ToList();

        public IReadOnlyList<DayOfWeek> DaysOfWeek { get; } =
            Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();

        public event EventHandler ScheduleChanged;

        public RoutineScheduleEditor()
        {
            InitializeComponent();
        }

        private void ScheduleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            (sender as ComboBox)?.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
            RaiseScheduleChanged();
        }

        private void ResetDayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            (sender as ComboBox)?.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
            if ((DataContext as RoutineSettings)?.ResetCadence == ResetCadence.Weekly)
            {
                RaiseScheduleChanged();
            }
        }

        private void StartingDatePicker_SelectedDateChanged(
            object sender,
            SelectionChangedEventArgs args)
        {
            (sender as DatePicker)?.GetBindingExpression(DatePicker.SelectedDateProperty)?.UpdateSource();
            if ((DataContext as RoutineSettings)?.ResetCadence == ResetCadence.BiWeekly)
            {
                RaiseScheduleChanged();
            }
        }

        private void RaiseScheduleChanged()
        {
            if (IsLoaded)
            {
                ScheduleChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void TimeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            if (!(sender is TextBox textBox) || args.Text.Length != 1)
            {
                args.Handled = true;
                return;
            }

            var character = args.Text[0];
            if (character >= '0' && character <= '9')
            {
                if (textBox.SelectionLength == 0 &&
                    textBox.CaretIndex == 2 &&
                    textBox.Text.IndexOf(':') < 0 &&
                    textBox.Text.Length >= 2 &&
                    textBox.Text.Length < textBox.MaxLength)
                {
                    textBox.Text = textBox.Text.Insert(2, ":");
                    textBox.CaretIndex = 3;
                }

                return;
            }

            if (character == ':')
            {
                var textWithoutSelection = textBox.Text.Remove(
                    textBox.SelectionStart,
                    textBox.SelectionLength);
                if (textWithoutSelection.IndexOf(':') < 0 &&
                    textBox.SelectionStart >= 1 &&
                    textBox.SelectionStart <= 2)
                {
                    return;
                }
            }

            args.Handled = true;
        }

        private void TimeTextBox_TextChanged(object sender, TextChangedEventArgs args)
        {
            if (!(sender is TextBox textBox) ||
                !ScheduleCalculator.TryNormalizeTimeInput(textBox.Text, out var normalizedValue) ||
                !string.Equals(textBox.Text, normalizedValue, StringComparison.Ordinal))
            {
                return;
            }

            var previous = (DataContext as RoutineSettings)?.ResetTime;
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (!string.Equals(previous, normalizedValue, StringComparison.Ordinal))
            {
                RaiseScheduleChanged();
            }
        }

        private void TimeTextBox_PreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter && sender is TextBox textBox)
            {
                CommitTimeTextBox(textBox);
                args.Handled = true;
                Keyboard.ClearFocus();
            }
        }

        private void TimeTextBox_PreviewLostKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs args)
        {
            if (sender is TextBox textBox)
            {
                CommitTimeTextBox(textBox);
            }
        }

        private void TimeTextBox_Pasting(object sender, DataObjectPastingEventArgs args)
        {
            if (!(sender is TextBox textBox) ||
                !args.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true) ||
                !(args.SourceDataObject.GetData(DataFormats.UnicodeText, true) is string pastedText) ||
                !ScheduleCalculator.TryNormalizeTimeInput(pastedText, out var normalizedValue))
            {
                args.CancelCommand();
                return;
            }

            textBox.Text = normalizedValue;
            textBox.CaretIndex = normalizedValue.Length;
            args.CancelCommand();
        }

        private void CommitTimeTextBox(TextBox textBox)
        {
            if (!ScheduleCalculator.TryNormalizeTimeInput(textBox.Text, out var normalizedValue))
            {
                normalizedValue = "00:00";
            }

            var previous = (DataContext as RoutineSettings)?.ResetTime;
            if (!string.Equals(textBox.Text, normalizedValue, StringComparison.Ordinal))
            {
                textBox.Text = normalizedValue;
                textBox.CaretIndex = normalizedValue.Length;
            }

            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (!string.Equals(previous, normalizedValue, StringComparison.Ordinal))
            {
                RaiseScheduleChanged();
            }
        }
    }
}
