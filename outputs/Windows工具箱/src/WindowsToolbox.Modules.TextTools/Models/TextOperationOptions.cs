namespace WindowsToolbox.Modules.TextTools.Models;

public sealed class TextOperationOptions
{
    public string Prefix { get; init; } = string.Empty;
    public string Suffix { get; init; } = string.Empty;
    public string Find { get; init; } = string.Empty;
    public string Replace { get; init; } = string.Empty;
    public bool MatchCase { get; init; }
    public int TabSize { get; init; } = 4;
}
