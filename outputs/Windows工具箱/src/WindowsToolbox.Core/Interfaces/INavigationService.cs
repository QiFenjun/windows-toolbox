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

public enum NavigationDiagnosticStage
{
    Requested,
    FactoryStarted,
    ViewModelResolved,
    Completed,
    Stale,
    Failed
}

public sealed class NavigationDiagnosticEventArgs(
    long requestId,
    string sourcePageId,
    string targetPageId,
    NavigationDiagnosticStage stage,
    TimeSpan elapsed,
    bool cacheHit,
    string? exceptionType = null) : EventArgs
{
    public long RequestId { get; } = requestId;
    public string SourcePageId { get; } = sourcePageId;
    public string TargetPageId { get; } = targetPageId;
    public NavigationDiagnosticStage Stage { get; } = stage;
    public TimeSpan Elapsed { get; } = elapsed;
    public bool CacheHit { get; } = cacheHit;
    public string? ExceptionType { get; } = exceptionType;
}
