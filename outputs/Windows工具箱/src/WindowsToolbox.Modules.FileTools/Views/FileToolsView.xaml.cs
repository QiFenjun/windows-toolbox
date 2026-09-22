using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfButton = System.Windows.Controls.Button;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WindowsToolbox.Modules.FileTools.Models;
using WindowsToolbox.Modules.FileTools.Services;
using WindowsToolbox.Modules.FileTools.ViewModels;

namespace WindowsToolbox.Modules.FileTools.Views;

public partial class FileToolsView : WpfUserControl
{
    public FileToolsView() => InitializeComponent();

    private FileToolsViewModel? ViewModel => DataContext as FileToolsViewModel;

    private void ChooseFiles_Click(object sender, RoutedEventArgs e)
    {
        WpfOpenFileDialog dialog = new() { Multiselect = true, CheckFileExists = true, Title = "选择文件" };
        if (dialog.ShowDialog() == true) ViewModel?.AddPaths(dialog.FileNames);
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        using Forms.FolderBrowserDialog dialog = new() { Description = "选择文件夹" };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) ViewModel?.AddPaths([dialog.SelectedPath]);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ViewModel?.ClearPaths();
    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        foreach (string path in SelectionList.SelectedItems.Cast<string>().ToArray())
            ViewModel.RemovePath(path);
    }
    private void RefreshPreview_Click(object sender, RoutedEventArgs e) => ViewModel?.RefreshRenamePreview();
    private void ApplyRename_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || !ViewModel.CanApplyRename) return;
        if (WpfMessageBox.Show($"即将重命名 {ViewModel.RenamePreview.Count(item => item.Status == RenameItemStatus.Ready)} 个文件。", "确认重命名", WpfMessageBoxButton.OKCancel, WpfMessageBoxImage.Warning) == WpfMessageBoxResult.OK)
            ViewModel.ApplyRenameCommand.Execute(null);
    }
    private void UndoRename_Click(object sender, RoutedEventArgs e) => ViewModel?.UndoRenameCommand.Execute(null);
    private void ComputeHash_Click(object sender, RoutedEventArgs e) => ViewModel?.ComputeHashCommand.Execute(null);
    private void VerifyHash_Click(object sender, RoutedEventArgs e) => ViewModel?.VerifyExpectedHash();
    private async void GenerateSums_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || ViewModel.HashResults.All(item => item.Status != HashItemStatus.Completed)) return;
        WpfSaveFileDialog dialog = new() { FileName = "SHA256SUMS.txt", Filter = "文本文件|*.txt|所有文件|*.*", Title = "生成 SHA256SUMS.txt" };
        if (dialog.ShowDialog() != true) return;
        bool overwrite = !File.Exists(dialog.FileName) ||
            WpfMessageBox.Show("目标文件已存在，是否覆盖？", "确认覆盖", WpfMessageBoxButton.OKCancel, WpfMessageBoxImage.Warning) == WpfMessageBoxResult.OK;
        if (!overwrite && File.Exists(dialog.FileName)) return;
        await ViewModel.GenerateSha256SumsAsync(dialog.FileName, overwrite);
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel?.CancelCommand.Execute(null);
    private void CalculateFolderSize_Click(object sender, RoutedEventArgs e) => ViewModel?.CalculateFolderSizeCommand.Execute(null);
    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton { Tag: string value } && !string.IsNullOrEmpty(value))
            WpfClipboard.SetText(value);
    }

    private void RenameTab_Click(object sender, RoutedEventArgs e) => Select(FileToolTab.Rename);
    private void HashTab_Click(object sender, RoutedEventArgs e) => Select(FileToolTab.Hash);
    private void PathTab_Click(object sender, RoutedEventArgs e) => Select(FileToolTab.Path);
    private void InfoTab_Click(object sender, RoutedEventArgs e) => Select(FileToolTab.Info);

    private void Select(FileToolTab tab)
    {
        ViewModel?.SelectTool(tab);
        RenamePanel.Visibility = tab == FileToolTab.Rename ? Visibility.Visible : Visibility.Collapsed;
        HashPanel.Visibility = tab == FileToolTab.Hash ? Visibility.Visible : Visibility.Collapsed;
        PathPanel.Visibility = tab == FileToolTab.Path ? Visibility.Visible : Visibility.Collapsed;
        InfoPanel.Visibility = tab == FileToolTab.Info ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FileToolsView_DragOver(object sender, WpfDragEventArgs e) =>
        e.Effects = e.Data.GetDataPresent(WpfDataFormats.FileDrop, true) ? WpfDragDropEffects.Copy : WpfDragDropEffects.None;

    private void FileToolsView_Drop(object sender, WpfDragEventArgs e)
    {
        IReadOnlyList<string> paths = FileDropParser.Parse(e.Data);
        if (paths.Count > 0) ViewModel?.AddPaths(paths);
        e.Handled = true;
    }
}
