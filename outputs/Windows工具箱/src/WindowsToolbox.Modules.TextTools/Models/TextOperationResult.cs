namespace WindowsToolbox.Modules.TextTools.Models;

public sealed record TextOperationResult(
    bool IsSuccess,
    string Output,
    string? Error = null,
    string? Message = null,
    int MatchCount = 0)
{
    public static TextOperationResult Success(string output, string? message = null, int matchCount = 0) =>
        new(true, output, null, message, matchCount);

    public static TextOperationResult Failure(string error) =>
        new(false, string.Empty, error);
}
