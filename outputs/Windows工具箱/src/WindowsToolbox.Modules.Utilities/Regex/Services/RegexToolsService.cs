using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using WindowsToolbox.Modules.Utilities.Regex.Models;
using RegexEngine = System.Text.RegularExpressions.Regex;

namespace WindowsToolbox.Modules.Utilities.Regex.Services;

public static class RegexToolsService
{
    public const int SoftInputBytes = 1024 * 1024;
    public const int LiveInputBytes = 5 * 1024 * 1024;
    public const int MaximumInputBytes = 10 * 1024 * 1024;
    public const int MaximumPatternCharacters = 16 * 1024;
    public const int MaximumMatches = 10_000;
    public const int MaximumOutputCharacters = 10 * 1024 * 1024;
    public const RegexOptions AllowedOptions = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline |
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;
    public static bool AllowsLive(string text) => Encoding.UTF8.GetByteCount(text) < LiveInputBytes;

    public static RegexResult Run(RegexRequest request, CancellationToken token = default)
    {
        List<RegexMatchItem> matches = [];
        try
        {
            token.ThrowIfCancellationRequested();
            if (Encoding.UTF8.GetByteCount(request.Text) > MaximumInputBytes || request.Pattern.Length > MaximumPatternCharacters || request.Replacement.Length > MaximumPatternCharacters)
                return new(RegexRunStatus.TooLarge, [], "", "输入超限：文本最多 10 MiB（UTF-8），表达式和替换各最多 16384 字符。请使用专门开发工具。");
            if (request.TimeoutMilliseconds is not (100 or 500 or 1000 or 2000) || (request.Options & ~AllowedOptions) != 0)
                return new(RegexRunStatus.Invalid, [], "", "正则选项或超时值无效。");
            TimeSpan timeout = TimeSpan.FromMilliseconds(request.TimeoutMilliseconds);
            RegexEngine regex = new(request.Pattern, request.Options, timeout);
            string[] names = regex.GetGroupNames();
            Stopwatch budget = Stopwatch.StartNew();
            void CheckBudget()
            {
                token.ThrowIfCancellationRequested();
                // Regex timeout bounds each search; this also bounds many individually cheap matches.
                if (budget.Elapsed > TimeSpan.FromMilliseconds(Math.Max(2000, request.TimeoutMilliseconds * 2)))
                    throw new RegexMatchTimeoutException();
            }
            Match match = regex.Match(request.Text);
            while (match.Success && matches.Count < MaximumMatches)
            {
                CheckBudget();
                matches.Add(new(match, names));
                match = match.NextMatch(); // .NET advances zero-length matches correctly.
            }
            bool limited = match.Success;
            long outputBound = request.Text.Length;
            int replacements = 0;
            string replacement;
            try
            {
                replacement = regex.Replace(request.Text, current =>
                {
                    CheckBudget();
                    if (++replacements > MaximumMatches) throw new OutputLimitException();
                    long expanded = ReplacementLengthBound(regex, current, request.Text.Length, request.Replacement);
                    outputBound += expanded - current.Length;
                    if (expanded > MaximumOutputCharacters || outputBound > MaximumOutputCharacters) throw new OutputLimitException();
                    return current.Result(request.Replacement);
                });
            }
            catch (OutputLimitException)
            {
                return new(RegexRunStatus.Limited, matches.AsReadOnly(), "", "已达到匹配/替换安全上限；替换预览未生成。最多显示 10,000 条，输出最多 10 Mi 字符。");
            }
            CheckBudget();
            return new(limited ? RegexRunStatus.Limited : matches.Count == 0 ? RegexRunStatus.NoMatch : RegexRunStatus.Matches,
                matches.AsReadOnly(), replacement, limited ? "已达到 10,000 条显示上限。" : matches.Count == 0 ? "No Match · 没有匹配。" : $"Matches: {matches.Count}");
        }
        catch (RegexMatchTimeoutException) { return new(RegexRunStatus.TimedOut, [], "", "正则执行超时。表达式可能产生灾难性回溯。"); }
        catch (ArgumentException) { return new(RegexRunStatus.Invalid, [], "", "正则表达式无效。请检查括号、转义字符和选项。"); }
        catch (OperationCanceledException) { return new(RegexRunStatus.Cancelled, [], "", "已取消。"); }
    }

    // Measure substitution tokens before Match.Result allocates; $' / $` / $_ may amplify large inputs.
    private static long ReplacementLengthBound(RegexEngine regex, Match match, int inputLength, string replacement)
    {
        long length = 0;
        for (int i = 0; i < replacement.Length; i++)
        {
            if (replacement[i] != '$' || i + 1 == replacement.Length) { length++; continue; }
            char next = replacement[i + 1];
            switch (next)
            {
                case '$': length++; i++; continue;
                case '&': length += match.Length; i++; continue;
                case '`': length += match.Index; i++; continue;
                case '\'': length += inputLength - match.Index - match.Length; i++; continue;
                case '_': length += inputLength; i++; continue;
                case '+':
                    for (int group = match.Groups.Count - 1; group > 0; group--)
                        if (match.Groups[group].Success) { length += match.Groups[group].Length; break; }
                    i++; continue;
            }
            int start = i + 1, end = start;
            string? name = null;
            if (next == '{')
            {
                end = replacement.IndexOf('}', start + 1);
                if (end >= 0) name = replacement[(start + 1)..end];
            }
            else if (char.IsAsciiDigit(next))
            {
                while (end < replacement.Length && char.IsAsciiDigit(replacement[end])) end++;
                name = replacement[start..end]; end--;
            }
            if (name is not null && regex.GroupNumberFromName(name) is int number && number >= 0)
            { length += match.Groups[number].Length; i = end; }
            else length++; // Unknown substitutions remain literal; remaining characters count normally.
        }
        return length;
    }

    private sealed class OutputLimitException : Exception;
}
