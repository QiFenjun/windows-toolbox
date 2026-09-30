using System.Windows;
using System.Windows.Controls;
using WindowsToolbox.Modules.Utilities.Regex.ViewModels;

namespace WindowsToolbox.Modules.Utilities.Regex.Views;

public partial class RegexToolsView : UserControl
{
    public RegexToolsView() => InitializeComponent();
    private void View_Loaded(object sender, RoutedEventArgs e) { if (DataContext is RegexToolsViewModel vm) vm.Activate(); }
    private void View_Unloaded(object sender, RoutedEventArgs e) { if (DataContext is RegexToolsViewModel vm) vm.Deactivate(); }
    private void Option_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: string index, IsChecked: bool value } && DataContext is RegexToolsViewModel vm && int.TryParse(index, out int option))
            vm.SetOption(option, value);
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) { if (DataContext is RegexToolsViewModel vm) vm.CancelCommand.Execute(null); }
}
