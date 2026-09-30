using WindowsToolbox.Core.Interfaces;
using System.Diagnostics;

namespace WindowsToolbox.Core.Services;

/// <summary>缓存页面 ViewModel，避免导航时重复创建页面状态；View Loaded/Unloaded 不会销毁应用级缓存实例。</summary>
public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<string, Func<object>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _cache = new(StringComparer.OrdinalIgnoreCase);
    private long _requestGeneration;

    public string CurrentPageId { get; private set; } = string.Empty;
    public object? CurrentViewModel { get; private set; }
    public event EventHandler<NavigationChangedEventArgs>? Navigated;
    public event EventHandler<NavigationDiagnosticEventArgs>? Diagnostic;

    public void Register(string pageId, Func<object> viewModelFactory)
    {
        if (string.IsNullOrWhiteSpace(pageId))
            throw new ArgumentException("页面 ID 不能为空。", nameof(pageId));

        _factories[pageId] = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
    }

    public bool Navigate(string pageId)
    {
        long requestId = ++_requestGeneration;
        string sourcePageId = CurrentPageId;
        Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.Requested, TimeSpan.Zero, cacheHit: false);
        if (!_factories.TryGetValue(pageId, out Func<object>? factory))
        {
            Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.Failed, TimeSpan.Zero, cacheHit: false, "UnknownPage");
            return false;
        }

        bool cacheHit = _cache.TryGetValue(pageId, out object? viewModel);
        if (!cacheHit)
        {
            Stopwatch elapsed = Stopwatch.StartNew();
            Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.FactoryStarted, TimeSpan.Zero, cacheHit: false);
            try { viewModel = factory() ?? throw new InvalidOperationException("页面工厂返回空 ViewModel。"); }
            catch (Exception exception)
            {
                Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.Failed, elapsed.Elapsed, cacheHit: false, exception.GetType().Name);
                throw;
            }
            _cache[pageId] = viewModel;
            Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.ViewModelResolved, elapsed.Elapsed, cacheHit: false);
            if (requestId != _requestGeneration)
            {
                Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.Stale, elapsed.Elapsed, cacheHit: false);
                return false;
            }
        }
        else
            Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.ViewModelResolved, TimeSpan.Zero, cacheHit: true);

        if (requestId != _requestGeneration)
        {
            Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.Stale, TimeSpan.Zero, cacheHit);
            return false;
        }

        object activeViewModel = viewModel ?? throw new InvalidOperationException("页面缓存包含空 ViewModel。");
        CurrentPageId = pageId;
        CurrentViewModel = activeViewModel;
        try { Navigated?.Invoke(this, new NavigationChangedEventArgs(pageId, activeViewModel)); }
        catch (Exception exception)
        {
            Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.Failed, TimeSpan.Zero, cacheHit, exception.GetType().Name);
            throw;
        }

        if (requestId != _requestGeneration || !string.Equals(CurrentPageId, pageId, StringComparison.OrdinalIgnoreCase))
        {
            Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.Stale, TimeSpan.Zero, cacheHit);
            return false;
        }

        Report(requestId, sourcePageId, pageId, NavigationDiagnosticStage.Completed, TimeSpan.Zero, cacheHit);
        return true;
    }

    private void Report(long requestId, string sourcePageId, string targetPageId, NavigationDiagnosticStage stage,
        TimeSpan elapsed, bool cacheHit, string? exceptionType = null) =>
        Diagnostic?.Invoke(this, new NavigationDiagnosticEventArgs(
            requestId, sourcePageId, targetPageId, stage, elapsed, cacheHit, exceptionType));
}
