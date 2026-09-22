using System.Windows;
using System.Windows.Controls;
using WindowsToolbox.Modules.WindowTools.ViewModels;

namespace WindowsToolbox.Modules.WindowTools.Views;

public partial class WindowToolsView : UserControl
{
    public WindowToolsView() => InitializeComponent();

    private void CopyInfo_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WindowToolsViewModel viewModel && !string.IsNullOrWhiteSpace(viewModel.SelectedInfoText))
            Clipboard.SetText(viewModel.SelectedInfoText);
    }
}
