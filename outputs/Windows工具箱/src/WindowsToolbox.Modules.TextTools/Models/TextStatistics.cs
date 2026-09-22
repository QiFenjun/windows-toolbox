using System.Text;

namespace WindowsToolbox.Modules.TextTools.Models;

public sealed record TextStatistics(
    int Characters,
    int NonWhitespaceCharacters,
    int WordCount,
    int LineCount,
    int Utf8Bytes,
    int Utf16Bytes)
{
    public static TextStatistics From(string? text)
    {
        string value = text ?? string.Empty;
        int words = 0;
        bool inWord = false;
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
                inWord = false;
            else if (!inWord)
            {
                words++;
                inWord = true;
            }
        }

        int lines = value.Length == 0 ? 0 : TextOperationUtilities.SplitLines(value).Count;
        return new TextStatistics(
            value.Length,
            value.Count(character => !char.IsWhiteSpace(character)),
            words,
            lines,
            Encoding.UTF8.GetByteCount(value),
            Encoding.Unicode.GetByteCount(value));
    }
}
