using System.IO;
using WindowsToolbox.Modules.LockInspector.Interop;
using WindowsToolbox.Modules.LockInspector.Models;

namespace WindowsToolbox.Modules.LockInspector.Services;

public sealed class LockScanService(IRestartManagerClient client)
{
    public const int BatchSize = 256;
    public const int QuickScanLimit = 5000;
    public const int FullScanLimit = 20000;

    public Task<LockScanResult> ScanAsync(LockScanRequest request, CancellationToken cancellationToken = default,
        IProgress<LockScanProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        LockScanRequest snapshot = request with { Targets = request.Targets.ToArray() };
        return Task.Run(() => Scan(snapshot, cancellationToken, progress));
    }

    private LockScanResult Scan(LockScanRequest request, CancellationToken token, IProgress<LockScanProgress>? progress)
    {
        DateTimeOffset started = DateTimeOffset.Now;
        Dictionary<string, LockingProcessInfo> blockers = new(StringComparer.OrdinalIgnoreCase);
        List<int> errors = [];
        int enumerated = 0, registered = 0, skipped = 0, batches = 0;
        uint rebootReason = 0;
        bool cancelled = false, limited = false;
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> batch = new(BatchSize);
        ScanFileEnumerator enumeration = new();
        try
        {
            if (!Enum.IsDefined(request.ScanType)) throw new ArgumentException("Invalid scan type.");
            IEnumerable<string> targets = request.ScanType == LockScanType.Files ? request.Targets :
                request.Targets.Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(root => enumeration.Enumerate(root, request.Recursive, token));
            int limit = request.ScanType == LockScanType.Files || request.Recursive ? FullScanLimit : QuickScanLimit;
            foreach (string target in targets)
            {
                token.ThrowIfCancellationRequested();
                string path;
                try
                {
                    if (string.IsNullOrWhiteSpace(target) || !Path.IsPathFullyQualified(target))
                    { skipped++; continue; }
                    path = Path.GetFullPath(target);
                    if (!seen.Add(path)) continue;
                    if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    { skipped++; continue; }
                }
                catch (Exception ex) when (ScanFileEnumerator.IsFileError(ex))
                { skipped++; continue; }
                enumerated++;
                // Isolate long paths so a native rejection cannot discard ordinary files in the batch.
                if (path.Length >= 260 && batch.Count > 0) QueryBatch();
                batch.Add(path);
                if (batch.Count == BatchSize || path.Length >= 260) QueryBatch();
                if (enumerated >= limit) { limited = true; break; }
            }
            if (batch.Count > 0) QueryBatch();
        }
        catch (OperationCanceledException) { cancelled = true; }
        skipped += enumeration.Skipped;
        limited |= enumeration.WasLimited;
        progress?.Report(new(enumerated, registered, skipped, batches, blockers.Count));
        return new()
        {
            Target = request, StartedAt = started, CompletedAt = DateTimeOffset.Now,
            FilesEnumerated = enumerated, FilesRegistered = registered, FilesSkipped = skipped,
            Batches = batches, WasLimited = limited, WasCancelled = cancelled, Errors = errors,
            RebootReason = rebootReason, Blockers = blockers.Values.ToArray()
        };

        void QueryBatch()
        {
            token.ThrowIfCancellationRequested();
            // Recheck immediately before registration: targets may disappear while enumerating.
            string[] files = batch.Where(File.Exists).ToArray();
            skipped += batch.Count - files.Length;
            batch.Clear();
            if (files.Length == 0) return;
            int error = client.StartSession(out uint session);
            if (error != 0) { errors.Add(error); skipped += files.Length; return; }
            try
            {
                token.ThrowIfCancellationRequested();
                error = client.RegisterResources(session, files);
                if (error != 0) { errors.Add(error); skipped += files.Length; return; }
                registered += files.Length;
                batches++;
                RmProcessInfo[]? buffer = null;
                for (int attempt = 0; attempt <= 3; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    uint count = (uint)(buffer?.Length ?? 0);
                    error = client.GetList(session, out uint needed, ref count, buffer, out uint reasons);
                    rebootReason |= reasons;
                    if (error == 0)
                    {
                        if (count > (buffer?.Length ?? 0)) { errors.Add(234); break; }
                        foreach (RmProcessInfo process in (buffer ?? []).Take((int)count))
                        {
                            LockingProcessInfo info = process.ToModel();
                            blockers.TryAdd(info.Identity, info);
                        }
                        break;
                    }
                    if (error != 234 || attempt == 3 || needed == 0 || needed > 16384)
                    { errors.Add(error); break; }
                    buffer = new RmProcessInfo[needed];
                }
            }
            finally
            {
                int endError = client.EndSession(session);
                if (endError != 0) errors.Add(endError);
                progress?.Report(new(enumerated, registered, skipped + enumeration.Skipped, batches, blockers.Count));
            }
        }
    }
}
