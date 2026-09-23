using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WindowsToolbox.Modules.Utilities.QR.ViewModels;

namespace WindowsToolbox.Modules.Utilities.QR.Views;

public partial class QrToolsView : UserControl
{
    public QrToolsView() => InitializeComponent();

    private void SavePng_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not QrToolsViewModel viewModel)
            return;
        SaveFileDialog dialog = new()
        {
            Title = "保存二维码 PNG",
            Filter = "PNG 图像 (*.png)|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = "QRCode.png"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;
        try
        {
            using FileStream output = new(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None);
            viewModel.SavePng(output);
        }
        catch
        {
            MessageBox.Show(Window.GetWindow(this), "保存 PNG 失败。", "二维码工具", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not QrToolsViewModel viewModel)
            return;
        OpenFileDialog dialog = new()
        {
            Title = "选择二维码图片",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|PNG 图像 (*.png)|*.png|JPEG 图像 (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP 图像 (*.bmp)|*.bmp",
            Multiselect = false,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            _ = viewModel.DecodeFileAsync(dialog.FileName);
    }

    private void ImageDrop_DragOver(object sender, DragEventArgs e) =>
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void ImageDrop_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not QrToolsViewModel viewModel || !e.Data.GetDataPresent(DataFormats.FileDrop))
            return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            _ = viewModel.DecodeDroppedFilesAsync(paths);
    }
}
