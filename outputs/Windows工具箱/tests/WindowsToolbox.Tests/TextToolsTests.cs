using System.Text;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.TextTools;
using WindowsToolbox.Modules.TextTools.Models;
using WindowsToolbox.Modules.TextTools.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class TextToolsTests
{
    [TestMethod] public void Upper() => Equal("ABC", "case.upper", "Abc");
    [TestMethod] public void Lower() => Equal("abc", "case.lower", "AbC");
    [TestMethod] public void Title() => Equal("Hello World", "case.title", "hello world");
    [TestMethod] public void Sentence() => Equal("Hello. World!", "case.sentence", "hELLO. wORLD!");
    [TestMethod] public void Swap() => Equal("aBc!", "case.swap", "AbC!");
    [TestMethod] public void Trim() => Equal("hello", "space.trim", "  hello  ");
    [TestMethod] public void TrimLines() => Equal("a\nb", "space.trim-lines", " a \n b ");
    [TestMethod] public void RemoveBlankLines() => Equal("a\nb", "space.remove-blank", "a\n\n \nb");
    [TestMethod] public void CollapseBlankLines() => Equal("a\n\nb", "space.collapse-blank", "a\n\n\n\nb");
    [TestMethod] public void CollapseSpaces() => Equal("a b\nc d", "space.collapse-spaces", "a  b\nc\t d");
    [TestMethod] public void Tabs() => Equal("a    b", "space.tabs", "a\tb", new TextOperationOptions { TabSize = 4 });
    [TestMethod] public void RemoveAllWhitespace() => Equal("ab", "space.remove-all", " a\t\nb ");
    [TestMethod] public void SortAscending() => Equal("a\nb\nc", "lines.sort-asc", "c\na\nb");
    [TestMethod] public void SortDescending() => Equal("c\nb\na", "lines.sort-desc", "a\nc\nb");
    [TestMethod] public void DedupePreservesFirst() => Equal("a\nb\nc", "lines.dedupe", "a\nb\na\nc\nb");
    [TestMethod] public void ReverseLines() => Equal("c\nb\na", "lines.reverse", "a\nb\nc");
    [TestMethod] public void PrefixLines() => Equal("> a\n> b", "lines.prefix", "a\nb", new TextOperationOptions { Prefix = "> " });
    [TestMethod] public void SuffixLines() => Equal("a;\nb;", "lines.suffix", "a\nb", new TextOperationOptions { Suffix = ";" });
    [TestMethod] public void AddLineNumbers() => Equal("1. a\n2. b", "lines.number", "a\nb");
    [TestMethod] public void RemoveLineNumbers() => Equal("a\nb", "lines.unnumber", "1. a\n2. b");
    [TestMethod] public void ReplaceAll() => Replace("a a", "x x", "x", "a");
    [TestMethod] public void ReplaceCaseInsensitive() => Replace("x x", "A a", "a", "x");
    [TestMethod] public void ReplaceCaseSensitive() => Replace("A x", "A a", "a", "x", true);
    [TestMethod] public void EmptyFindReturnsError() => Assert.IsFalse(Exec("find-replace", "abc", new TextOperationOptions()).IsSuccess);
    [TestMethod] public void ReplaceReportsMatchCount() => Assert.AreEqual(2, Exec("find-replace", "a a", new TextOperationOptions { Find = "a", Replace = "b" }).MatchCount);
    [TestMethod] public void JsonValidate() => Assert.IsTrue(Exec("json.validate", "{\"a\":1}").IsSuccess);
    [TestMethod] public void JsonInvalid() => Assert.IsFalse(Exec("json.validate", "{bad}").IsSuccess);
    [TestMethod] public void JsonPretty() => StringAssert.Contains(Exec("json.pretty", "{\"a\":1}").Output, "\n");
    [TestMethod] public void JsonMinify() => Equal("{\"a\":1}", "json.minify", "{ \"a\": 1 }");
    [TestMethod] public void JsonBigIntegerPreserved() => StringAssert.Contains(Exec("json.minify", "{\"n\":12345678901234567890}").Output, "12345678901234567890");
    [TestMethod] public void JsonEscape() => Assert.AreEqual("a\"b", Exec("json.unescape", Exec("json.escape", "a\"b").Output).Output);
    [TestMethod] public void JsonUnescape() => Equal("a\"b", "json.unescape", "\"a\\\"b\"");
    [TestMethod] public void UrlEncode() => Equal("a%20b", "url.encode", "a b");
    [TestMethod] public void UrlDecode() => Equal("a b", "url.decode", "a%20b");
    [TestMethod] public void QueryFormat() => Equal("a = 1\nb = 2", "url.query", "?a=1&b=2");
    [TestMethod] public void Base64Encode() => Equal("5Lit5paH", "base64.encode", "中文");
    [TestMethod] public void Base64Decode() => Equal("中文", "base64.decode", "5Lit5paH");
    [TestMethod] public void Base64Invalid() => Assert.IsFalse(Exec("base64.decode", "not-base64").IsSuccess);
    [TestMethod] public void Base64InvalidUtf8() => Assert.IsFalse(Exec("base64.decode", "/w==").IsSuccess);
    [TestMethod] public void UnicodeEscapeChinese() => Equal("\\u4E2D\\u6587", "unicode.escape", "中文");
    [TestMethod] public void UnicodeEscapeEmoji() => Equal("\\uD83D\\uDE00", "unicode.escape", "😀");
    [TestMethod] public void UnicodeUnescapePair() => Equal("😀", "unicode.unescape", "\\uD83D\\uDE00");
    [TestMethod] public void UnicodeUnescapeInvalid() => Assert.IsFalse(Exec("unicode.unescape", "\\uD83D").IsSuccess);
    [TestMethod] public void StatisticsCharacters() => Assert.AreEqual(3, TextStatistics.From("a b").Characters);
    [TestMethod] public void StatisticsNonWhitespace() => Assert.AreEqual(2, TextStatistics.From("a b").NonWhitespaceCharacters);
    [TestMethod] public void StatisticsWords() => Assert.AreEqual(2, TextStatistics.From("a b").WordCount);
    [TestMethod] public void StatisticsLines() => Assert.AreEqual(2, TextStatistics.From("a\nb").LineCount);
    [TestMethod] public void StatisticsUtf8Bytes() => Assert.AreEqual(6, TextStatistics.From("中文").Utf8Bytes);
    [TestMethod] public void StatisticsUtf16Bytes() => Assert.AreEqual(4, TextStatistics.From("中文").Utf16Bytes);
    [TestMethod] public void RegistryContainsExpectedOperations() => CollectionAssert.Contains(TextOperationRegistry.CreateDefault().Select(operation => operation.Id).ToList(), "json.pretty");
    [TestMethod] public void RegistryDescriptionsArePresent() => Assert.IsTrue(TextOperationRegistry.CreateDefault().All(operation => !string.IsNullOrWhiteSpace(operation.Description)));
    [TestMethod] public void ModuleMetadataIsStable()
    {
        IToolModule module = new TextToolsModule();
        Assert.AreEqual("text-tools", module.Id); Assert.AreEqual("Text Tools", module.DisplayName); Assert.AreEqual("效率工具", module.Category);
    }
    [TestMethod] public void ModuleKeywordsSupportSearch() => CollectionAssert.Contains(new TextToolsModule().Keywords.ToList(), "JSON");
    [TestMethod] public void CrLfIsNormalized() => Equal("a\nb", "lines.reverse", "b\r\na");
    [TestMethod] public void TabSizeIsClamped() => Equal("a b", "space.tabs", "a\tb", new TextOperationOptions { TabSize = 0 });
    [TestMethod] public void FailedOperationHasNoOutput() => Assert.AreEqual(string.Empty, Exec("json.pretty", "{bad}").Output);
    [TestMethod] public void EmptyInputStatsHaveZeroLines() => Assert.AreEqual(0, TextStatistics.From(string.Empty).LineCount);
    [TestMethod] public void EmptyFindDoesNotLoop() => Assert.IsFalse(Exec("find-replace", string.Empty, new TextOperationOptions()).IsSuccess);
    [TestMethod] public void UnicodeSurrogateRejectsLowOnly() => Assert.IsFalse(Exec("unicode.unescape", "\\uDE00").IsSuccess);
    [TestMethod] public void JsonEscapeRoundTrip() { string value = "中文 😀"; Assert.AreEqual(value, Exec("json.unescape", Exec("json.escape", value).Output).Output); }
    [TestMethod] public void Base64RoundTrip() { string value = "文本 😀"; Assert.AreEqual(value, Exec("base64.decode", Exec("base64.encode", value).Output).Output); }
    [TestMethod] public void PrefixAndSuffixCanBeEmpty() => Equal("a\nb", "lines.prefix", "a\nb");
    [TestMethod] public void SortUsesOrdinalIgnoreCase() => Equal("a\nB", "lines.sort-asc", "B\na");
    [TestMethod] public void DedupeIsOrdinal() => Equal("a\nA", "lines.dedupe", "a\nA");
    [TestMethod] public void StatsUseUtf16CodeUnits() => Assert.AreEqual(2, TextStatistics.From("😀").Characters);
    [TestMethod] public void UrlDecodeKeepsLiteralInvalidPercent() => Assert.AreEqual("%ZZ", Exec("url.decode", "%ZZ").Output);
    [TestMethod] public void RegistryHasNoDuplicateIds() { string[] ids = TextOperationRegistry.CreateDefault().Select(operation => operation.Id).ToArray(); Assert.AreEqual(ids.Length, ids.Distinct(StringComparer.Ordinal).Count()); }
    [TestMethod] public void RegistryHasAtLeastThirtyOperations() => Assert.IsTrue(TextOperationRegistry.CreateDefault().Count >= 30);

    private static TextOperationResult Exec(string id, string input, TextOperationOptions? options = null) =>
        TextOperationRegistry.CreateDefault().Single(operation => operation.Id == id).Execute(input, options ?? new TextOperationOptions());

    private static void Equal(string expected, string id, string input, TextOperationOptions? options = null) =>
        Assert.AreEqual(expected.Replace("\r\n", "\n"), Exec(id, input, options).Output.Replace("\r\n", "\n"));

    private static void Replace(string expected, string input, string find, string replacement, bool matchCase = false) =>
        Equal(expected, "find-replace", input, new TextOperationOptions { Find = find, Replace = replacement, MatchCase = matchCase });
}
