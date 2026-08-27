using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GameRoutines
{
    public partial class GameRoutinesSettingsView : UserControl
    {
        private ScrollViewer trackedGamesScrollViewer;
        private DockPanel settingsFooterPanel;
        private double removeSelectedInlineReservedHeight;
        private double approvedRemoveSelectedButtonHeight;
        private bool removeSelectedInFooter;

        public GameRoutinesSettingsView()
        {
            InitializeComponent();
            InitializeThemeResourceAliases();
            Loaded += UserControl_Loaded;
            Unloaded += UserControl_Unloaded;
            TrackedGamesPane.SizeChanged += (sender, args) =>
            {
                UpdateTrackedGamesListMaximumHeight();
                QueueRemoveSelectedPlacementUpdate();
            };
        }

        private void InitializeThemeResourceAliases()
        {
            var iconStyle = FindCompatibleStyle("IconFontStyle", typeof(TextBlock)) ??
                FindCompatibleStyle("GameRoutinesIconTextFallbackStyle", typeof(TextBlock));
            if (iconStyle != null)
            {
                Resources["GameRoutinesSettingsIconTextStyle"] = iconStyle;
            }

            Resources["GameRoutinesSettingsControlHoverBackgroundBrush"] =
                FindThemeBrush("ControlHoverBackgroundBrush", "HoverBrush");
            Resources["GameRoutinesSettingsControlSelectedBackgroundBrush"] =
                FindThemeBrush("ControlSelectedBackgroundBrush", "NormalBrush");
            Resources["GameRoutinesSettingsControlBorderBrush"] =
                FindThemeBrush("ControlBorderBrush", "NormalBorderBrush");
        }

        private Style FindCompatibleStyle(string resourceKey, Type targetType)
        {
            var style = TryFindResource(resourceKey) as Style;
            if (style == null ||
                (style.TargetType != null && !style.TargetType.IsAssignableFrom(targetType)))
            {
                return null;
            }

            return style;
        }

        private Brush FindThemeBrush(string preferredResourceKey, string fallbackResourceKey)
        {
            return TryFindResource(preferredResourceKey) as Brush ??
                TryFindResource(fallbackResourceKey) as Brush ??
                Brushes.Transparent;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            (DataContext as GameRoutinesSettingsViewModel)?.RefreshLibraryGames();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                ApplySettingsFooterSpacing();
                InitializeTrackedGamesScrollViewer();
                UpdateTrackedGamesListMaximumHeight();
                UpdateRemoveSelectedPlacement();
            }));
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (trackedGamesScrollViewer != null)
            {
                trackedGamesScrollViewer.ScrollChanged -= TrackedGamesScrollViewer_ScrollChanged;
                trackedGamesScrollViewer = null;
            }

            RestoreRemoveSelectedInline();
        }

        private void UpdateTrackedGamesListMaximumHeight()
        {
            if (removeSelectedInlineReservedHeight <= 0 &&
                ReferenceEquals(RemoveSelectedButton.Parent, TrackedGamesStack))
            {
                removeSelectedInlineReservedHeight = GetRenderedHeight(RemoveSelectedButton);
            }

            var availableHeight = TrackedGamesPane.ActualHeight -
                GetRenderedHeight(TrackedGamesHeader) -
                removeSelectedInlineReservedHeight;
            TrackedGamesList.MaxHeight = Math.Max(0, availableHeight);
        }

        private static double GetRenderedHeight(FrameworkElement element)
        {
            return element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
        }

        private void ApplySettingsFooterSpacing()
        {
            var settingsWindow = Window.GetWindow(this);
            if (settingsWindow == null)
            {
                return;
            }

            CaptureApprovedRemoveSelectedButtonHeight();

            if (settingsWindow.FindName("ButtonOK") is Button saveButton)
            {
                saveButton.Margin = new Thickness(4, 4, 4, 16);
                ApplyRemoveSelectedVisualStyle(saveButton);
                MatchApprovedRemoveSelectedButtonHeight(saveButton);
            }

            if (settingsWindow.FindName("ButtonCancel") is Button cancelButton)
            {
                cancelButton.Margin = new Thickness(4, 4, 16, 16);
                ApplyRemoveSelectedVisualStyle(cancelButton);
                MatchApprovedRemoveSelectedButtonHeight(cancelButton);
                settingsFooterPanel = cancelButton.Parent as DockPanel;
            }
        }

        private void ApplyRemoveSelectedVisualStyle(Button button)
        {
            var normalButtonStyle = RemoveSelectedButton.Style ??
                RemoveSelectedButton.TryFindResource(typeof(Button)) as Style;
            if (normalButtonStyle != null)
            {
                button.Style = normalButtonStyle;
            }
        }

        private void CaptureApprovedRemoveSelectedButtonHeight()
        {
            if (approvedRemoveSelectedButtonHeight > 0)
            {
                return;
            }

            approvedRemoveSelectedButtonHeight = RemoveSelectedButton.ActualHeight;
            if (approvedRemoveSelectedButtonHeight <= 0)
            {
                RemoveSelectedButton.Measure(
                    new Size(double.PositiveInfinity, double.PositiveInfinity));
                approvedRemoveSelectedButtonHeight = RemoveSelectedButton.DesiredSize.Height;
            }

            if (approvedRemoveSelectedButtonHeight > 0)
            {
                RemoveSelectedButton.Height = approvedRemoveSelectedButtonHeight;
            }
        }

        private void MatchApprovedRemoveSelectedButtonHeight(Button button)
        {
            if (approvedRemoveSelectedButtonHeight <= 0)
            {
                return;
            }

            button.Height = approvedRemoveSelectedButtonHeight;
            button.Padding = RemoveSelectedButton.Padding;
            button.VerticalContentAlignment = VerticalAlignment.Center;
            button.ApplyTemplate();

            var template = button.Template;
            if (template != null &&
                template.FindName("Border", button) is FrameworkElement buttonChrome)
            {
                buttonChrome.Height = approvedRemoveSelectedButtonHeight;
            }
        }

        private void InitializeTrackedGamesScrollViewer()
        {
            if (trackedGamesScrollViewer != null)
            {
                return;
            }

            trackedGamesScrollViewer = FindVisualChild<ScrollViewer>(TrackedGamesList);
            if (trackedGamesScrollViewer != null)
            {
                trackedGamesScrollViewer.ScrollChanged += TrackedGamesScrollViewer_ScrollChanged;
            }
        }

        private void TrackedGamesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateRemoveSelectedPlacement();
        }

        private void QueueRemoveSelectedPlacementUpdate()
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(UpdateRemoveSelectedPlacement));
        }

        private void UpdateRemoveSelectedPlacement()
        {
            if (trackedGamesScrollViewer == null || settingsFooterPanel == null)
            {
                return;
            }

            var shouldUseFooter =
                trackedGamesScrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible;
            if (shouldUseFooter == removeSelectedInFooter)
            {
                return;
            }

            if (shouldUseFooter)
            {
                TrackedGamesStack.Children.Remove(RemoveSelectedButton);
                RemoveSelectedButton.DataContext = DataContext;
                RemoveSelectedButton.Margin = new Thickness(10, 4, 4, 16);
                RemoveSelectedButton.HorizontalAlignment = HorizontalAlignment.Left;
                RemoveSelectedButton.VerticalAlignment = VerticalAlignment.Bottom;
                DockPanel.SetDock(RemoveSelectedButton, Dock.Left);
                settingsFooterPanel.Children.Add(RemoveSelectedButton);
                removeSelectedInFooter = true;
            }
            else
            {
                RestoreRemoveSelectedInline();
            }
        }

        private void RestoreRemoveSelectedInline()
        {
            if (!removeSelectedInFooter)
            {
                return;
            }

            settingsFooterPanel?.Children.Remove(RemoveSelectedButton);
            RemoveSelectedButton.ClearValue(DataContextProperty);
            RemoveSelectedButton.Margin = new Thickness(0, 8, 0, 0);
            RemoveSelectedButton.HorizontalAlignment = HorizontalAlignment.Center;
            RemoveSelectedButton.VerticalAlignment = VerticalAlignment.Stretch;
            TrackedGamesStack.Children.Add(RemoveSelectedButton);
            removeSelectedInFooter = false;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                {
                    return match;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
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

        private void RoutinesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            QueueSelectedRoutineVisibilityUpdate();
        }

        private void RoutineMoveButton_Click(object sender, RoutedEventArgs e)
        {
            QueueSelectedRoutineVisibilityUpdate();
        }

        private void QueueSelectedRoutineVisibilityUpdate()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var selectedRoutine = GetViewModel()?.SelectedRoutine;
                if (selectedRoutine != null)
                {
                    RoutinesList.ScrollIntoView(selectedRoutine);
                }
            }), DispatcherPriority.Loaded);
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

        private void ChecklistItemCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox &&
                checkBox.DataContext is ChecklistItemSettings item)
            {
                GetViewModel()?.ChecklistItemChecked(item, checkBox.IsChecked == true);
            }
        }

        private void RoutineNameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                CommitRoutineNameTextBox(textBox);
                e.Handled = true;
            }
        }

        private void RoutineNameTextBox_PreviewLostKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                CommitRoutineNameTextBox(textBox);
            }
        }

        private void CommitRoutineNameTextBox(TextBox textBox)
        {
            if (!(textBox.DataContext is RoutineSettings routine))
            {
                return;
            }

            GetViewModel()?.CommitRoutineName(routine, textBox.Text);
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            textBox.CaretIndex = textBox.Text.Length;
        }

        private void RoutineStateToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton)
            {
                GetViewModel()?.SetSelectedRoutineState(toggleButton.IsChecked == true);
                toggleButton.GetBindingExpression(
                    System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateTarget();
            }
        }

        private void RoutineCountTowardOverallCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                GetViewModel()?.RoutineCountTowardOverallChanged(checkBox.IsChecked == true);
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

        private GameRoutinesSettingsViewModel GetViewModel()
        {
            return DataContext as GameRoutinesSettingsViewModel;
        }
    }
}
