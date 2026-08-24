using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameRoutines
{
    public partial class CustomReminderEditor : UserControl
    {
        public static readonly DependencyProperty EnabledVerticalOffsetProperty = DependencyProperty.Register(
            nameof(EnabledVerticalOffset),
            typeof(double),
            typeof(CustomReminderEditor),
            new PropertyMetadata(0d));

        public static readonly DependencyProperty FormMarginProperty = DependencyProperty.Register(
            nameof(FormMargin),
            typeof(Thickness),
            typeof(CustomReminderEditor),
            new PropertyMetadata(new Thickness(0, 3, 0, 0)));

        public static readonly DependencyProperty LabelRowMarginProperty = DependencyProperty.Register(
            nameof(LabelRowMargin),
            typeof(Thickness),
            typeof(CustomReminderEditor),
            new PropertyMetadata(new Thickness(0, 5, 10, 5)));

        public static readonly DependencyProperty FieldRowMarginProperty = DependencyProperty.Register(
            nameof(FieldRowMargin),
            typeof(Thickness),
            typeof(CustomReminderEditor),
            new PropertyMetadata(new Thickness(0, 5, 0, 5)));

        public double EnabledVerticalOffset
        {
            get => (double)GetValue(EnabledVerticalOffsetProperty);
            set => SetValue(EnabledVerticalOffsetProperty, value);
        }

        public Thickness FormMargin
        {
            get => (Thickness)GetValue(FormMarginProperty);
            set => SetValue(FormMarginProperty, value);
        }

        public Thickness LabelRowMargin
        {
            get => (Thickness)GetValue(LabelRowMarginProperty);
            set => SetValue(LabelRowMarginProperty, value);
        }

        public Thickness FieldRowMargin
        {
            get => (Thickness)GetValue(FieldRowMarginProperty);
            set => SetValue(FieldRowMarginProperty, value);
        }

        public IReadOnlyList<DayOfWeek> DaysOfWeek { get; } =
            Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();

        public IReadOnlyList<ReminderCadence> ReminderCadences { get; } =
            Enum.GetValues(typeof(ReminderCadence)).Cast<ReminderCadence>().ToList();

        public CustomReminderEditor()
        {
            InitializeComponent();
        }

        private void TimeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!(sender is TextBox textBox) || e.Text.Length != 1)
            {
                e.Handled = true;
                return;
            }

            var character = e.Text[0];
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

            e.Handled = true;
        }

        private void TimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox &&
                ScheduleCalculator.TryNormalizeTimeInput(textBox.Text, out var normalizedValue) &&
                string.Equals(textBox.Text, normalizedValue, StringComparison.Ordinal))
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
        }

        private void TimeTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                CommitTimeTextBox(textBox);
                e.Handled = true;
                Keyboard.ClearFocus();
            }
        }

        private void TimeTextBox_PreviewLostKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                CommitTimeTextBox(textBox);
            }
        }

        private void TimeTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!(sender is TextBox textBox) ||
                !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true) ||
                !(e.SourceDataObject.GetData(DataFormats.UnicodeText, true) is string pastedText) ||
                !ScheduleCalculator.TryNormalizeTimeInput(pastedText, out var normalizedValue))
            {
                e.CancelCommand();
                return;
            }

            textBox.Text = normalizedValue;
            textBox.CaretIndex = normalizedValue.Length;
            e.CancelCommand();
        }

        private static void CommitTimeTextBox(TextBox textBox)
        {
            if (!ScheduleCalculator.TryNormalizeTimeInput(textBox.Text, out var normalizedValue))
            {
                normalizedValue = "00:00";
            }

            if (!string.Equals(textBox.Text, normalizedValue, StringComparison.Ordinal))
            {
                textBox.Text = normalizedValue;
                textBox.CaretIndex = normalizedValue.Length;
            }

            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }
}
