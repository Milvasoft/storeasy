using System.Buffers.Binary;
using System.Text;
using System.Xml.Linq;

namespace Storeasy.Infrastructure.Intake.Packages;

/// <summary>
/// Reads Apple property lists, binary (<c>bplist00</c>) and XML, into plain values: dictionaries
/// (<see cref="Dictionary{TKey, TValue}"/> of string to object), lists, strings, longs, doubles, booleans, dates and byte arrays.
/// </summary>
public static class PropertyListReader
{
    private const int _maxObjects = 1_000_000;
    private const int _maxDepth = 64;

    /// <summary>
    /// Parses <paramref name="data"/>; throws <see cref="FormatException"/> when it is not a property list.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static object Parse(byte[] data)
    {
        if (data.Length >= 8 && Encoding.ASCII.GetString(data, 0, 6) == "bplist")
            return new BinaryReaderState(data).ReadTop();

        try
        {
            var document = XDocument.Parse(Encoding.UTF8.GetString(data), LoadOptions.None);
            var root = document.Root?.Elements().FirstOrDefault() ?? throw new FormatException("The property list is empty.");

            return ParseXml(root, 0);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new FormatException("The property list is not valid XML.", ex);
        }
    }

    /// <summary>
    /// The top level dictionary of a property list, or an empty one.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static Dictionary<string, object> ParseDictionary(byte[] data) => Parse(data) as Dictionary<string, object> ?? [];

    private static object ParseXml(XElement element, int depth)
    {
        if (depth > _maxDepth)
            throw new FormatException("The property list is nested too deeply.");

        switch (element.Name.LocalName)
        {
            case "dict":
                var dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
                var children = element.Elements().ToList();

                for (var i = 0; i + 1 < children.Count; i += 2)
                {
                    if (children[i].Name.LocalName == "key")
                        dictionary[children[i].Value] = ParseXml(children[i + 1], depth + 1);
                }

                return dictionary;
            case "array":
                return element.Elements().Select(e => ParseXml(e, depth + 1)).ToList();
            case "string":
                return element.Value;
            case "integer":
                return long.TryParse(element.Value, out var number) ? number : 0L;
            case "real":
                return double.TryParse(element.Value, System.Globalization.CultureInfo.InvariantCulture, out var real) ? real : 0d;
            case "true":
                return true;
            case "false":
                return false;
            case "date":
                return DateTime.TryParse(element.Value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var date) ? date : null;
            case "data":
                return Convert.FromBase64String(string.Concat(element.Value.Where(c => !char.IsWhiteSpace(c))));
            default:
                return null;
        }
    }

    private sealed class BinaryReaderState(byte[] data)
    {
        private readonly byte[] _data = data;
        private long[] _offsets;
        private int _refSize;

        internal object ReadTop()
        {
            if (_data.Length < 40)
                throw new FormatException("The binary property list is truncated.");

            var trailer = _data.AsSpan(_data.Length - 32);
            var offsetSize = trailer[6];
            _refSize = trailer[7];
            var count = BinaryPrimitives.ReadInt64BigEndian(trailer[8..]);
            var top = BinaryPrimitives.ReadInt64BigEndian(trailer[16..]);
            var tableOffset = BinaryPrimitives.ReadInt64BigEndian(trailer[24..]);

            if (count <= 0 || count > _maxObjects || offsetSize is < 1 or > 8 || _refSize is < 1 or > 8 || tableOffset < 8 || tableOffset + count * offsetSize > _data.Length - 32 || top >= count)
                throw new FormatException("The binary property list trailer is not valid.");

            _offsets = new long[count];

            for (var i = 0; i < count; i++)
            {
                _offsets[i] = ReadSized(tableOffset + i * offsetSize, offsetSize);
            }

            return ReadObject(top, 0);
        }

        private object ReadObject(long reference, int depth)
        {
            if (depth > _maxDepth || reference < 0 || reference >= _offsets.Length)
                throw new FormatException("The binary property list references an unknown object.");

            var offset = _offsets[reference];

            if (offset < 8 || offset >= _data.Length - 32)
                throw new FormatException("The binary property list has a bad offset.");

            var marker = _data[offset];
            var kind = marker >> 4;
            var info = marker & 0x0F;

            switch (kind)
            {
                case 0x0:
                    return info switch { 0x8 => false, 0x9 => true, _ => null };
                case 0x1:
                    return ReadSized(offset + 1, 1 << info);
                case 0x2:
                    return info == 2 ? BinaryPrimitives.ReadSingleBigEndian(_data.AsSpan((int)offset + 1)) : BinaryPrimitives.ReadDoubleBigEndian(_data.AsSpan((int)offset + 1));
                case 0x3:
                    return new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(BinaryPrimitives.ReadDoubleBigEndian(_data.AsSpan((int)offset + 1)));
                case 0x4:
                    {
                        var (length, start) = Length(offset, info);

                        return _data.AsSpan((int)start, (int)length).ToArray();
                    }
                case 0x5:
                    {
                        var (length, start) = Length(offset, info);

                        return Encoding.ASCII.GetString(_data, (int)start, (int)length);
                    }
                case 0x6:
                    {
                        var (length, start) = Length(offset, info);

                        return Encoding.BigEndianUnicode.GetString(_data, (int)start, (int)length * 2);
                    }
                case 0x8:
                    return ReadSized(offset + 1, info + 1);
                case 0xA:
                case 0xC:
                    {
                        var (length, start) = Length(offset, info);
                        var list = new List<object>((int)Math.Min(length, 1024));

                        for (var i = 0; i < length; i++)
                        {
                            list.Add(ReadObject(ReadSized(start + i * _refSize, _refSize), depth + 1));
                        }

                        return list;
                    }
                case 0xD:
                    {
                        var (length, start) = Length(offset, info);
                        var dictionary = new Dictionary<string, object>(StringComparer.Ordinal);

                        for (var i = 0; i < length; i++)
                        {
                            var key = ReadObject(ReadSized(start + i * _refSize, _refSize), depth + 1) as string;
                            var value = ReadObject(ReadSized(start + (length + i) * _refSize, _refSize), depth + 1);

                            if (key != null)
                                dictionary[key] = value;
                        }

                        return dictionary;
                    }
                default:
                    return null;
            }
        }

        private (long Length, long Start) Length(long offset, int info)
        {
            if (info != 0xF)
                return (info, offset + 1);

            var marker = _data[offset + 1];
            var size = 1 << (marker & 0x0F);
            var length = ReadSized(offset + 2, size);

            if (length < 0 || length > _data.Length)
                throw new FormatException("The binary property list has a bad length.");

            return (length, offset + 2 + size);
        }

        private long ReadSized(long offset, int size)
        {
            if (offset < 0 || offset + size > _data.Length)
                throw new FormatException("The binary property list is truncated.");

            long value = 0;

            for (var i = 0; i < size; i++)
            {
                value = (value << 8) | _data[offset + i];
            }

            return value;
        }
    }
}
