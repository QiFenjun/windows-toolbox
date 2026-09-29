using System.Globalization;

namespace WindowsToolbox.Modules.Utilities.Developer.Services;

/// <summary>Result of inspecting one UUID/GUID input. All strings are null when invalid.</summary>
public sealed record UuidInspection(
    bool IsValid,
    string? CanonicalD,
    string? CanonicalN,
    string? Version,
    string? Variant,
    string? SpecialName,
    string? Error)
{
    public static UuidInspection Invalid(string error) =>
        new(false, null, null, null, null, null, error);
}

/// <summary>
/// Validates, normalizes and dissects UUID/GUID text. Deterministic and pure: nothing is
/// persisted and no history is kept (generation belongs to Random Tools).
///
/// Version and variant are read from the canonical RFC string layout
/// ("xxxxxxxx-xxxx-Mxxx-Nxxx-…": M = version nibble, N = variant nibble), never from
/// .NET Guid's internal little-endian byte fields.
/// </summary>
public static class UuidInspector
{
    public const string NilUuid = "00000000-0000-0000-0000-000000000000";
    public const string MaxUuid = "ffffffff-ffff-ffff-ffff-ffffffffffff";

    public static UuidInspection Inspect(string? input)
    {
        string text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
            return UuidInspection.Invalid("请输入 UUID。");

        if (text.StartsWith('{') || text.EndsWith('}'))
        {
            if (!(text.StartsWith('{') && text.EndsWith('}') && text.Length >= 3))
                return UuidInspection.Invalid("UUID 格式无效。");
            text = text[1..^1];
        }

        if (!LooksLikeUuid(text))
            return UuidInspection.Invalid("UUID 格式无效。");

        if (!Guid.TryParse(text, out Guid guid))
            return UuidInspection.Invalid("UUID 格式无效。");

        // Guid round-trips only canonical forms we already validated, so "D" is RFC order.
        string canonicalD = guid.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant();
        string canonicalN = guid.ToString("N", CultureInfo.InvariantCulture).ToLowerInvariant();

        if (canonicalD == NilUuid)
            return new UuidInspection(true, canonicalD, canonicalN, "N/A", "N/A", "Nil UUID", null);

        string? specialName = canonicalD == MaxUuid ? "Max UUID" : null;

        // Canonical string index 14 is the version nibble; index 19 is the variant nibble.
        int version = HexValue(canonicalD[14]);
        string versionText = version is >= 1 and <= 8
            ? version.ToString(CultureInfo.InvariantCulture)
            : "Unknown";

        string variantText = HexValue(canonicalD[19]) switch
        {
            <= 0x7 => "NCS",
            <= 0xB => "RFC 4122 / RFC 9562",
            <= 0xD => "Microsoft (Legacy)",
            _ => "Future Reserved"
        };

        return new UuidInspection(true, canonicalD, canonicalN, versionText, variantText, specialName, null);
    }

    private static bool LooksLikeUuid(string text)
    {
        if (text.Length == 36)
        {
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (index is 8 or 13 or 18 or 23)
                {
                    if (character != '-') return false;
                }
                else if (HexValue(character) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        if (text.Length == 32)
        {
            foreach (char character in text)
            {
                if (HexValue(character) < 0)
                    return false;
            }
            return true;
        }

        return false;
    }

    private static int HexValue(char character)
    {
        if (character is >= '0' and <= '9') return character - '0';
        if (character is >= 'a' and <= 'f') return character - 'a' + 10;
        if (character is >= 'A' and <= 'F') return character - 'A' + 10;
        return -1;
    }
}
