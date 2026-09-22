using System.Windows.Controls;
using System.Windows.Input;
using WindowsToolbox.Modules.TextTools.ViewModels;

namespace WindowsToolbox.Modules.TextTools.Views;

public partial class TextToolsView : UserControl
{
    public TextToolsView() => InitializeComponent();

    private void TextToolsView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not TextToolsViewModel viewModel) return;
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OperationSearchBox.Focus(); e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == (ModifierKeys.Control))
        {
            if (viewModel.ExecuteCommand.CanExecute(null)) viewModel.ExecuteCommand.Execute(null); e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            viewModel.CopyOutputCommand.Execute(null); e.Handled = true;
        }
        else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            viewModel.ClearCommand.Execute(null); e.Handled = true;
        }
    }
}
