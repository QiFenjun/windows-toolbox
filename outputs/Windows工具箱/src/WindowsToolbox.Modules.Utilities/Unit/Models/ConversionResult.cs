namespace WindowsToolbox.Modules.Utilities.Unit.Models;

/// <summary>Outcome of a conversion attempt: either a formatted value or a user-facing error.</summary>
public readonly record struct ConversionResult(bool Success, string Value, string? Error)
{
    public static ConversionResult Ok(string value) => new(true, value, null);
    public static ConversionResult Fail(string error) => new(false, string.Empty, error);
}
