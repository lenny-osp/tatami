using System.Text;

namespace Tatami.Core;

/// <summary>
/// 解析 Apple 的 .strings 檔：每一項是 "key" = "value";，可以有 // 和 /* */ 註解與空行。
/// 翻譯檔和 macOS 版共用（Resources/&lt;語言&gt;.lproj/Localizable.strings），規則和
/// scripts/check_localizations.py 相同。
/// </summary>
public static class StringsFile
{
    /// <summary>讀取檔案。UTF-8（可有 BOM）或有 BOM 的 UTF-16 都可以。</summary>
    public static Dictionary<string, string> Load(string path) =>
        Parse(File.ReadAllText(path, Encoding.UTF8), path);

    /// <summary>解析內容。格式錯誤時丟出 FormatException，訊息裡有行號。</summary>
    public static Dictionary<string, string> Parse(string text, string? sourceName = null)
    {
        var parser = new Parser(text, sourceName ?? "<strings>");
        return parser.Parse();
    }

    private sealed class Parser(string text, string sourceName)
    {
        private int pos;

        public Dictionary<string, string> Parse()
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            while (true)
            {
                SkipSpaceAndComments();
                if (pos >= text.Length) return entries;
                int keyStart = pos;
                var key = ReadString("key");
                Expect('=');
                var value = ReadString("value");
                Expect(';');
                if (!entries.TryAdd(key, value)) throw Fail($"duplicate key \"{key}\"", keyStart);
            }
        }

        private void SkipSpaceAndComments()
        {
            while (pos < text.Length)
            {
                char ch = text[pos];
                if (char.IsWhiteSpace(ch) || ch == '﻿')
                {
                    pos++;
                }
                else if (string.CompareOrdinal(text, pos, "//", 0, 2) == 0)
                {
                    int end = text.IndexOf('\n', pos);
                    pos = end < 0 ? text.Length : end + 1;
                }
                else if (string.CompareOrdinal(text, pos, "/*", 0, 2) == 0)
                {
                    int end = text.IndexOf("*/", pos + 2, StringComparison.Ordinal);
                    if (end < 0) throw Fail("unterminated /* comment", pos);
                    pos = end + 2;
                }
                else
                {
                    return;
                }
            }
        }

        private void Expect(char expected)
        {
            SkipSpaceAndComments();
            if (pos >= text.Length) throw Fail($"unexpected end of file, expected '{expected}'", pos);
            if (text[pos] != expected) throw Fail($"expected '{expected}' but found '{text[pos]}'", pos);
            pos++;
        }

        private string ReadString(string what)
        {
            SkipSpaceAndComments();
            if (pos >= text.Length || text[pos] != '"') throw Fail($"expected a quoted {what}", pos);
            int start = pos;
            pos++;
            var result = new StringBuilder();
            while (true)
            {
                if (pos >= text.Length) throw Fail($"unterminated {what}", start);
                char ch = text[pos];
                if (ch == '"')
                {
                    pos++;
                    return result.ToString();
                }
                if (ch != '\\')
                {
                    result.Append(ch);
                    pos++;
                    continue;
                }
                if (pos + 1 >= text.Length) throw Fail("backslash at end of file", pos);
                char escape = text[pos + 1];
                switch (escape)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '\'': result.Append('\''); break;
                    case 'n': result.Append('\n'); break;
                    case 't': result.Append('\t'); break;
                    case 'r': result.Append('\r'); break;
                    case '0': result.Append('\0'); break;
                    case 'U' or 'u':
                        if (pos + 6 > text.Length
                            || !int.TryParse(text.AsSpan(pos + 2, 4), System.Globalization.NumberStyles.HexNumber, null, out int code))
                        {
                            throw Fail($"\\{escape} must be followed by 4 hex digits", pos);
                        }
                        result.Append((char)code);
                        pos += 4;
                        break;
                    default:
                        throw Fail($"unknown escape \\{escape}", pos);
                }
                pos += 2;
            }
        }

        private FormatException Fail(string message, int at)
        {
            int line = 1;
            for (int i = 0; i < at && i < text.Length; i++)
            {
                if (text[i] == '\n') line++;
            }
            return new FormatException($"{sourceName}:{line}: {message}");
        }
    }
}
