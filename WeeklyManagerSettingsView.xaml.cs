using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WeeklyManager
{
    public partial class WeeklyManagerSettingsView : UserControl
    {
        public WeeklyManagerSettingsView()
        {
            InitializeComponent();
            Loaded += UserControl_Loaded;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            (DataContext as WeeklyManagerSettingsViewModel)?.RefreshLibraryGames();
        }

        private void GameSearchTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var viewModel = GetViewModel();
            if (viewModel?.SelectedLibraryGame == null)
            {
                viewModel.OpenGameSearch();
            }
        }

        private void GameSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                var viewModel = GetViewModel();
                if (GameSearchTextBox.IsKeyboardFocusWithin && viewModel?.SelectedLibraryGame == null)
                {
                    viewModel.OpenGameSearch();
                }
            }));
        }

        private void GameSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var viewModel = GetViewModel();
            if (viewModel == null)
            {
                return;
            }

            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                viewModel.OpenGameSearch();
                viewModel.MoveGameSearchSelection(e.Key == Key.Down ? 1 : -1);
                GameSearchResultsList.ScrollIntoView(viewModel.SelectedSearchResult);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                viewModel.ConfirmGameSearchSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                viewModel.CloseGameSearch();
                e.Handled = true;
            }
        }

        private void GameSearchResultsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var viewModel = GetViewModel();
            if (viewModel == null)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                viewModel.ConfirmGameSearchSelection();
                GameSearchTextBox.Focus();
                GameSearchTextBox.CaretIndex = GameSearchTextBox.Text.Length;
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                viewModel.CloseGameSearch();
                GameSearchTextBox.Focus();
                e.Handled = true;
            }
        }

        private void GameSearchResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(
                GameSearchResultsList,
                e.OriginalSource as DependencyObject) as ListBoxItem;
            var viewModel = GetViewModel();
            if (item?.DataContext is LibraryGameOption selectedGame && viewModel != null)
            {
                viewModel.SelectedSearchResult = selectedGame;
                viewModel.ConfirmGameSearchSelection();
                GameSearchTextBox.Focus();
                GameSearchTextBox.CaretIndex = GameSearchTextBox.Text.Length;
                e.Handled = true;
            }
        }

        private void GameSearchPopup_Closed(object sender, EventArgs e)
        {
            GetViewModel()?.CloseGameSearch();
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
                WeeklyScheduleCalculator.TryNormalizeTimeInput(textBox.Text, out var normalizedValue) &&
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
                !WeeklyScheduleCalculator.TryNormalizeTimeInput(pastedText, out var normalizedValue))
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
            if (!WeeklyScheduleCalculator.TryNormalizeTimeInput(textBox.Text, out var normalizedValue))
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

        private void ChecklistItemCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox &&
                checkBox.DataContext is ChecklistItemSettings item)
            {
                GetViewModel()?.ChecklistItemChecked(item, checkBox.IsChecked == true);
            }
        }

        private void ChecklistAutoCompletionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                GetViewModel()?.ChecklistAutoCompletionChanged(checkBox.IsChecked == true);
            }
        }

        private void ChecklistItemTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                CommitChecklistItemTextBox(textBox);
                e.Handled = true;
            }
        }

        private void ChecklistItemTextBox_PreviewLostKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                CommitChecklistItemTextBox(textBox);
            }
        }

        private void CommitChecklistItemTextBox(TextBox textBox)
        {
            if (!(textBox.DataContext is ChecklistItemSettings item))
            {
                return;
            }

            GetViewModel()?.CommitChecklistItemText(item, textBox.Text);
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            textBox.CaretIndex = textBox.Text.Length;
        }

        private WeeklyManagerSettingsViewModel GetViewModel()
        {
            return DataContext as WeeklyManagerSettingsViewModel;
        }
    }
}
