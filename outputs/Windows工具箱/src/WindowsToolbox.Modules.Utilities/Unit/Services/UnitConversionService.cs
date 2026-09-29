using System.Globalization;
using System.Numerics;
using WindowsToolbox.Modules.Utilities.Unit.Models;

namespace WindowsToolbox.Modules.Utilities.Unit.Services;

/// <summary>
/// Offline unit conversion: parse → convert (decimal arithmetic) → smart formatting.
/// All conversions are synchronous and allocation-light; they are near-instant by design.
/// </summary>
public sealed class UnitConversionService
{
    public const string InvalidNumberError = "请输入有效数字。";
    public const string BelowAbsoluteZeroError = "低于绝对零度，无效。";
    public const string UnknownCategoryError = "未知的单位类别。";
    public const string UnknownUnitError = "未知的单位。";

    // Absolute zero in Celsius. Kelvin must stay >= 0, i.e. Celsius >= -273.15 °C.
    private const decimal AbsoluteZeroCelsius = -273.15m;

    // Formatting: at most this many significant digits for long decimals,
    // scientific notation outside the fixed-notation exponent window.
    private const int MaxSignificantDigits = 12;
    private const int ScientificUpperExponent = 15;
    private const int ScientificLowerExponent = -6;

    public static IReadOnlyList<UnitCategory> Categories => UnitCatalog.Categories;

    public static UnitCategory? FindCategory(string? categoryId) => UnitCatalog.Find(categoryId);

    /// <summary>
    /// Converts <paramref name="input"/> from <paramref name="fromUnitId"/> to
    /// <paramref name="toUnitId"/> inside <paramref name="categoryId"/>.
    /// Never throws for user input; failures come back as <see cref="ConversionResult.Error"/>.
    /// </summary>
    public ConversionResult Convert(
        string? input,
        string? categoryId,
        string? fromUnitId,
        string? toUnitId)
    {
        UnitCategory? category = UnitCatalog.Find(categoryId);
        if (category is null)
            return ConversionResult.Fail(UnknownCategoryError);

        UnitDefinition? from = category.Find(fromUnitId ?? string.Empty);
        UnitDefinition? to = category.Find(toUnitId ?? string.Empty);
        if (from is null || to is null)
            return ConversionResult.Fail(UnknownUnitError);

        if (!TryParseNumber(input, out decimal value))
            return ConversionResult.Fail(InvalidNumberError);

        if (category.Id == "temperature" || from.Kind == UnitKind.Temperature)
            return ConvertTemperature(value, from, to);

        decimal result = value * from.ToBaseFactor / to.ToBaseFactor;
        return ConversionResult.Ok(FormatValue(result));
    }

    /// <summary>Accepts integers, decimals, negative values and scientific notation (e.g. 1e-6).</summary>
    public static bool TryParseNumber(string? input, out decimal value)
    {
        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent |
            NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowTrailingWhite;
        return decimal.TryParse(input?.Trim(), styles, CultureInfo.InvariantCulture, out value);
    }

    private static ConversionResult ConvertTemperature(decimal value, UnitDefinition from, UnitDefinition to)
    {
        // Base scale is Celsius. Order of operations keeps common values exact in decimal
        // (e.g. 0 °C → 32 °F, 100 °C → 212 °F, 0 K → -273.15 °C).
        decimal celsius = from.Id switch
        {
            "c" => value,
            "k" => value - 273.15m,
            "f" => (value - 32m) * 5m / 9m,
            _ => value
        };

        if (celsius < AbsoluteZeroCelsius)
            return ConversionResult.Fail(BelowAbsoluteZeroError);

        decimal result = to.Id switch
        {
            "c" => celsius,
            "k" => celsius + 273.15m,
            "f" => celsius * 9m / 5m + 32m,
            _ => celsius
        };

        return ConversionResult.Ok(FormatValue(result));
    }

    /// <summary>
    /// Smart formatting:
    /// - zero prints "0"; integers print exactly (no rounding, no trailing-zero stripping);
    /// - long decimals are rounded to 12 significant digits with meaningless zeros trimmed;
    /// - values outside 1e-6 … 1e15 use plain .NET-style scientific notation (e.g. 1E-9, 1.25E+20).
    /// </summary>
    public static string FormatValue(decimal value)
    {
        if (value == 0m)
            return "0";

        int exponent = AdjustedExponent(value);

        if (exponent > ScientificUpperExponent || exponent < ScientificLowerExponent)
            return FormatScientific(value, exponent);

        decimal rounded = value;
        if (value != decimal.Truncate(value))
        {
            // Inside the fixed window decimals stays within Math.Round's -28…28 bound.
            int decimals = MaxSignificantDigits - 1 - exponent;
            rounded = Math.Round(value, decimals, MidpointRounding.ToEven);
        }

        return rounded.ToString("0.############################", CultureInfo.InvariantCulture);
    }

    private static string FormatScientific(decimal value, int exponent)
    {
        decimal magnitude = Math.Abs(value);
        decimal scaled = magnitude / Pow10(exponent);
        decimal mantissa = Math.Round(scaled, MaxSignificantDigits - 1, MidpointRounding.ToEven);
        if (mantissa >= 10m)
        {
            mantissa /= 10m;
            exponent += 1;
        }

        string mantissaText = mantissa.ToString("0.############################", CultureInfo.InvariantCulture);
        mantissaText = TrimTrailingZeros(mantissaText);
        string sign = value < 0 ? "-" : string.Empty;
        string exponentSign = exponent >= 0 ? "+" : string.Empty;
        return $"{sign}{mantissaText}E{exponentSign}{exponent.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string TrimTrailingZeros(string text)
    {
        if (!text.Contains('.'))
            return text;
        text = text.TrimEnd('0');
        return text.TrimEnd('.');
    }

    /// <summary>Base-10 order of magnitude: 10^exponent ≤ |value| &lt; 10^(exponent+1).</summary>
    private static int AdjustedExponent(decimal value)
    {
        decimal magnitude = Math.Abs(value);
        if (magnitude == 0m)
            return 0;

        int[] bits = decimal.GetBits(magnitude);
        int scale = (bits[3] >> 16) & 0x7F;
        BigInteger mantissa = ((BigInteger)(uint)bits[2] << 64) |
                              ((BigInteger)(uint)bits[1] << 32) |
                              (uint)bits[0];
        int digits = mantissa.ToString().Length;
        return digits - 1 - scale;
    }

    private static decimal Pow10(int exponent)
    {
        if (exponent == 0)
            return 1m;
        decimal result = 1m;
        int steps = Math.Abs(exponent);
        for (int index = 0; index < steps; index++)
            result *= 10m;
        return exponent > 0 ? result : 1m / result;
    }
}
