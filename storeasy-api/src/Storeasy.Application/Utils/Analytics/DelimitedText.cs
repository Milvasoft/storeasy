using System.Globalization;
using System.Text;

namespace Storeasy.Application.Utils.Analytics;

/// <summary>
/// Reads the report files of the stores: decodes the bytes (UTF-16 with a byte order mark, as Google Play writes them, or
/// UTF-8) and splits comma or tab separated lines with RFC 4180 quoting. Columns are found by header name, ignoring case
/// and spaces, so a reordered or renamed column fails loudly instead of being read as another one.
/// </summary>
public sealed class DelimitedText
{
    private readonly Dictionary<string, int> _columns;

    private DelimitedText(List<string> header, List<List<string>> rows)
    {
        Header = header;
        Rows = rows;
        _columns = header.Select((name, index) => (Key: Key(name), index))
                         .Where(c => c.Key.Length > 0)
                         .GroupBy(c => c.Key)
                         .ToDictionary(g => g.Key, g => g.First().index);
    }

    /// <summary>
    /// Header cells as written.
    /// </summary>
    public IReadOnlyList<string> Header { get; }

    /// <summary>
    /// Data rows (empty lines skipped).
    /// </summary>
    public IReadOnlyList<List<string>> Rows { get; }

    /// <summary>
    /// Decodes report bytes: UTF-16 LE or BE when a byte order mark says so, UTF-8 otherwise (a UTF-8 mark is dropped).
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;

        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }

    /// <summary>
    /// Parses a file whose first line is the header. The separator is a tab when the header has one, a comma otherwise.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static DelimitedText Parse(string text)
    {
        text = (text ?? string.Empty).TrimStart('﻿');

        var firstLine = text.Split('\n', 2)[0];
        var separator = firstLine.Contains('\t') ? '\t' : ',';
        var lines = Split(text, separator);

        if (lines.Count == 0)
            return new DelimitedText([], []);

        return new DelimitedText(lines[0], [.. lines.Skip(1).Where(l => l.Any(c => c.Length > 0))]);
    }

    /// <summary>
    /// Index of the first column whose name (ignoring case, spaces and punctuation) equals one of
    /// <paramref name="names"/>, or -1.
    /// </summary>
    /// <param name="names"></param>
    /// <returns></returns>
    public int Column(params string[] names)
    {
        foreach (var name in names)
        {
            if (_columns.TryGetValue(Key(name), out var index))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Index of the first column whose name contains <paramref name="part"/> (ignoring case, spaces and punctuation), or -1.
    /// </summary>
    /// <param name="part"></param>
    /// <returns></returns>
    public int ColumnContaining(string part)
    {
        var key = Key(part);

        for (var index = 0; index < Header.Count; index++)
        {
            if (Key(Header[index]).Contains(key, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// A trimmed cell, or null when the row is shorter or the column is missing.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    public static string Cell(List<string> row, int column) => column >= 0 && column < row.Count ? row[column].Trim() : null;

    /// <summary>
    /// A count cell (digits with optional grouping commas or a decimal part, which is rounded), or null.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    public static long? Count(List<string> row, int column)
    {
        var text = Cell(row, column)?.Replace(",", string.Empty, StringComparison.Ordinal);

        if (string.IsNullOrEmpty(text))
            return null;

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
            return whole;

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? (long)Math.Round(number) : null;
    }

    /// <summary>
    /// A date cell (<c>yyyy-MM-dd</c>, <c>yyyyMMdd</c> or <c>MM/dd/yyyy</c>), or null.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    public static DateOnly? Date(List<string> row, int column)
        => DateOnly.TryParseExact(Cell(row, column), ["yyyy-MM-dd", "yyyyMMdd", "MM/dd/yyyy", "M/d/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static string Key(string name) => new([.. (name ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    private static List<List<string>> Split(string text, char separator)
    {
        var lines = new List<List<string>>();
        var line = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    cell.Append(c);
                }

                continue;
            }

            if (c == '"' && cell.Length == 0)
            {
                quoted = true;
            }
            else if (c == separator)
            {
                line.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\n')
            {
                line.Add(cell.ToString().TrimEnd('\r'));
                cell.Clear();
                lines.Add(line);
                line = [];
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || line.Count > 0)
        {
            line.Add(cell.ToString().TrimEnd('\r'));
            lines.Add(line);
        }

        return lines;
    }
}
