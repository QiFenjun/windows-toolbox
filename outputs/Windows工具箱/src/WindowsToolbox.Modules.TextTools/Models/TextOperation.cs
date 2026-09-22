using WindowsToolbox.Core.Utilities;

namespace WindowsToolbox.Modules.TextTools.Models;

public interface ITextOperation
{
    string Id { get; }
    string DisplayName { get; }
    string Category { get; }
    string Description { get; }
    TextOperationResult Execute(string input, TextOperationOptions options);
}

public sealed class TextOperation(
    string id,
    string displayName,
    string category,
    string description,
    Func<string, TextOperationOptions, TextOperationResult> execute) : ITextOperation
{
    public string Id => id;
    public string DisplayName => displayName;
    public string Category => category;
    public string Description => description;
    public TextOperationResult Execute(string input, TextOperationOptions options) => execute(input, options);
}

public static class TextOperationUtilities
{
    public static IReadOnlyList<string> SplitLines(string input) =>
        input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    public static string JoinLines(IEnumerable<string> lines) =>
        string.Join(Environment.NewLine, lines);

    public static TextOperationResult TransformLines(
        string input,
        Func<IEnumerable<string>, IEnumerable<string>> transform) =>
        TextOperationResult.Success(JoinLines(transform(SplitLines(input))));
}
