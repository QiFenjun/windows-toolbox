using WindowsToolbox.Core.Interfaces;

namespace WindowsToolbox.Core.Services;

/// <summary>缓存页面 ViewModel，避免导航时重复创建页面状态。</summary>
public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<string, Func<object>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string CurrentPageId { get; private set; } = string.Empty;
    public object? CurrentViewModel { get; private set; }
    public event EventHandler<NavigationChangedEventArgs>? Navigated;

    public void Register(string pageId, Func<object> viewModelFactory)
    {
        if (string.IsNullOrWhiteSpace(pageId))
            throw new ArgumentException("页面 ID 不能为空。", nameof(pageId));

        _factories[pageId] = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
    }

    public bool Navigate(string pageId)
    {
        if (!_factories.TryGetValue(pageId, out Func<object>? factory))
            return false;

        if (!_cache.TryGetValue(pageId, out object? viewModel))
        {
            viewModel = factory();
            _cache[pageId] = viewModel;
        }

        CurrentPageId = pageId;
        CurrentViewModel = viewModel;
        Navigated?.Invoke(this, new NavigationChangedEventArgs(pageId, viewModel));
        return true;
    }
}
