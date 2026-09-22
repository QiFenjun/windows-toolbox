using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using Microsoft.Win32;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WindowsToolbox.Modules.QuickLaunch.Models;
using WindowsToolbox.Modules.QuickLaunch.ViewModels;
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WindowsToolbox.Modules.QuickLaunch.Views;

public partial class QuickLaunchView : WpfUserControl
{
    private QuickLaunchViewModel? ViewModel => DataContext as QuickLaunchViewModel;

    public QuickLaunchView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is QuickLaunchViewModel old) old.FocusSearchRequested -= FocusSearchRequested;
            if (e.NewValue is QuickLaunchViewModel current) current.FocusSearchRequested += FocusSearchRequested;
        };
        Unloaded += (_, _) => { if (ViewModel is not null) ViewModel.FocusSearchRequested -= FocusSearchRequested; };
    }

    private void FocusSearchRequested(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => { SearchBox.Focus(); SearchBox.SelectAll(); });
    }

    private void AddApplication_Click(object sender, RoutedEventArgs e)
    {
        WpfOpenFileDialog dialog = new() { Filter = "应用程序 (*.exe)|*.exe", CheckFileExists = true, Multiselect = false, Title = "选择应用程序" };
        if (dialog.ShowDialog() == true) ViewModel?.BeginAdd(QuickLaunchItemType.Application, dialog.FileName);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        using Forms.FolderBrowserDialog dialog = new() { Description = "选择文件夹" };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) ViewModel?.BeginAdd(QuickLaunchItemType.Folder, dialog.SelectedPath);
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        WpfOpenFileDialog dialog = new() { Filter = "所有文件 (*.*)|*.*", CheckFileExists = true, Multiselect = false, Title = "选择文件" };
        if (dialog.ShowDialog() == true) ViewModel?.BeginAdd(QuickLaunchItemType.File, dialog.FileName);
    }

    private void AddUrl_Click(object sender, RoutedEventArgs e) => ViewModel?.BeginAdd(QuickLaunchItemType.Url);

    private void QuickLaunchView_DragOver(object sender, WpfDragEventArgs e) =>
        e.Effects = e.Data.GetDataPresent(WpfDataFormats.FileDrop, true) ? WpfDragDropEffects.Copy : WpfDragDropEffects.None;

    private void QuickLaunchView_Drop(object sender, WpfDragEventArgs e)
    {
        if (e.Data.GetDataPresent(WpfDataFormats.FileDrop, true) && e.Data.GetData(WpfDataFormats.FileDrop, true) is string[] paths)
            ViewModel?.AddDroppedPaths(paths);
        e.Handled = true;
    }

    private void QuickLaunchView_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
        else if (SearchBox.IsKeyboardFocusWithin && e.Key == Key.Down)
        {
            ViewModel?.MoveSelection(1);
            e.Handled = true;
        }
        else if (SearchBox.IsKeyboardFocusWithin && e.Key == Key.Up)
        {
            ViewModel?.MoveSelection(-1);
            e.Handled = true;
        }
        else if (SearchBox.IsKeyboardFocusWithin && e.Key == Key.Enter)
        {
            ViewModel?.LaunchSelected();
            e.Handled = true;
        }
    }

    private QuickLaunchItemViewModel? ContextItem(object sender) =>
        (sender as MenuItem)?.Parent is ContextMenu menu && menu.PlacementTarget is FrameworkElement target
            ? target.DataContext as QuickLaunchItemViewModel
            : null;

    private void ContextOpen_Click(object sender, RoutedEventArgs e) => ViewModel?.LaunchItem(ContextItem(sender));
    private void ContextEdit_Click(object sender, RoutedEventArgs e) => ViewModel?.BeginEdit(ContextItem(sender));
    private void ContextPin_Click(object sender, RoutedEventArgs e) => ViewModel?.TogglePinItem(ContextItem(sender));
    private void ContextLocation_Click(object sender, RoutedEventArgs e) => ViewModel?.OpenLocationItem(ContextItem(sender));
    private void ContextMoveUp_Click(object sender, RoutedEventArgs e) => ViewModel?.MoveUpCommand.Execute(ContextItem(sender));
    private void ContextMoveDown_Click(object sender, RoutedEventArgs e) => ViewModel?.MoveDownCommand.Execute(ContextItem(sender));
    private void ContextRemove_Click(object sender, RoutedEventArgs e) => ViewModel?.RemoveItem(ContextItem(sender));

    private void ContextCopy_Click(object sender, RoutedEventArgs e)
    {
        QuickLaunchItemViewModel? item = ContextItem(sender);
        if (item is not null) WpfClipboard.SetText(item.Target);
    }
}
