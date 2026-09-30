using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WindowsToolbox.Modules.Utilities.Image.ViewModels;

namespace WindowsToolbox.Modules.Utilities.Image.Views;

public partial class ImageToolsView : UserControl
{
    public ImageToolsView() => InitializeComponent();
    private void ChooseFiles_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Title = "选择图片", Filter = "PNG / JPEG / BMP|*.png;*.jpg;*.jpeg;*.bmp", Multiselect = true, CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true && DataContext is ImageToolsViewModel vm) vm.AddFiles(dialog.FileNames);
    }
    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new() { Title = "选择输出目录" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true && DataContext is ImageToolsViewModel vm) vm.OutputDirectory = dialog.FolderName;
    }
    private void Files_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DataContext is ImageToolsViewModel { IsBusy: false } && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private void Files_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is ImageToolsViewModel vm && e.Data.GetData(DataFormats.FileDrop) is string[] paths) vm.AddFiles(paths);
        e.Handled = true;
    }
}
