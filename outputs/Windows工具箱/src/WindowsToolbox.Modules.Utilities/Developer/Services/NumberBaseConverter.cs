using System.Globalization;
using System.Numerics;
using System.Text;

namespace WindowsToolbox.Modules.Utilities.Developer.Services;

/// <summary>Formatted four-base output for one value.</summary>
public sealed record BaseConversionResult(string Binary, string Octal, string DecimalValue, string Hex)
{
    public IReadOnlyList<(string Label, string Value)> Lines { get; } =
    [
        ("BIN", Binary),
        ("OCT", Octal),
        ("DEC", DecimalValue),
        ("HEX", Hex)
    ];
}

/// <summary>
/// Arbitrary-precision base conversion for binary / octal / decimal / hexadecimal.
/// Pure functions, no state, no network, no persistence. Negative values use a plain
/// mathematical minus sign (no two's complement, which would require a fixed bit width).
/// </summary>
public static class NumberBaseConverter
{
    /// <summary>UI input cap so a pasted megabyte of digits cannot stall the app.</summary>
    public const int MaxInputLength = 4096;

    public const int BinaryBase = 2;
    public const int OctalBase = 8;
    public const int DecimalBase = 10;
    public const int HexBase = 16;

    private const string DecimalDigits = "0123456789";
    private const string HexDigitsUpper = "0123456789ABCDEF";

    public static readonly IReadOnlyList<int> SupportedBases = [BinaryBase, OctalBase, DecimalBase, HexBase];

    public static string BaseName(int baseValue) => baseValue switch
    {
        BinaryBase => "二进制",
        OctalBase => "八进制",
        DecimalBase => "十进制",
        HexBase => "十六进制",
        _ => throw new ArgumentOutOfRangeException(nameof(baseValue))
    };

    /// <summary>
    /// Parses <paramref name="input"/> in <paramref name="baseValue"/> (2/8/10/16).
    /// An optional sign is allowed; an optional prefix (0b/0o/0x) must match the selected base.
    /// Never partially parses: any illegal character fails the whole input.
    /// </summary>
    public static bool TryParse(string? input, int baseValue, out BigInteger value, out string? error)
    {
        if (baseValue is not (BinaryBase or OctalBase or DecimalBase or HexBase))
            throw new ArgumentOutOfRangeException(nameof(baseValue));

        value = BigInteger.Zero;
        error = null;

        string text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = "请输入数字。";
            return false;
        }
        if (text.Length > MaxInputLength)
        {
            error = "输入过长。";
            return false;
        }

        bool negative = false;
        if (text[0] == '-' || text[0] == '+')
        {
            negative = text[0] == '-';
            text = text[1..];
            if (text.Length == 0)
            {
                error = "请输入数字。";
                return false;
            }
        }

        if (!TryStripPrefix(ref text, baseValue, out error))
            return false;

        if (text.Length == 0)
        {
            error = "请输入数字。";
            return false;
        }

        BigInteger magnitude = BigInteger.Zero;
        foreach (char character in text)
        {
            int digit = HexDigitValue(character);
            if (digit < 0 || digit >= baseValue)
            {
                error = $"包含{BaseName(baseValue)}无效的数字字符。";
                return false;
            }
            magnitude = magnitude * baseValue + digit;
        }

        value = negative ? -magnitude : magnitude;
        return true;
    }

    /// <summary>Converts a value into all four output strings (hex is uppercase).</summary>
    public static BaseConversionResult Format(BigInteger value, bool showPrefix = false)
    {
        bool negative = value.Sign < 0;
        BigInteger magnitude = BigInteger.Abs(value);
        string sign = negative ? "-" : string.Empty;

        string binary = ToRadix(magnitude, BinaryBase);
        string octal = ToRadix(magnitude, OctalBase);
        string decimalText = (negative ? "-" : string.Empty) + magnitude.ToString(CultureInfo.InvariantCulture);
        string hex = ToRadix(magnitude, HexBase);

        if (showPrefix)
        {
            binary = "0b" + binary;
            octal = "0o" + octal;
            hex = "0x" + hex;
        }

        return new BaseConversionResult(sign + binary, sign + octal, decimalText, sign + hex);
    }

    public static bool TryConvert(string? input, int baseValue, bool showPrefix, out BaseConversionResult result, out string? error)
    {
        if (!TryParse(input, baseValue, out BigInteger value, out error))
        {
            result = new BaseConversionResult(string.Empty, string.Empty, string.Empty, string.Empty);
            return false;
        }

        result = Format(value, showPrefix);
        return true;
    }

    private static bool TryStripPrefix(ref string text, int baseValue, out string? error)
    {
        error = null;
        if (text.Length < 2 || text[0] != '0')
            return true;

        string prefix = text[..2].ToLowerInvariant();
        if (prefix is not ("0b" or "0o" or "0x"))
            return true;

        int prefixBase = prefix switch
        {
            "0b" => BinaryBase,
            "0o" => OctalBase,
            _ => HexBase
        };

        if (prefixBase != baseValue)
        {
            error = "前缀与当前进制不一致。";
            return false;
        }

        text = text[2..];
        return true;
    }

    private static int HexDigitValue(char character)
    {
        if (character is >= '0' and <= '9') return character - '0';
        if (character is >= 'a' and <= 'f') return character - 'a' + 10;
        if (character is >= 'A' and <= 'F') return character - 'A' + 10;
        return -1;
    }

    private static string ToRadix(BigInteger magnitude, int radix)
    {
        if (magnitude.IsZero)
            return "0";

        string digits = radix == HexBase ? HexDigitsUpper : DecimalDigits;
        StringBuilder builder = new(64);

        BigInteger current = magnitude;
        while (!current.IsZero)
        {
            current = BigInteger.DivRem(current, radix, out BigInteger remainder);
            builder.Append(digits[(int)remainder]);
        }

        // Digits were appended least-significant first.
        char[] characters = new char[builder.Length];
        builder.CopyTo(0, characters, 0, builder.Length);
        Array.Reverse(characters);
        return new string(characters);
    }
}
