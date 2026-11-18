using System.Buffers.Binary;

namespace Storeasy.Domain.Assets;

/// <summary>
/// Format, size and alpha of a PNG or JPEG read from its header (the stores reject screenshots with an alpha channel).
/// A PNG has alpha when its color type carries one (4, 6) or it has a <c>tRNS</c> chunk before the image data.
/// </summary>
/// <param name="Format">png or jpeg.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="HasAlpha">Whether the image has an alpha channel or transparency.</param>
public sealed record ImageHeader(string Format, int Width, int Height, bool HasAlpha)
{
    private static readonly byte[] _pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Reads the header of <paramref name="data"/>; null when it is neither a PNG nor a JPEG with a frame header.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static ImageHeader Read(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 26 && data[..8].SequenceEqual(_pngSignature))
            return ReadPng(data);

        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8)
            return ReadJpeg(data);

        return null;
    }

    private static ImageHeader ReadPng(ReadOnlySpan<byte> data)
    {
        var width = (int)BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
        var height = (int)BinaryPrimitives.ReadUInt32BigEndian(data[20..]);
        var colorType = data[25];
        var hasAlpha = colorType is 4 or 6;
        var offset = 8;

        while (!hasAlpha && offset + 8 <= data.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            var type = System.Text.Encoding.ASCII.GetString(data.Slice(offset + 4, 4));

            if (type == "tRNS")
                hasAlpha = true;

            if (type is "IDAT" or "IEND" || length < 0)
                break;

            offset += 12 + length;
        }

        return new ImageHeader("png", width, height, hasAlpha);
    }

    private static ImageHeader ReadJpeg(ReadOnlySpan<byte> data)
    {
        var offset = 2;

        while (offset + 9 < data.Length)
        {
            if (data[offset] != 0xFF)
            {
                offset++;
                continue;
            }

            var marker = data[offset + 1];

            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7) or 0xFF)
            {
                offset += marker == 0xFF ? 1 : 2;
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 2)..]);
            var isFrame = marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);

            if (isFrame)
                return new ImageHeader("jpeg", BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 5)..]), false);

            offset += 2 + length;
        }

        return null;
    }
}
