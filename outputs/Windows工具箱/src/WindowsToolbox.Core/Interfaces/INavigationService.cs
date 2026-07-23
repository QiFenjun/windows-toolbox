namespace WindowsToolbox.Core.Interfaces;

public interface INavigationService
{
    string CurrentPageId { get; }
    object? CurrentViewModel { get; }
    event EventHandler<NavigationChangedEventArgs>? Navigated;
    void Register(string pageId, Func<object> viewModelFactory);
    bool Navigate(string pageId);
}

public sealed class NavigationChangedEventArgs(string pageId, object viewModel) : EventArgs
{
    public string PageId { get; } = pageId;
    public object ViewModel { get; } = viewModel;
}
