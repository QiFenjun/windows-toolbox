using System.IO;
using System.Text.Json;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>只持久化按天聚合的字节数，不保存地址、URL、载荷或连接历史。</summary>
public sealed class TrafficHistoryStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public TrafficHistoryStore(string? filePath = null) =>
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsToolbox", "NetworkTraffic", "daily-traffic.json");

    public async Task<IReadOnlyDictionary<string, DailyTrafficRecord>> LoadTodayAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, DailyTrafficRecord>();
            List<DailyTrafficRecord>? records;
            await using (FileStream stream = File.OpenRead(_filePath))
                records = await JsonSerializer.DeserializeAsync<List<DailyTrafficRecord>>(stream, _options, cancellationToken);
            List<DailyTrafficRecord> allRecords = records ?? [];
            List<DailyTrafficRecord> validRecords = allRecords.Where(IsValidRecord).ToList();
            if (validRecords.Count != allRecords.Count)
                await WriteRecordsAsync(validRecords, cancellationToken);

            string today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
            return validRecords
                .Where(record => record.Date == today)
                .ToDictionary(record => record.ApplicationId, StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException) { return new Dictionary<string, DailyTrafficRecord>(); }
        catch (UnauthorizedAccessException) { return new Dictionary<string, DailyTrafficRecord>(); }
        catch (JsonException) { return new Dictionary<string, DailyTrafficRecord>(); }
    }

    public async Task SaveTodayAsync(IEnumerable<DailyTrafficRecord> records, CancellationToken cancellationToken)
    {
        try
        {
            await WriteRecordsAsync(records, cancellationToken);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>识别早期版本以“PID -1”“PID 0”等伪应用形式保存的无效归因记录。</summary>
    public static bool IsValidRecord(DailyTrafficRecord record)
    {
        if (record.DownloadBytes < 0 || record.UploadBytes < 0)
            return false;
        if (record.IdentityStatus == TrafficIdentityStatus.Invalid)
            return false;

        if (IsInvalidLegacyIdentity(record.ApplicationName)
            || IsInvalidLegacyIdentity(record.ApplicationId)
            || IsInvalidLegacyIdentity(record.ExecutablePathHash))
        {
            return false;
        }

        // “Unknown”可能是合法产品名；只有旧版三个身份字段都为占位值时才判定无效。
        return !(IsUnknownPlaceholder(record.ApplicationName)
            && IsUnknownPlaceholder(record.ApplicationId)
            && IsUnknownPlaceholder(record.ExecutablePathHash));
    }

    private async Task WriteRecordsAsync(IEnumerable<DailyTrafficRecord> records, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        string temporaryPath = _filePath + ".tmp";
        await using (FileStream stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, records, _options, cancellationToken);
        File.Move(temporaryPath, _filePath, overwrite: true);
    }

    private static bool IsInvalidLegacyIdentity(string value)
    {
        string normalized = value.Trim();
        if (string.Equals(normalized, "Unknown PID", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "未知进程", StringComparison.Ordinal))
        {
            return true;
        }

        if (!normalized.StartsWith("PID", StringComparison.OrdinalIgnoreCase))
            return false;

        string pidText = normalized[3..].Trim();
        return int.TryParse(pidText, out int processId) && processId <= 0;
    }

    private static bool IsUnknownPlaceholder(string value) =>
        string.Equals(value.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value.Trim(), "unknown", StringComparison.OrdinalIgnoreCase);
}

public sealed record DailyTrafficRecord(
    string Date,
    string ApplicationId,
    string ApplicationName,
    string ExecutablePathHash,
    long DownloadBytes,
    long UploadBytes,
    TrafficIdentityStatus IdentityStatus = TrafficIdentityStatus.Resolved);
