using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WindowsToolbox.Modules.LockInspector.Models;
using WindowsToolbox.Modules.LockInspector.Services;
using WindowsToolbox.Modules.LockInspector.ViewModels;

namespace WindowsToolbox.Modules.LockInspector.Views;

public partial class LockInspectorView : UserControl
{
    private LockInspectorViewModel? Model => DataContext as LockInspectorViewModel;
    public LockInspectorView() => InitializeComponent();
    private async void ChooseFiles(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Multiselect = true, CheckFileExists = true };
        if (Model is { CanSelect: true } vm && dialog.ShowDialog() == true) await vm.ScanAsync(new(dialog.FileNames));
    }
    private async void ChooseFolder(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new();
        if (Model is { CanSelect: true } vm && dialog.ShowDialog() == true) await vm.ScanAsync(new([dialog.FolderName], LockScanType.Folder, vm.Recursive));
    }
    private async void ChooseDrive(object sender, RoutedEventArgs e)
    {
        if (Model is { CanSelect: true, SelectedDrive: not null } vm) await vm.ScanAsync(new([vm.SelectedDrive.Root], LockScanType.Drive, vm.Recursive));
    }
    private async void Rescan(object sender, RoutedEventArgs e) { if (Model is { CanRescan: true } vm) await vm.RescanAsync(); }
    private void Cancel(object sender, RoutedEventArgs e) => Model?.Cancel();
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = Model?.CanSelect == true && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
    }
    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (Model is not { CanSelect: true } vm || e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
        bool folders = paths.All(Directory.Exists);
        if (!folders && paths.Any(Directory.Exists)) { vm.ShowActionError(); return; }
        LockScanType type = !folders ? LockScanType.Files : paths.Length == 1 && Path.GetPathRoot(paths[0]) == paths[0] ? LockScanType.Drive : LockScanType.Folder;
        await vm.ScanAsync(new(paths, type, vm.Recursive));
    }
    private async void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedIcon.Source = null;
        LockingProcessInfo? selected = Model?.Selected;
        var image = await Task.Run(() => ProcessIcon.Read(selected?.ExecutablePath));
        if (ReferenceEquals(selected, Model?.Selected)) SelectedIcon.Source = image;
    }
    private void Copy(object sender, RoutedEventArgs e) => Action(() => { if (Model?.Selected is { } item) Clipboard.SetText(item.InfoText); });
    private void OpenLocation(object sender, RoutedEventArgs e) => Action(() => { if (Model?.Selected is { } item && !ProcessDetailsService.OpenLocation(item)) Model.ShowActionError(); });
    private void TaskManager(object sender, RoutedEventArgs e) => Action(ProcessDetailsService.OpenTaskManager);
    private void Action(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException or IOException or InvalidOperationException or ArgumentException) { Model?.ShowActionError(); }
    }
}
