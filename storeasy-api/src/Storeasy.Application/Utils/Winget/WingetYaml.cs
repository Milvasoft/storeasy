using System.Globalization;
using System.Text;

namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// An ordered YAML mapping (manifest fields keep their order, so a rewritten manifest diffs cleanly).
/// </summary>
public sealed class YamlMap : List<KeyValuePair<string, object>>
{
    /// <summary>
    /// Value of <paramref name="key"/>, or null.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public object Get(string key) => this.FirstOrDefault(p => p.Key == key).Value;

    /// <summary>
    /// String value of <paramref name="key"/>, or null when missing or not a scalar.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public string Text(string key) => Get(key) as string;

    /// <summary>
    /// Mapping value of <paramref name="key"/>, or null.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public YamlMap Map(string key) => Get(key) as YamlMap;

    /// <summary>
    /// Sequence value of <paramref name="key"/>, or null.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public List<object> Seq(string key) => Get(key) as List<object>;

    /// <summary>
    /// Whether the mapping has <paramref name="key"/>.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool Has(string key) => this.Any(p => p.Key == key);

    /// <summary>
    /// Sets <paramref name="key"/> in place, or appends it (before <paramref name="before"/> when that key exists). A null
    /// value removes the key.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="before">Key to insert a new key in front of (keeps <c>ManifestType</c> and <c>ManifestVersion</c> last).</param>
    public void Set(string key, object value, string before = null)
    {
        var index = FindIndex(p => p.Key == key);

        if (value == null)
        {
            if (index >= 0)
                RemoveAt(index);

            return;
        }

        if (index >= 0)
        {
            this[index] = new(key, value);
            return;
        }

        var at = before == null ? -1 : FindIndex(p => p.Key == before);

        if (at >= 0)
            Insert(at, new(key, value));
        else
            Add(new(key, value));
    }

    /// <summary>
    /// A deep copy.
    /// </summary>
    /// <returns></returns>
    public YamlMap Clone()
    {
        var copy = new YamlMap();

        copy.AddRange(this.Select(p => new KeyValuePair<string, object>(p.Key, WingetYaml.CloneValue(p.Value))));

        return copy;
    }
}

/// <summary>
/// Thrown for YAML the reader does not understand.
/// </summary>
/// <param name="message"></param>
public sealed class WingetYamlException(string message) : Exception(message);

/// <summary>
/// Reads and writes the block style YAML subset winget manifests use (mappings, sequences, plain, quoted and block
/// scalars, empty flow collections and one line flow sequences, comments). Scalars stay strings: the manifest schema, not
/// YAML, decides their type. No dependency: the subset is small and the writer must produce the layout winget-pkgs
/// expects (sequence items at their key's indent, block scalars for multi line texts).
/// </summary>
public static class WingetYaml
{
    private static readonly HashSet<string> _ambiguous = new(StringComparer.Ordinal) { "~", "null", "Null", "NULL", "" };

    /// <summary>
    /// Parses one YAML document into a mapping.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static YamlMap Parse(string text)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

        if (lines.Count > 0 && lines[0].Length > 0 && lines[0][0] == '﻿')
            lines[0] = lines[0][1..];

        // Document markers carry nothing a manifest needs.
        for (var k = 0; k < lines.Count; k++)
        {
            if (lines[k].TrimEnd() is "---" or "...")
                lines[k] = string.Empty;

            if (lines[k].Contains('\t') && lines[k].TrimStart(' ').StartsWith('\t'))
                throw new WingetYamlException($"Line {k + 1}: tabs are not allowed for indentation.");
        }

        var reader = new Reader(lines);
        var i = 0;

        reader.SkipBlank(ref i);

        if (i >= lines.Count)
            return [];

        var root = reader.ParseNode(ref i, Indent(lines[i]));

        reader.SkipBlank(ref i);

        if (i < lines.Count)
            throw new WingetYamlException($"Line {i + 1}: unexpected content.");

        return root as YamlMap ?? throw new WingetYamlException("The document is not a mapping.");
    }

    /// <summary>
    /// Writes a mapping as block YAML with LF line ends and a final line feed.
    /// </summary>
    /// <param name="map"></param>
    /// <param name="header">Comment lines written first (without <c>#</c>).</param>
    /// <returns></returns>
    public static string Write(YamlMap map, IEnumerable<string> header = null)
    {
        var builder = new StringBuilder();

        foreach (var line in header ?? [])
            builder.Append("# ").Append(line).Append('\n');

        if (header?.Any() == true)
            builder.Append('\n');

        WriteMap(builder, map, 0);

        return builder.ToString();
    }

    /// <summary>
    /// A deep copy of a parsed value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static object CloneValue(object value) => value switch
    {
        YamlMap map => map.Clone(),
        List<object> list => list.Select(CloneValue).ToList(),
        _ => value,
    };

    /// <summary>
    /// The scalar as written: plain when YAML reads it back unchanged, single quoted otherwise, double quoted when it holds
    /// control characters.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string Scalar(string value)
    {
        if (value.Any(c => char.IsControl(c)))
            return "\"" + Escape(value) + "\"";

        return NeedsQuotes(value) ? "'" + value.Replace("'", "''") + "'" : value;
    }

    private static bool NeedsQuotes(string value)
    {
        if (_ambiguous.Contains(value))
            return true;

        if (value != value.Trim())
            return true;

        if ("-?:,[]{}#&*!|>'\"%@`".Contains(value[0]))
            return true;

        return value.Contains(": ", StringComparison.Ordinal) || value.Contains(" #", StringComparison.Ordinal) || value.EndsWith(':');
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder();

        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\t' => "\\t",
                '\r' => "\\r",
                _ when char.IsControl(c) => "\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture),
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }

    private static void WriteMap(StringBuilder builder, YamlMap map, int indent)
    {
        foreach (var (key, value) in map)
        {
            if (value == null)
                continue;

            var pad = new string(' ', indent);
            var name = Scalar(key);

            switch (value)
            {
                case string text when text.Contains('\n'):
                    WriteBlock(builder, pad + name + ":", text, indent + 2);
                    break;
                case string text:
                    builder.Append(pad).Append(name).Append(": ").Append(Scalar(text)).Append('\n');
                    break;
                case YamlMap child when child.Count == 0:
                    builder.Append(pad).Append(name).Append(": {}\n");
                    break;
                case YamlMap child:
                    builder.Append(pad).Append(name).Append(":\n");
                    WriteMap(builder, child, indent + 2);
                    break;
                case List<object> list when list.Count == 0:
                    builder.Append(pad).Append(name).Append(": []\n");
                    break;
                case List<object> list:
                    builder.Append(pad).Append(name).Append(":\n");
                    WriteSeq(builder, list, indent);
                    break;
                default:
                    builder.Append(pad).Append(name).Append(": ").Append(Scalar(Convert.ToString(value, CultureInfo.InvariantCulture))).Append('\n');
                    break;
            }
        }
    }

    private static void WriteSeq(StringBuilder builder, List<object> list, int indent)
    {
        var pad = new string(' ', indent);

        foreach (var item in list)
        {
            switch (item)
            {
                case YamlMap map when map.Count > 0:
                {
                    // "- " then the first key; the other keys line up under it.
                    var nested = new StringBuilder();

                    WriteMap(nested, map, indent + 2);

                    var text = nested.ToString();

                    builder.Append(pad).Append("- ").Append(text[(indent + 2)..]);
                    break;
                }
                case YamlMap:
                    builder.Append(pad).Append("- {}\n");
                    break;
                case List<object> inner:
                    builder.Append(pad).Append("-\n");
                    WriteSeq(builder, inner, indent + 2);
                    break;
                case string text when text.Contains('\n'):
                    WriteBlock(builder, pad + "-", text, indent + 2);
                    break;
                default:
                    builder.Append(pad).Append("- ").Append(Scalar(Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty)).Append('\n');
                    break;
            }
        }
    }

    private static void WriteBlock(StringBuilder builder, string head, string text, int indent)
    {
        var body = text.Replace("\r\n", "\n");
        var keep = body.EndsWith('\n');
        var lines = (keep ? body[..^1] : body).Split('\n');

        builder.Append(head).Append(keep ? " |" : " |-");

        // A first line that starts with a space needs an explicit indentation indicator.
        if (lines.Length > 0 && lines[0].StartsWith(' '))
            builder.Append('2');

        builder.Append('\n');

        var pad = new string(' ', indent);

        foreach (var line in lines)
            builder.Append(line.Length == 0 ? string.Empty : pad + line.TrimEnd()).Append('\n');
    }

    internal static int Indent(string line)
    {
        var count = 0;

        while (count < line.Length && line[count] == ' ')
            count++;

        return count;
    }

    private sealed class Reader(List<string> lines)
    {
        private readonly List<string> _lines = lines;

        public void SkipBlank(ref int i)
        {
            while (i < _lines.Count && IsBlank(_lines[i]))
                i++;
        }

        private static bool IsBlank(string line)
        {
            var trimmed = line.Trim();

            return trimmed.Length == 0 || trimmed[0] == '#';
        }

        public object ParseNode(ref int i, int indent)
        {
            var content = _lines[i][indent..];

            return IsSeqItem(content) ? ParseSeq(ref i, indent) : ParseMap(ref i, indent);
        }

        private static bool IsSeqItem(string content) => content == "-" || content.StartsWith("- ", StringComparison.Ordinal);

        private YamlMap ParseMap(ref int i, int indent)
        {
            var map = new YamlMap();

            while (true)
            {
                SkipBlank(ref i);

                if (i >= _lines.Count)
                    break;

                var line = _lines[i];
                var lineIndent = Indent(line);

                if (lineIndent < indent)
                    break;

                if (lineIndent > indent)
                    throw new WingetYamlException($"Line {i + 1}: unexpected indentation.");

                var content = line[indent..];

                if (IsSeqItem(content))
                    break;

                var (key, rest) = SplitKey(content, i);

                if (map.Has(key))
                    throw new WingetYamlException($"Line {i + 1}: duplicate key '{key}'.");

                i++;
                map.Add(new(key, ParseValue(ref i, indent, rest, allowSameIndentSeq: true)));
            }

            return map;
        }

        private List<object> ParseSeq(ref int i, int indent)
        {
            var list = new List<object>();

            while (true)
            {
                SkipBlank(ref i);

                if (i >= _lines.Count)
                    break;

                var line = _lines[i];

                if (Indent(line) != indent || !IsSeqItem(line[indent..]))
                    break;

                var after = line[(indent + 1)..];
                var spaces = Indent(after);
                var item = after.Trim();

                if (item.Length == 0 || item[0] == '#')
                {
                    i++;
                    list.Add(ParseValue(ref i, indent, string.Empty, allowSameIndentSeq: false));
                    continue;
                }

                var column = indent + 1 + spaces;

                if (IsSeqItem(item) || LooksLikeKey(item))
                {
                    // "- Key: value": the item is a mapping (or a nested sequence) starting at the item's column.
                    _lines[i] = new string(' ', column) + line[column..];
                    list.Add(ParseNode(ref i, column));
                    continue;
                }

                i++;
                list.Add(ParseValue(ref i, indent, item, allowSameIndentSeq: false));
            }

            return list;
        }

        private object ParseValue(ref int i, int indent, string rest, bool allowSameIndentSeq)
        {
            rest = StripComment(rest).Trim();

            if (rest.Length == 0)
            {
                var j = i;

                SkipBlank(ref j);

                if (j >= _lines.Count)
                    return null;

                var nextIndent = Indent(_lines[j]);

                if (nextIndent > indent || (allowSameIndentSeq && nextIndent == indent && IsSeqItem(_lines[j][indent..])))
                {
                    i = j;
                    return ParseNode(ref i, nextIndent);
                }

                return null;
            }

            if (rest[0] is '|' or '>')
                return ParseBlockScalar(ref i, indent, rest);

            if (rest[0] == '"')
                return ParseDoubleQuoted(ref i, rest);

            if (rest[0] == '\'')
                return ParseSingleQuoted(ref i, rest);

            if (rest[0] == '[')
                return ParseFlowSeq(ref i, rest);

            if (rest == "{}")
                return new YamlMap();

            if (rest[0] == '{')
                throw new WingetYamlException($"Line {i}: flow mappings are not supported.");

            if (rest[0] is '&' or '*' or '!')
                throw new WingetYamlException($"Line {i}: anchors, aliases and tags are not supported.");

            // A plain scalar may continue on more indented lines; they fold into one line.
            var builder = new StringBuilder(rest);

            while (i < _lines.Count && !IsBlank(_lines[i]) && Indent(_lines[i]) > indent && !LooksLikeKey(_lines[i].Trim()) && !IsSeqItem(_lines[i].Trim()))
            {
                builder.Append(' ').Append(StripComment(_lines[i]).Trim());
                i++;
            }

            return builder.ToString();
        }

        private string ParseBlockScalar(ref int i, int indent, string header)
        {
            var literal = header[0] == '|';
            var chomp = header.Contains('-') ? '-' : header.Contains('+') ? '+' : ' ';
            var digit = header.FirstOrDefault(char.IsDigit);
            var collected = new List<string>();
            var blockIndent = digit != default ? indent + (digit - '0') : -1;

            while (i < _lines.Count)
            {
                var line = _lines[i];

                if (line.Trim().Length == 0)
                {
                    collected.Add(string.Empty);
                    i++;
                    continue;
                }

                var lineIndent = Indent(line);

                if (blockIndent < 0)
                {
                    if (lineIndent <= indent)
                        break;

                    blockIndent = lineIndent;
                }

                if (lineIndent < blockIndent)
                    break;

                collected.Add(line[blockIndent..]);
                i++;
            }

            var trailing = 0;

            while (trailing < collected.Count && collected[^(trailing + 1)].Length == 0)
                trailing++;

            var body = collected.Take(collected.Count - trailing).ToList();
            string text;

            if (literal)
            {
                text = string.Join('\n', body);
            }
            else
            {
                var builder = new StringBuilder();

                for (var k = 0; k < body.Count; k++)
                {
                    if (k > 0)
                    {
                        var previous = body[k - 1];
                        var current = body[k];

                        builder.Append(previous.Length == 0 || current.Length == 0 || current.StartsWith(' ') || previous.StartsWith(' ') ? "\n" : " ");
                    }

                    builder.Append(body[k]);
                }

                text = builder.ToString().Replace(" \n", "\n");
            }

            if (body.Count == 0)
                return string.Empty;

            return chomp switch
            {
                '-' => text,
                '+' => text + new string('\n', trailing + 1),
                _ => text + "\n",
            };
        }

        private string ParseDoubleQuoted(ref int i, string rest)
        {
            var text = rest;
            var builder = new StringBuilder();
            var k = 1;

            while (true)
            {
                if (k >= text.Length)
                {
                    if (i >= _lines.Count)
                        throw new WingetYamlException("Unterminated double quoted scalar.");

                    builder.Append(' ');
                    text = _lines[i].Trim();
                    i++;
                    k = 0;
                    continue;
                }

                var c = text[k];

                if (c == '"')
                {
                    EnsureNothingAfter(text[(k + 1)..], i);
                    return builder.ToString();
                }

                if (c == '\\' && k + 1 < text.Length)
                {
                    var e = text[k + 1];
                    k += 2;

                    switch (e)
                    {
                        case 'n': builder.Append('\n'); break;
                        case 't': builder.Append('\t'); break;
                        case 'r': builder.Append('\r'); break;
                        case '0': builder.Append('\0'); break;
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case ' ': builder.Append(' '); break;
                        case 'x' when k + 2 <= text.Length:
                            builder.Append((char)int.Parse(text.AsSpan(k, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            k += 2;
                            break;
                        case 'u' when k + 4 <= text.Length:
                            builder.Append((char)int.Parse(text.AsSpan(k, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            k += 4;
                            break;
                        default:
                            throw new WingetYamlException($"Line {i}: unknown escape \\{e}.");
                    }

                    continue;
                }

                builder.Append(c);
                k++;
            }
        }

        private string ParseSingleQuoted(ref int i, string rest)
        {
            var text = rest;
            var builder = new StringBuilder();
            var k = 1;

            while (true)
            {
                if (k >= text.Length)
                {
                    if (i >= _lines.Count)
                        throw new WingetYamlException("Unterminated single quoted scalar.");

                    builder.Append(' ');
                    text = _lines[i].Trim();
                    i++;
                    k = 0;
                    continue;
                }

                if (text[k] == '\'')
                {
                    if (k + 1 < text.Length && text[k + 1] == '\'')
                    {
                        builder.Append('\'');
                        k += 2;
                        continue;
                    }

                    EnsureNothingAfter(text[(k + 1)..], i);
                    return builder.ToString();
                }

                builder.Append(text[k]);
                k++;
            }
        }

        private List<object> ParseFlowSeq(ref int i, string rest)
        {
            var text = rest;

            while (!text.TrimEnd().EndsWith(']'))
            {
                if (i >= _lines.Count)
                    throw new WingetYamlException("Unterminated flow sequence.");

                text += " " + StripComment(_lines[i]).Trim();
                i++;
            }

            var inner = text.Trim()[1..^1];
            var items = new List<object>();
            var current = new StringBuilder();
            var quote = '\0';

            foreach (var c in inner)
            {
                if (quote != '\0')
                {
                    current.Append(c);

                    if (c == quote)
                        quote = '\0';

                    continue;
                }

                if (c is '"' or '\'')
                {
                    quote = c;
                    current.Append(c);
                    continue;
                }

                if (c is '[' or '{')
                    throw new WingetYamlException($"Line {i}: nested flow collections are not supported.");

                if (c == ',')
                {
                    AddFlowItem(items, current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            AddFlowItem(items, current.ToString());

            return items;
        }

        private void AddFlowItem(List<object> items, string raw)
        {
            var value = raw.Trim();

            if (value.Length == 0)
                return;

            var position = 0;

            items.Add(value[0] switch
            {
                '"' => ParseDoubleQuoted(ref position, value),
                '\'' => ParseSingleQuoted(ref position, value),
                _ => value,
            });
        }

        private static void EnsureNothingAfter(string tail, int line)
        {
            var rest = tail.Trim();

            if (rest.Length > 0 && rest[0] != '#')
                throw new WingetYamlException($"Line {line}: unexpected text after a quoted scalar.");
        }

        private static bool LooksLikeKey(string content)
        {
            if (content.Length == 0 || content[0] is '"' or '\'' && !QuotedKey(content))
                return false;

            return KeyEnd(content) >= 0;
        }

        private static bool QuotedKey(string content)
        {
            var quote = content[0];
            var close = content.IndexOf(quote, 1);

            return close > 0 && close + 1 < content.Length && content[close + 1] == ':' && (close + 2 == content.Length || content[close + 2] == ' ');
        }

        private static int KeyEnd(string content)
        {
            if (content[0] is '"' or '\'')
            {
                var close = content.IndexOf(content[0], 1);

                return close > 0 && close + 1 < content.Length && content[close + 1] == ':' && (close + 2 == content.Length || content[close + 2] == ' ') ? close + 1 : -1;
            }

            for (var k = 0; k < content.Length; k++)
            {
                if (content[k] == '#' && k > 0 && content[k - 1] == ' ')
                    return -1;

                if (content[k] == ':' && (k + 1 == content.Length || content[k + 1] == ' '))
                    return k;
            }

            return -1;
        }

        private (string Key, string Value) SplitKey(string content, int i)
        {
            var end = KeyEnd(content);

            if (end < 0)
                throw new WingetYamlException($"Line {i + 1}: expected 'key: value'.");

            var key = content[..end].Trim();

            if (key.Length >= 2 && key[0] is '"' or '\'' && key[^1] == key[0])
                key = key[1..^1];

            return (key, content[(end + 1)..]);
        }

        private static string StripComment(string text)
        {
            var quote = '\0';

            for (var k = 0; k < text.Length; k++)
            {
                var c = text[k];

                if (quote != '\0')
                {
                    if (c == quote)
                        quote = '\0';

                    continue;
                }

                if ((c is '"' or '\'') && (k == 0 || text[k - 1] == ' ' || text[k - 1] == '['))
                {
                    quote = c;
                    continue;
                }

                if (c == '#' && (k == 0 || text[k - 1] == ' '))
                    return text[..k];
            }

            return text;
        }
    }
}
