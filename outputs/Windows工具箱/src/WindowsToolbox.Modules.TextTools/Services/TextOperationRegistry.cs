using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WindowsToolbox.Modules.TextTools.Models;

namespace WindowsToolbox.Modules.TextTools.Services;

public static class TextOperationRegistry
{
    public static IReadOnlyList<ITextOperation> CreateDefault() =>
    [
        Op("case.upper", "全部大写", "基础", "将文本转换为当前文化的大写形式。",
            (input, _) => TextOperationResult.Success(input.ToUpper(CultureInfo.CurrentCulture))),
        Op("case.lower", "全部小写", "基础", "将文本转换为当前文化的小写形式。",
            (input, _) => TextOperationResult.Success(input.ToLower(CultureInfo.CurrentCulture))),
        Op("case.title", "首字母大写", "基础", "按当前系统文化规则转换标题大小写。",
            (input, _) => TextOperationResult.Success(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower(CultureInfo.CurrentCulture)))),
        Op("case.sentence", "Sentence case", "基础", "将每个句子的首个字母转换为大写。",
            (input, _) => ToSentenceCase(input)),
        Op("case.swap", "大小写反转", "基础", "反转字母的大小写，其它字符保持不变。",
            (input, _) => TextOperationResult.Success(SwapCase(input))),

        Op("space.trim", "Trim 首尾空白", "空白", "删除整个文本首尾的 Unicode 空白。",
            (input, _) => TextOperationResult.Success(input.Trim())),
        Op("space.trim-lines", "删除每行首尾空白", "空白", "逐行删除首尾空白。",
            (input, _) => TextOperationUtilities.TransformLines(input, lines => lines.Select(line => line.Trim()))),
        Op("space.remove-blank", "删除空行", "空白", "删除 IsNullOrWhiteSpace 行。",
            (input, _) => TextOperationUtilities.TransformLines(input, lines => lines.Where(line => !string.IsNullOrWhiteSpace(line)))),
        Op("space.collapse-blank", "合并连续空行", "空白", "将连续空行合并为一个空行。",
            (input, _) => TextOperationUtilities.TransformLines(input, CollapseBlankLines)),
        Op("space.collapse-spaces", "多个空格合并为一个", "空白", "每行将连续空格或 Tab 合并为一个空格。",
            (input, _) => TextOperationResult.Success(string.Join(Environment.NewLine,
                TextOperationUtilities.SplitLines(input).Select(line => Regex.Replace(line, @"[^\S\r\n]+", " "))))),
        Op("space.tabs", "Tab 转空格", "空白", "将 Tab 转换为固定数量的空格。",
            (input, options) => TextOperationResult.Success(input.Replace("\t", new string(' ', Math.Clamp(options.TabSize, 1, 16))))),
        Op("space.remove-all", "移除全部空白", "空白", "移除所有 Unicode 空白字符。",
            (input, _) => TextOperationResult.Success(string.Concat(input.Where(character => !char.IsWhiteSpace(character))))),

        Op("lines.sort-asc", "按行排序 A→Z", "行", "按 OrdinalIgnoreCase 排序，中文不承诺拼音排序。",
            (input, _) => TextOperationUtilities.TransformLines(input, lines => lines.OrderBy(line => line, StringComparer.OrdinalIgnoreCase))),
        Op("lines.sort-desc", "按行排序 Z→A", "行", "按 OrdinalIgnoreCase 倒序排序。",
            (input, _) => TextOperationUtilities.TransformLines(input, lines => lines.OrderByDescending(line => line, StringComparer.OrdinalIgnoreCase))),
        Op("lines.dedupe", "删除重复行", "行", "保留每行第一次出现的位置。",
            (input, _) => TextOperationUtilities.TransformLines(input, lines => lines.Distinct(StringComparer.Ordinal))),
        Op("lines.reverse", "行反转", "行", "反转行的顺序。",
            (input, _) => TextOperationUtilities.TransformLines(input, lines => lines.Reverse())),
        Op("lines.prefix", "每行添加前缀", "行", "为每一行添加 Prefix 参数。",
            (input, options) => TextOperationUtilities.TransformLines(input, lines => lines.Select(line => options.Prefix + line))),
        Op("lines.suffix", "每行添加后缀", "行", "为每一行添加 Suffix 参数。",
            (input, options) => TextOperationUtilities.TransformLines(input, lines => lines.Select(line => line + options.Suffix))),
        Op("lines.number", "添加行号", "行", "使用“1. text”格式添加行号。",
            (input, _) => TextOperationUtilities.TransformLines(input, lines => lines.Select((line, index) => $"{index + 1}. {line}"))),
        Op("lines.unnumber", "移除行号", "行", "只移除 Text Tools 添加的“数字. ”格式行号。",
            (input, _) => TextOperationUtilities.TransformLines(input, lines => lines.Select(line => Regex.Replace(line, @"^\s*\d+\.\s?", string.Empty)))),

        Op("find-replace", "查找与替换", "查找替换", "按普通文本查找并全部替换，支持区分大小写。",
            ReplaceText),

        Op("json.validate", "Validate JSON", "JSON", "验证 JSON，并保留原始文本。",
            ValidateJson),
        Op("json.pretty", "Pretty Print", "JSON", "验证 JSON 并生成带缩进的易读格式。", (input, _) => FormatJson(input, true)),
        Op("json.minify", "Minify", "JSON", "验证 JSON 并生成紧凑格式。", (input, _) => FormatJson(input, false)),
        Op("json.escape", "Escape JSON String", "JSON", "将文本编码为 JSON 字符串字面量。", (input, _) => TextOperationResult.Success(JsonSerializer.Serialize(input))),
        Op("json.unescape", "Unescape JSON String", "JSON", "解析 JSON 字符串字面量。", UnescapeJson),

        Op("url.encode", "URL Encode", "编码", "使用官方 Uri API 编码完整文本。", (input, _) => TextOperationResult.Success(Uri.EscapeDataString(input))),
        Op("url.decode", "URL Decode", "编码", "使用官方 Uri API 解码文本。", (input, _) => DecodeUrl(input)),
        Op("url.query", "Query 参数格式化", "编码", "将 URL 查询参数展开为 key = value 行。", (input, _) => FormatQuery(input)),

        Op("base64.encode", "UTF-8 → Base64", "编码", "将 UTF-8 文本编码为 Base64。", (input, _) => TextOperationResult.Success(Convert.ToBase64String(Encoding.UTF8.GetBytes(input)))),
        Op("base64.decode", "Base64 → UTF-8", "编码", "严格解码 Base64 并验证 UTF-8。", DecodeBase64),

        Op("unicode.escape", "Text → Unicode Escape", "编码", "将非 ASCII 字符编码为 Unicode 转义，正确处理 Emoji。", (input, _) => TextOperationResult.Success(EscapeUnicode(input))),
        Op("unicode.unescape", "Unicode Escape → Text", "编码", "解析 \\uXXXX 与合法 surrogate pair。", (input, _) => UnescapeUnicode(input)),

        Op("stats", "文本统计", "统计", "查看字符、行数和编码字节统计，不改变文本。",
            (input, _) => TextOperationResult.Success(input, "统计已更新"))
    ];

    private static TextOperation Op(string id, string name, string category, string description,
        Func<string, TextOperationOptions, TextOperationResult> execute) =>
        new(id, name, category, description, execute);

    private static TextOperationResult ToSentenceCase(string input)
    {
        string lower = input.ToLower(CultureInfo.CurrentCulture);
        StringBuilder builder = new(lower.Length);
        bool capitalize = true;
        foreach (char character in lower)
        {
            if (capitalize && char.IsLetter(character))
            {
                builder.Append(char.ToUpper(character, CultureInfo.CurrentCulture));
                capitalize = false;
            }
            else
                builder.Append(character);

            if (character is '.' or '!' or '?' or '\n' or '\r')
                capitalize = true;
        }
        return TextOperationResult.Success(builder.ToString());
    }

    private static string SwapCase(string input)
    {
        StringBuilder builder = new(input.Length);
        foreach (char character in input)
        {
            if (char.IsLetter(character))
                builder.Append(char.IsUpper(character) ? char.ToLower(character) : char.ToUpper(character));
            else
                builder.Append(character);
        }
        return builder.ToString();
    }

    private static IEnumerable<string> CollapseBlankLines(IEnumerable<string> lines)
    {
        bool previousBlank = false;
        foreach (string line in lines)
        {
            bool blank = string.IsNullOrWhiteSpace(line);
            if (blank && previousBlank)
                continue;
            yield return line;
            previousBlank = blank;
        }
    }

    private static TextOperationResult ReplaceText(string input, TextOperationOptions options)
    {
        if (string.IsNullOrEmpty(options.Find))
            return TextOperationResult.Failure("查找内容不能为空。");

        StringComparison comparison = options.MatchCase
            ? StringComparison.CurrentCulture
            : StringComparison.CurrentCultureIgnoreCase;
        StringBuilder output = new(input.Length);
        int cursor = 0;
        int count = 0;
        while (cursor < input.Length)
        {
            int index = input.IndexOf(options.Find, cursor, comparison);
            if (index < 0)
            {
                output.Append(input, cursor, input.Length - cursor);
                break;
            }

            output.Append(input, cursor, index - cursor);
            output.Append(options.Replace);
            cursor = index + options.Find.Length;
            count++;
        }

        return TextOperationResult.Success(output.ToString(), $"匹配 {count} 处", count);
    }

    private static TextOperationResult ValidateJson(string input, TextOperationOptions _)
    {
        try { using JsonDocument document = JsonDocument.Parse(input); return TextOperationResult.Success(input, "有效 JSON"); }
        catch (JsonException exception) { return TextOperationResult.Failure($"JSON 无效：{exception.Message}"); }
    }

    private static TextOperationResult FormatJson(string input, bool indented)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(input);
            string output = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = indented });
            return TextOperationResult.Success(output);
        }
        catch (JsonException exception) { return TextOperationResult.Failure($"JSON 无效：{exception.Message}"); }
    }

    private static TextOperationResult UnescapeJson(string input, TextOperationOptions _)
    {
        try
        {
            string? output = JsonSerializer.Deserialize<string>(input);
            return output is null ? TextOperationResult.Failure("JSON 字符串不能为空。") : TextOperationResult.Success(output);
        }
        catch (JsonException) { return TextOperationResult.Failure("无效的 JSON 字符串。"); }
    }

    private static TextOperationResult DecodeUrl(string input)
    {
        try { return TextOperationResult.Success(Uri.UnescapeDataString(input)); }
        catch (UriFormatException) { return TextOperationResult.Failure("无效的 URL 编码。"); }
    }

    private static TextOperationResult FormatQuery(string input)
    {
        string query = input.Contains('?') ? input[(input.IndexOf('?') + 1)..] : input.TrimStart('?');
        if (query.Length == 0)
            return TextOperationResult.Success(string.Empty);
        string[] pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        string output = string.Join(Environment.NewLine, pairs.Select(pair =>
        {
            int separator = pair.IndexOf('=');
            string key = separator < 0 ? pair : pair[..separator];
            string value = separator < 0 ? string.Empty : pair[(separator + 1)..];
            return $"{Uri.UnescapeDataString(key)} = {Uri.UnescapeDataString(value)}";
        }));
        return TextOperationResult.Success(output);
    }

    private static TextOperationResult DecodeBase64(string input, TextOperationOptions _)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(input);
            string output = new UTF8Encoding(false, true).GetString(bytes);
            return TextOperationResult.Success(output);
        }
        catch (FormatException) { return TextOperationResult.Failure("无效的 Base64 输入。"); }
        catch (DecoderFallbackException) { return TextOperationResult.Failure("Base64 内容不是有效 UTF-8 文本。"); }
    }

    private static string EscapeUnicode(string input)
    {
        StringBuilder builder = new(input.Length);
        foreach (System.Text.Rune rune in input.EnumerateRunes())
        {
            if (rune.Value <= 0x7F && rune.Value is not (0x09 or 0x0A or 0x0D))
                builder.Append((char)rune.Value);
            else if (rune.Value <= 0xFFFF)
                builder.Append($"\\u{rune.Value:X4}");
            else
            {
                int value = rune.Value - 0x10000;
                builder.Append($"\\u{0xD800 + (value >> 10):X4}\\u{0xDC00 + (value & 0x3FF):X4}");
            }
        }
        return builder.ToString();
    }

    private static TextOperationResult UnescapeUnicode(string input)
    {
        StringBuilder output = new(input.Length);
        for (int index = 0; index < input.Length; index++)
        {
            if (input[index] != '\\')
            {
                output.Append(input[index]);
                continue;
            }

            if (index + 5 >= input.Length || input[index + 1] != 'u' || !TryParseHex(input.AsSpan(index + 2, 4), out int code))
                return TextOperationResult.Failure("发现无效的 Unicode 转义。");
            index += 5;
            if (code is >= 0xD800 and <= 0xDBFF)
            {
                if (index + 6 >= input.Length || input[index + 1] != '\\' || input[index + 2] != 'u' ||
                    !TryParseHex(input.AsSpan(index + 3, 4), out int low) || low is < 0xDC00 or > 0xDFFF)
                    return TextOperationResult.Failure("高代理项后缺少合法的低代理项。");
                output.Append(char.ConvertFromUtf32(char.ConvertToUtf32((char)code, (char)low)));
                index += 6;
            }
            else if (code is >= 0xDC00 and <= 0xDFFF)
                return TextOperationResult.Failure("不允许单独的低代理项。");
            else
                output.Append((char)code);
        }
        return TextOperationResult.Success(output.ToString());
    }

    private static bool TryParseHex(ReadOnlySpan<char> value, out int code)
    {
        code = 0;
        foreach (char character in value)
        {
            int digit = character switch
            {
                >= '0' and <= '9' => character - '0',
                >= 'A' and <= 'F' => character - 'A' + 10,
                >= 'a' and <= 'f' => character - 'a' + 10,
                _ => -1
            };
            if (digit < 0) { code = 0; return false; }
            code = code * 16 + digit;
        }
        return true;
    }
}
