using System.Windows.Controls;
using WindowsToolbox.Modules.Utilities.Time.ViewModels;

namespace WindowsToolbox.Modules.Utilities.Time.Views;

public partial class TimeToolsView : UserControl
{
    public TimeToolsView() => InitializeComponent();

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        IsVisibleChanged += OnIsVisibleChanged;
        if (DataContext is TimeToolsViewModel viewModel)
        {
            if (IsVisible) viewModel.Activate();
            else viewModel.Deactivate();
        }
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        IsVisibleChanged -= OnIsVisibleChanged;
        if (DataContext is TimeToolsViewModel viewModel)
            viewModel.Deactivate();
    }

    private void OnIsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is not TimeToolsViewModel viewModel) return;
        if (e.NewValue is true) viewModel.Activate();
        else viewModel.Deactivate();
    }
}
