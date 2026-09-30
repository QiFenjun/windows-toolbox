using System.Text.RegularExpressions;

namespace WindowsToolbox.Modules.Utilities.Regex.Models;

public enum RegexRunStatus { Valid, NoMatch, Matches, Limited, Invalid, TimedOut, Cancelled, TooLarge }
public sealed record RegexRequest(string Pattern, string Text, string Replacement = "", RegexOptions Options = RegexOptions.None, int TimeoutMilliseconds = 500);
public sealed record RegexResult(RegexRunStatus Status, IReadOnlyList<RegexMatchItem> Matches, string Replacement, string Message);

public sealed class RegexMatchItem(Match match, string[] names)
{
    public int Index => match.Index;
    public int Length => match.Length;
    public string Value => match.Value;
    public string Preview => match.ValueSpan[..Math.Min(200, match.Length)].ToString() + (match.Length > 200 ? "…" : "");
    public IReadOnlyList<RegexGroupItem> Groups => names.Select(name => new RegexGroupItem(name, match.Groups[name])).ToArray();
}

public sealed class RegexGroupItem(string name, Group group)
{
    public string Name => name;
    public bool Success => group.Success;
    public int Index => group.Index;
    public int Length => group.Length;
    public string Value => group.Value;
    public string Preview => group.ValueSpan[..Math.Min(200, group.Length)].ToString() + (group.Length > 200 ? "…" : "");
}
