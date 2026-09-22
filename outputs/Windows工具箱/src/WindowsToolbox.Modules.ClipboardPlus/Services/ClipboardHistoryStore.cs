using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.IO;
using WindowsToolbox.Modules.ClipboardPlus.Models;

namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public sealed class ClipboardHistoryStore : IClipboardHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _directory;
    public string FilePath { get; }

    public ClipboardHistoryStore(string? localAppData = null)
    {
        string root = localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _directory = Path.Combine(root, "WindowsToolbox", "ClipboardPlus");
        FilePath = Path.Combine(_directory, "history.dat");
    }

    public async Task<IReadOnlyList<ClipboardHistoryItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
            return [];

        try
        {
            byte[] protectedBytes = await File.ReadAllBytesAsync(FilePath, cancellationToken).ConfigureAwait(false);
            byte[] plainBytes = Unprotect(protectedBytes);
            return JsonSerializer.Deserialize<List<ClipboardHistoryItem>>(plainBytes, JsonOptions) ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or CryptographicException or InvalidDataException)
        {
            string corruptPath = $"{FilePath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            try { File.Move(FilePath, corruptPath, true); } catch { }
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyCollection<ClipboardHistoryItem> items, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(items, JsonOptions);
        byte[] encrypted = Protect(json);
        string temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, FilePath, true);
    }

    private static byte[] Protect(byte[] data) => CryptProtect(data);
    private static byte[] Unprotect(byte[] data) => CryptUnprotect(data);

    private static byte[] CryptProtect(byte[] data)
    {
        DATA_BLOB input = new(data);
        if (!CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out DATA_BLOB output))
            throw new CryptographicException(Marshal.GetLastWin32Error());
        try { return output.ToArray(); } finally { LocalFree(output.pbData); }
    }

    private static byte[] CryptUnprotect(byte[] data)
    {
        DATA_BLOB input = new(data);
        if (!CryptUnprotectData(ref input, IntPtr.Zero, null, IntPtr.Zero, IntPtr.Zero, 0, out DATA_BLOB output))
            throw new CryptographicException(Marshal.GetLastWin32Error());
        try { return output.ToArray(); } finally { LocalFree(output.pbData); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
        public DATA_BLOB(byte[] bytes)
        {
            cbData = bytes.Length;
            pbData = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, pbData, bytes.Length);
        }
        public readonly byte[] ToArray()
        {
            byte[] bytes = new byte[cbData];
            Marshal.Copy(pbData, bytes, 0, cbData);
            return bytes;
        }
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptProtectData(ref DATA_BLOB dataIn, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DATA_BLOB dataOut);
    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB dataIn, IntPtr description, byte[]? entropy, IntPtr reserved, IntPtr prompt, int flags, out DATA_BLOB dataOut);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
