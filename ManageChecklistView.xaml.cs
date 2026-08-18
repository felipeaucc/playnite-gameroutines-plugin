using System.Windows.Controls;
using System.Windows.Input;

namespace WeeklyManager
{
    public partial class ManageChecklistView : UserControl
    {
        public ManageChecklistView()
        {
            InitializeComponent();
        }

        private void NewItemTextBox_PreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Enter || !(DataContext is ManageChecklistViewModel viewModel))
            {
                return;
            }

            if (viewModel.AddItemCommand.CanExecute(null))
            {
                viewModel.AddItemCommand.Execute(null);
            }

            args.Handled = true;
        }

        private void ItemTextBox_PreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Enter && sender is TextBox textBox)
            {
                CommitItemText(textBox);
                args.Handled = true;
            }
        }

        private void ItemTextBox_PreviewLostKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs args)
        {
            if (sender is TextBox textBox)
            {
                CommitItemText(textBox);
            }
        }

        private void CommitItemText(TextBox textBox)
        {
            if (!(DataContext is ManageChecklistViewModel viewModel) ||
                !(textBox.DataContext is ChecklistItemSettings item))
            {
                return;
            }

            viewModel.CommitItemText(item, textBox.Text);
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            textBox.CaretIndex = textBox.Text.Length;
        }
    }
}
