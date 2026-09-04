using System.Collections.Concurrent;
using System.Windows.Media;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

/// <summary>异步、限流且按文件版本缓存的应用图标服务。</summary>
public sealed class ApplicationIconService : IApplicationIconService
{
    private readonly IApplicationIconExtractor _extractor;
    private readonly ConcurrentDictionary<string, Lazy<Task<ImageSource?>>> _memoryCache = new();
    private readonly SemaphoreSlim _extractionGate;

    public ApplicationIconService(int maxConcurrentExtractions = 4)
        : this(new ShellApplicationIconExtractor(), maxConcurrentExtractions)
    {
    }

    public ApplicationIconService(IApplicationIconExtractor extractor, int maxConcurrentExtractions = 4)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _extractionGate = new SemaphoreSlim(Math.Clamp(maxConcurrentExtractions, 1, 8));
    }

    public async Task<ImageSource?> GetIconAsync(
        InstalledApplication application,
        int desiredSize,
        CancellationToken cancellationToken) =>
        (await GetIconResultAsync(application, desiredSize, cancellationToken)).Icon;

    public async Task<ApplicationIconResult> GetIconResultAsync(
        InstalledApplication application,
        int desiredSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        int size = Math.Clamp(desiredSize, 16, 256);
        IReadOnlyList<ApplicationIconCandidate> candidates;
        try
        {
            candidates = await Task.Run(
                () => ApplicationIconCandidateResolver.Resolve(application),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return ApplicationIconResult.Fallback; }

        foreach (ApplicationIconCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string cacheKey = ApplicationIconCacheKey.Create(candidate.Path, candidate.IconIndex, size);
            Lazy<Task<ImageSource?>> lazy = _memoryCache.GetOrAdd(
                cacheKey,
                _ => new Lazy<Task<ImageSource?>>(
                    () => ExtractAsync(candidate, size),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                ImageSource? icon = await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (icon is not null)
                    return new ApplicationIconResult(icon, candidate.Source, candidate.Path);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // 单个资源损坏、权限拒绝或无图标时继续尝试下一来源。
            }
        }

        return ApplicationIconResult.Fallback;
    }

    private async Task<ImageSource?> ExtractAsync(ApplicationIconCandidate candidate, int desiredSize)
    {
        await _extractionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => _extractor.Extract(
                candidate.Path,
                candidate.IconIndex,
                desiredSize)).ConfigureAwait(false);
        }
        finally
        {
            _extractionGate.Release();
        }
    }
}
