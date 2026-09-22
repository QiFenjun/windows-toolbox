using System.Windows.Controls;
using System.Windows.Input;
using WindowsToolbox.Modules.ClipboardPlus.Models;
using WindowsToolbox.Modules.ClipboardPlus.ViewModels;

namespace WindowsToolbox.Modules.ClipboardPlus.Views;

public partial class ClipboardPlusView : UserControl
{
    public ClipboardPlusView() => InitializeComponent();

    private void ClipboardPlusView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ClipboardPlusViewModel viewModel)
            return;
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && viewModel.SelectedItem is not null)
        {
            viewModel.CopyCommand.Execute(viewModel.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && viewModel.SelectedItem is not null)
        {
            viewModel.DeleteCommand.Execute(viewModel.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.SearchText = string.Empty;
            HistoryList.Focus();
            e.Handled = true;
        }
    }

    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ClipboardPlusViewModel viewModel && viewModel.SelectedItem is ClipboardHistoryItem item)
            viewModel.CopyCommand.Execute(item);
    }
}
