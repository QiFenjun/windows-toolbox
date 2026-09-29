using System.Security.Cryptography;
using System.Text;

namespace WindowsToolbox.Modules.Utilities.Developer.Services;

/// <summary>
/// Local UTF-8 text hashing (MD5 / SHA-1 / SHA-256 / SHA-512).
/// Pure and stateless: inputs and results are never written to disk or logs, and nothing
/// is uploaded. File hashing belongs to File Tools; HMAC/online lookup are out of scope.
/// </summary>
public static class TextHashService
{
    /// <summary>Above this size a hint is shown; hashing still works.</summary>
    public const int SoftLimitBytes = 1024 * 1024;

    /// <summary>Hard limit so a huge paste cannot stall the UI.</summary>
    public const int MaxInputBytes = 10 * 1024 * 1024;

    public const string DefaultAlgorithm = "SHA-256";

    public static readonly IReadOnlyList<string> Algorithms = ["MD5", "SHA-1", "SHA-256", "SHA-512"];

    public static bool IsCompatibilityOnly(string algorithm) =>
        algorithm is "MD5" or "SHA-1";

    public static string AlgorithmNote(string algorithm) => algorithm switch
    {
        "MD5" => "MD5：仅兼容性，不用于安全验证",
        "SHA-1" => "SHA-1：仅兼容性，不建议用于安全用途",
        "SHA-256" => "SHA-256：推荐",
        "SHA-512" => "SHA-512：推荐",
        _ => string.Empty
    };

    /// <summary>Computes the uppercase hex digest of the UTF-8 bytes of <paramref name="text"/>.</summary>
    public static bool TryCompute(string? text, string algorithm, out string hash, out string? error)
    {
        hash = string.Empty;
        error = null;

        byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        if (bytes.Length > MaxInputBytes)
        {
            error = "文本过大（超过 10 MiB），文件 Hash 请使用文件工具 / File Tools。";
            return false;
        }

        byte[] digest = algorithm switch
        {
            "MD5" => MD5.HashData(bytes),
            "SHA-1" => SHA1.HashData(bytes),
            "SHA-256" => SHA256.HashData(bytes),
            "SHA-512" => SHA512.HashData(bytes),
            _ => []
        };

        if (digest.Length == 0)
        {
            error = "不支持的哈希算法。";
            return false;
        }

        hash = Convert.ToHexString(digest);
        return true;
    }

    public static bool IsSoftLimitExceeded(string? text) =>
        Encoding.UTF8.GetByteCount(text ?? string.Empty) > SoftLimitBytes;
}
