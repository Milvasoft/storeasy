using System.Buffers.Binary;
using System.Text;

namespace Storeasy.Infrastructure.Intake.Packages;

/// <summary>
/// Reads the values storeasy needs from an Android manifest without aapt2: the protocol buffer form an App Bundle keeps in
/// <c>base/manifest/AndroidManifest.xml</c> (aapt2 <c>XmlNode</c>) and the binary XML form of an APK. Only the
/// <c>manifest</c> element (package, versionCode, versionName) and <c>uses-sdk</c> (minSdkVersion, targetSdkVersion) are read.
/// </summary>
public static class AndroidManifestReader
{
    private const int _maxDepth = 64;

    /// <summary>
    /// Values of a manifest.
    /// </summary>
    /// <param name="Package">Package name.</param>
    /// <param name="VersionCode">versionCode.</param>
    /// <param name="VersionName">versionName.</param>
    /// <param name="MinSdk">minSdkVersion.</param>
    /// <param name="TargetSdk">targetSdkVersion.</param>
    public sealed record ManifestValues(string Package, string VersionCode, string VersionName, string MinSdk, string TargetSdk);

    /// <summary>
    /// Reads an App Bundle manifest (protocol buffer <c>XmlNode</c>).
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static ManifestValues ReadProto(byte[] data)
    {
        var root = ProtoElement.ParseNode(data, 0);

        if (root?.Name != "manifest")
            throw new FormatException("The bundle manifest has no manifest element.");

        var usesSdk = root.Children.FirstOrDefault(c => c.Name == "uses-sdk");

        return new ManifestValues(root.Attribute("package"),
                                  root.Attribute("versionCode"),
                                  root.Attribute("versionName"),
                                  usesSdk?.Attribute("minSdkVersion"),
                                  usesSdk?.Attribute("targetSdkVersion"));
    }

    /// <summary>
    /// Reads an APK manifest (binary XML).
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static ManifestValues ReadBinaryXml(byte[] data)
    {
        if (data.Length < 8 || BinaryPrimitives.ReadUInt16LittleEndian(data) != 0x0003)
            throw new FormatException("The APK manifest is not binary XML.");

        var strings = new List<string>();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        int position = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));

        while (position + 8 <= data.Length)
        {
            var type = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position));
            var headerSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position + 2));
            var size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position + 4));

            if (size < 8 || position + size > data.Length)
                break;

            if (type == 0x0001)
                strings = ReadStringPool(data, position);
            else if (type == 0x0102)
                ReadStartElement(data, position, headerSize, strings, values);

            position += size;
        }

        return new ManifestValues(values.GetValueOrDefault("manifest.package"),
                                  values.GetValueOrDefault("manifest.versionCode"),
                                  values.GetValueOrDefault("manifest.versionName"),
                                  values.GetValueOrDefault("uses-sdk.minSdkVersion"),
                                  values.GetValueOrDefault("uses-sdk.targetSdkVersion"));
    }

    private static List<string> ReadStringPool(byte[] data, int chunk)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(chunk + 8));
        var flags = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(chunk + 16));
        var stringsStart = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(chunk + 20));
        var headerSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(chunk + 2));
        var utf8 = (flags & 0x100) != 0;
        var list = new List<string>(Math.Min(count, 4096));

        for (var i = 0; i < count; i++)
        {
            var offset = chunk + stringsStart + BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(chunk + headerSize + i * 4));

            if (offset < 0 || offset >= data.Length)
            {
                list.Add(null);
                continue;
            }

            if (utf8)
            {
                offset += (data[offset] & 0x80) != 0 ? 2 : 1;
                var length = data[offset] & 0x7F;

                if ((data[offset] & 0x80) != 0)
                {
                    length = (length << 8) | data[offset + 1];
                    offset += 2;
                }
                else
                {
                    offset += 1;
                }

                list.Add(Encoding.UTF8.GetString(data, offset, Math.Min(length, data.Length - offset)));
            }
            else
            {
                int length = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
                offset += 2;

                if ((length & 0x8000) != 0)
                {
                    length = ((length & 0x7FFF) << 16) | BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
                    offset += 2;
                }

                list.Add(Encoding.Unicode.GetString(data, offset, Math.Min(length * 2, data.Length - offset)));
            }
        }

        return list;
    }

    private static void ReadStartElement(byte[] data, int chunk, int headerSize, List<string> strings, Dictionary<string, string> values)
    {
        var extension = chunk + headerSize;
        var name = StringAt(strings, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(extension + 4)));

        if (name is not ("manifest" or "uses-sdk"))
            return;

        var attributeStart = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(extension + 8));
        var attributeSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(extension + 10));
        var attributeCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(extension + 12));

        for (var i = 0; i < attributeCount; i++)
        {
            var attribute = extension + attributeStart + i * attributeSize;

            if (attribute + 20 > data.Length)
                break;

            var attributeName = StringAt(strings, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(attribute + 4)));
            var rawValue = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(attribute + 8));
            var dataType = data[attribute + 15];
            var typedValue = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(attribute + 16));

            var value = dataType switch
            {
                0x03 => StringAt(strings, typedValue),
                0x10 or 0x11 => typedValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => rawValue >= 0 ? StringAt(strings, rawValue) : null,
            };

            if (attributeName != null && value != null)
                values[$"{name}.{attributeName}"] = value;
        }
    }

    private static string StringAt(List<string> strings, int index) => index >= 0 && index < strings.Count ? strings[index] : null;

    /// <summary>
    /// A protocol buffer <c>XmlElement</c>, with only what the reader needs.
    /// </summary>
    private sealed class ProtoElement
    {
        internal string Name { get; private set; }

        internal Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);

        internal List<ProtoElement> Children { get; } = [];

        internal string Attribute(string name) => Attributes.GetValueOrDefault(name);

        /// <summary>
        /// Parses an <c>XmlNode</c> (field 1 is the element).
        /// </summary>
        internal static ProtoElement ParseNode(ReadOnlySpan<byte> data, int depth)
        {
            if (depth > _maxDepth)
                throw new FormatException("The bundle manifest is nested too deeply.");

            var reader = new ProtoReader(data);

            while (reader.Next(out var field, out var wire))
            {
                if (field == 1 && wire == 2)
                    return ParseElement(reader.Bytes(), depth);

                reader.Skip(wire);
            }

            return null;
        }

        private static ProtoElement ParseElement(ReadOnlySpan<byte> data, int depth)
        {
            var element = new ProtoElement();
            var reader = new ProtoReader(data);

            while (reader.Next(out var field, out var wire))
            {
                switch (field)
                {
                    case 3 when wire == 2:
                        element.Name = Encoding.UTF8.GetString(reader.Bytes());
                        break;
                    case 4 when wire == 2:
                        var (name, value) = ParseAttribute(reader.Bytes());

                        if (name != null)
                            element.Attributes[name] = value;

                        break;
                    case 5 when wire == 2:
                        var child = ParseNode(reader.Bytes(), depth + 1);

                        if (child != null)
                            element.Children.Add(child);

                        break;
                    default:
                        reader.Skip(wire);
                        break;
                }
            }

            return element;
        }

        private static (string Name, string Value) ParseAttribute(ReadOnlySpan<byte> data)
        {
            string name = null;
            string value = null;
            string compiled = null;
            var reader = new ProtoReader(data);

            while (reader.Next(out var field, out var wire))
            {
                switch (field)
                {
                    case 2 when wire == 2:
                        name = Encoding.UTF8.GetString(reader.Bytes());
                        break;
                    case 3 when wire == 2:
                        value = Encoding.UTF8.GetString(reader.Bytes());
                        break;
                    case 6 when wire == 2:
                        compiled = CompiledValue(reader.Bytes());
                        break;
                    default:
                        reader.Skip(wire);
                        break;
                }
            }

            return (name, string.IsNullOrEmpty(value) ? compiled : value);
        }

        /// <summary>
        /// Value of an <c>Item</c>: a string (field 2 then field 1) or a primitive integer (field 7, then 6 or 7).
        /// </summary>
        private static string CompiledValue(ReadOnlySpan<byte> item)
        {
            var reader = new ProtoReader(item);

            while (reader.Next(out var field, out var wire))
            {
                if (wire != 2)
                {
                    reader.Skip(wire);
                    continue;
                }

                var inner = reader.Bytes();
                var innerReader = new ProtoReader(inner);

                while (innerReader.Next(out var innerField, out var innerWire))
                {
                    if (field == 2 && innerField == 1 && innerWire == 2)
                        return Encoding.UTF8.GetString(innerReader.Bytes());

                    if (field == 7 && innerField is 6 or 7 && innerWire == 0)
                        return ((int)innerReader.Varint()).ToString(System.Globalization.CultureInfo.InvariantCulture);

                    innerReader.Skip(innerWire);
                }
            }

            return null;
        }
    }

    /// <summary>
    /// A minimal protocol buffer wire reader.
    /// </summary>
    private ref struct ProtoReader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> _data = data;
        private int _position;

        internal bool Next(out int field, out int wire)
        {
            field = 0;
            wire = 0;

            if (_position >= _data.Length)
                return false;

            var key = Varint();

            field = (int)(key >> 3);
            wire = (int)(key & 7);

            return true;
        }

        internal ulong Varint()
        {
            ulong value = 0;

            for (var shift = 0; shift < 64; shift += 7)
            {
                if (_position >= _data.Length)
                    throw new FormatException("The bundle manifest is truncated.");

                var b = _data[_position++];

                value |= (ulong)(b & 0x7F) << shift;

                if ((b & 0x80) == 0)
                    return value;
            }

            throw new FormatException("The bundle manifest has a bad varint.");
        }

        internal ReadOnlySpan<byte> Bytes()
        {
            var length = (int)Varint();

            if (length < 0 || _position + length > _data.Length)
                throw new FormatException("The bundle manifest is truncated.");

            var slice = _data.Slice(_position, length);

            _position += length;

            return slice;
        }

        internal void Skip(int wire)
        {
            switch (wire)
            {
                case 0:
                    Varint();
                    break;
                case 1:
                    _position += 8;
                    break;
                case 2:
                    Bytes();
                    break;
                case 5:
                    _position += 4;
                    break;
                default:
                    throw new FormatException("The bundle manifest has an unknown wire type.");
            }
        }
    }
}
