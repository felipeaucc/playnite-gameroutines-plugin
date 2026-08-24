using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace GameRoutines
{
    public partial class BlockedManualStateWarningView : UserControl
    {
        public bool DontShowAgain => DontShowAgainCheckBox.IsChecked == true;

        internal BlockedManualStateWarningView(IEnumerable<BlockedManualStateWarningEntry> entries)
        {
            InitializeComponent();
            var entryList = (entries ?? Enumerable.Empty<BlockedManualStateWarningEntry>()).ToList();
            if (entryList.Count > 1)
            {
                var labels = entryList.Select(a => a.RoutineLabel).ToList();
                var joinedLabels = labels.Count == 2
                    ? $"{labels[0]} and {labels[1]}"
                    : $"{string.Join(", ", labels.Take(labels.Count - 1))}, and {labels.Last()}";
                var representative = entryList[0];
                var resolution = string.Equals(representative.RequestedState, "COMPLETE", System.StringComparison.Ordinal)
                    ? "Complete their checklists"
                    : "Uncheck an item in each routine";
                MessageTextBlock.Inlines.Add(new Run(
                    $"{joinedLabels} routines are being controlled by their checklists and are currently "));
                MessageTextBlock.Inlines.Add(new Run(representative.CurrentState) { FontWeight = FontWeights.Bold });
                MessageTextBlock.Inlines.Add(new Run(
                    $".{System.Environment.NewLine}{resolution} or disable automatic completion before " +
                    $"marking {representative.ActionLabel} as {representative.RequestedState}."));
                return;
            }

            for (var index = 0; index < entryList.Count; index++)
            {
                var entry = entryList[index];
                MessageTextBlock.Inlines.Add(new Run(
                    $"{entry.RoutineLabel} routine is being controlled by its checklist and is currently "));
                MessageTextBlock.Inlines.Add(new Run(entry.CurrentState) { FontWeight = FontWeights.Bold });
                MessageTextBlock.Inlines.Add(new Run(
                    $".{System.Environment.NewLine}{entry.Resolution} or disable automatic completion before " +
                    $"marking {entry.ActionLabel} as {entry.RequestedState}."));
                if (index < entryList.Count - 1)
                {
                    MessageTextBlock.Inlines.Add(new LineBreak());
                    MessageTextBlock.Inlines.Add(new LineBreak());
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            Window.GetWindow(this)?.Close();
        }
    }

    internal sealed class BlockedManualStateWarningEntry
    {
        public string RoutineLabel { get; set; }
        public string CurrentState { get; set; }
        public string Resolution { get; set; }
        public string ActionLabel { get; set; }
        public string RequestedState { get; set; }
    }
}
