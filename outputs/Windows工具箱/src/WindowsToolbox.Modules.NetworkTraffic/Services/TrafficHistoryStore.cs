using System.IO;
using System.Text.Json;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>只持久化按天聚合的字节数，不保存地址、URL、载荷或连接历史。</summary>
public sealed class TrafficHistoryStore
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsToolbox", "NetworkTraffic", "daily-traffic.json");
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public async Task<IReadOnlyDictionary<string, DailyTrafficRecord>> LoadTodayAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_filePath))
                return new Dictionary<string, DailyTrafficRecord>();
            await using FileStream stream = File.OpenRead(_filePath);
            List<DailyTrafficRecord>? records = await JsonSerializer.DeserializeAsync<List<DailyTrafficRecord>>(stream, _options, cancellationToken);
            string today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
            return (records ?? [])
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
            string? directory = Path.GetDirectoryName(_filePath);
            if (directory is not null)
                Directory.CreateDirectory(directory);
            await using FileStream stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, records, _options, cancellationToken);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public sealed record DailyTrafficRecord(
    string Date,
    string ApplicationId,
    string ApplicationName,
    string ExecutablePathHash,
    long DownloadBytes,
    long UploadBytes);
