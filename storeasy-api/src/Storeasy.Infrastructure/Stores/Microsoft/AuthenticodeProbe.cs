using Storeasy.Domain.Enums;
using System.Buffers.Binary;
using System.Text;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// Structural Authenticode check of an installer, read once as a stream (nothing is stored):
/// <list type="bullet">
/// <item>EXE (PE): the security data directory must point at a <c>WIN_CERTIFICATE</c> of revision 2.0 and type
/// PKCS#7 signed data.</item>
/// <item>MSI (compound file): the <c>\u0005DigitalSignature</c> stream must exist.</item>
/// </list>
/// This proves a signature is present, not that it is valid or trusted; Microsoft validates the chain at certification.
/// </summary>
public static class AuthenticodeProbe
{
    private const int _chunkSize = 81920;
    private const ushort _certificateRevision = 0x0200;
    private const ushort _certificateTypePkcs = 0x0002;

    private static readonly byte[] _compoundFileSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] _msiSignatureStream = Encoding.Unicode.GetBytes("\u0005DigitalSignature");

    /// <summary>
    /// Reads <paramref name="stream"/> (at most <paramref name="maxBytes"/>) and says whether it carries a signature, with
    /// a short note.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="maxBytes"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<(MsSignatureStatus Status, string Note)> InspectAsync(Stream stream, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[_chunkSize];
        var head = new MemoryStream();
        long read = 0;
        int count;

        // The PE and compound file headers are in the first 4 KB.
        while (head.Length < 4096 && (count = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, 4096 - head.Length)), cancellationToken)) > 0)
        {
            head.Write(buffer, 0, count);
            read += count;
        }

        var header = head.ToArray();

        if (header.Length >= 2 && header[0] == (byte)'M' && header[1] == (byte)'Z')
            return await InspectPeAsync(stream, header, read, maxBytes, buffer, cancellationToken);

        if (header.AsSpan().StartsWith(_compoundFileSignature))
            return await InspectMsiAsync(stream, header, read, maxBytes, buffer, cancellationToken);

        return (MsSignatureStatus.Unsigned, "Not a PE (EXE) or compound (MSI) file.");
    }

    private static async Task<(MsSignatureStatus, string)> InspectPeAsync(Stream stream, byte[] header, long read, long maxBytes, byte[] buffer, CancellationToken cancellationToken)
    {
        if (header.Length < 0x40)
            return (MsSignatureStatus.Unsigned, "Truncated PE header.");

        var pe = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0x3C));

        if (pe < 0 || pe + 24 + 2 > header.Length || !header.AsSpan(pe, 4).SequenceEqual("PE\0\0"u8))
            return (MsSignatureStatus.Unsigned, "Invalid PE header.");

        var optional = pe + 24;
        var magic = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(optional));
        var directories = optional + (magic == 0x20B ? 112 : 96);
        var security = directories + (4 * 8);

        if (security + 8 > header.Length)
            return (MsSignatureStatus.Unsigned, "Invalid PE optional header.");

        var offset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(security));
        var size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(security + 4));

        if (offset == 0 || size < 8)
            return (MsSignatureStatus.Unsigned, "The PE file has no certificate table.");

        if (offset + 8L > maxBytes)
            return (MsSignatureStatus.Unchecked, "The certificate table is beyond the download limit.");

        var certificate = new byte[8];
        var filled = 0;

        if (offset < read)
        {
            filled = (int)Math.Min(8, read - offset);
            Array.Copy(header, offset, certificate, 0, filled);
        }

        var position = read;

        while (filled < 8)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken);

            if (count == 0)
                return (MsSignatureStatus.Unsigned, "The certificate table points past the end of the file.");

            var start = position;
            position += count;

            if (position <= offset + filled)
                continue;

            var from = (int)Math.Max(0, offset + filled - start);
            var take = Math.Min(8 - filled, count - from);

            Array.Copy(buffer, from, certificate, filled, take);
            filled += take;
        }

        var revision = BinaryPrimitives.ReadUInt16LittleEndian(certificate.AsSpan(4));
        var type = BinaryPrimitives.ReadUInt16LittleEndian(certificate.AsSpan(6));

        return revision == _certificateRevision && type == _certificateTypePkcs
            ? (MsSignatureStatus.Signed, $"Authenticode PKCS#7 certificate of {size} bytes.")
            : (MsSignatureStatus.Unsigned, "The certificate table is not an Authenticode signature.");
    }

    private static async Task<(MsSignatureStatus, string)> InspectMsiAsync(Stream stream, byte[] header, long read, long maxBytes, byte[] buffer, CancellationToken cancellationToken)
    {
        var window = header;

        while (true)
        {
            if (window.AsSpan().IndexOf(_msiSignatureStream) >= 0)
                return (MsSignatureStatus.Signed, "The MSI carries a DigitalSignature stream.");

            if (read >= maxBytes)
                return (MsSignatureStatus.Unchecked, "The MSI is larger than the download limit.");

            var count = await stream.ReadAsync(buffer, cancellationToken);

            if (count == 0)
                return (MsSignatureStatus.Unsigned, "The MSI has no DigitalSignature stream.");

            read += count;

            // Keep the tail of the previous window so a name split across two reads is still found.
            var carry = Math.Min(window.Length, _msiSignatureStream.Length - 1);
            var next = new byte[carry + count];

            Array.Copy(window, window.Length - carry, next, 0, carry);
            Array.Copy(buffer, 0, next, carry, count);

            window = next;
        }
    }
}
