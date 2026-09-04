using System.Text.RegularExpressions;

namespace WindowsToolbox.Modules.InstalledApps.Services;

/// <summary>解析 Windows 卸载项的 DisplayIcon，而不是将资源索引误当作文件路径。</summary>
public static partial class DisplayIconParser
{
    public static ParsedDisplayIcon? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string input = Environment.ExpandEnvironmentVariables(value.Trim());
        string path = input;
        int? iconIndex = null;

        if (input.StartsWith('"'))
        {
            int closingQuote = input.IndexOf('"', 1);
            if (closingQuote <= 1)
                return null;

            path = input[1..closingQuote].Trim();
            string suffix = input[(closingQuote + 1)..].Trim();
            if (suffix.Length > 0)
            {
                Match match = IconIndexSuffixRegex().Match(suffix);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out int parsed))
                    return null;
                iconIndex = parsed;
            }
        }
        else
        {
            // 只识别末尾的 ,整数；路径中本身的逗号不会被错误拆开。
            Match match = DisplayIconWithIndexRegex().Match(input);
            if (match.Success && int.TryParse(match.Groups["index"].Value, out int parsed))
            {
                path = match.Groups["path"].Value.Trim();
                iconIndex = parsed;
            }
        }

        path = path.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(path) ? null : new ParsedDisplayIcon(path, iconIndex);
    }

    [GeneratedRegex(@"^\s*,\s*(-?\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex IconIndexSuffixRegex();

    [GeneratedRegex(@"^(?<path>.+),\s*(?<index>-?\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex DisplayIconWithIndexRegex();
}
