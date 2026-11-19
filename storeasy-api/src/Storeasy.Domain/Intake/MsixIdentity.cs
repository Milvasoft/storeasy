using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Domain.Intake;

/// <summary>
/// The outcome of <see cref="MsixIdentity.Match"/>.
/// </summary>
/// <param name="Matches">True when the package is the app's, false when it is not, null when the record has no package
/// identity yet (not synced) so nothing could be compared.</param>
/// <param name="Expected">The stored package identity the package was compared with, if any.</param>
/// <param name="Reason">Why it does not match, or why it could not be compared.</param>
public sealed record MsixIdentityMatch(bool? Matches, string Expected, string Reason);

/// <summary>
/// Package identity rules of MSIX packages. A Microsoft Store record keeps the package family name of its app
/// (<c>Name_PublisherId</c>, from Partner Center) in <c>StoreApp.BundleId</c>; the package's <c>Identity Name</c> must be
/// the name part, and the publisher id part is derived from the package's <c>Identity Publisher</c>. The Store id
/// (<c>StoreApp.ExternalId</c>, e.g. 9NGK4T6XHF34) is never a package identity, so it is never compared. No database, no
/// network.
/// </summary>
public static class MsixIdentity
{
    /// <summary>
    /// Length of a publisher id (the part of a package family name after the underscore).
    /// </summary>
    public const int PublisherIdLength = 13;

    private const string _alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    /// <summary>
    /// The publisher id Windows derives from a publisher distinguished name: the first 8 bytes of the SHA-256 of its
    /// UTF-16LE bytes, padded with one zero bit to 65 bits and written as 13 Crockford base32 characters (lower case).
    /// Returns null for an empty publisher.
    /// </summary>
    /// <param name="publisher">Publisher, e.g. <c>CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US</c>.</param>
    /// <returns></returns>
    public static string PublisherId(string publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher))
            return null;

        var hash = SHA256.HashData(Encoding.Unicode.GetBytes(publisher));
        var value = (UInt128)System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(hash.AsSpan(0, 8)) << 1;
        var id = new char[PublisherIdLength];

        for (var i = 0; i < PublisherIdLength; i++)
            id[i] = _alphabet[(int)((value >> (60 - (i * 5))) & 31)];

        return new string(id);
    }

    /// <summary>
    /// The package family name of an identity name and a publisher (<c>Name_PublisherId</c>). Returns null when the name
    /// is empty, and the bare name when the publisher is unknown.
    /// </summary>
    /// <param name="identityName"></param>
    /// <param name="publisher"></param>
    /// <returns></returns>
    public static string FamilyName(string identityName, string publisher)
    {
        if (string.IsNullOrWhiteSpace(identityName))
            return null;

        var publisherId = PublisherId(publisher);

        return publisherId == null ? identityName.Trim() : $"{identityName.Trim()}_{publisherId}";
    }

    /// <summary>
    /// Whether a package belongs to a Microsoft Store record.
    /// <list type="bullet">
    /// <item>No stored identity (empty, or only the Store id): null, the record is not synced yet.</item>
    /// <item>A package family name (<c>Name_PublisherId</c>): the package name must equal the name part, and when the
    /// package has a publisher its publisher id must equal the stored one.</item>
    /// <item>A bare identity name: the package name must equal it.</item>
    /// </list>
    /// Names are compared without case, like Windows does.
    /// </summary>
    /// <param name="packageName">The package's <c>Identity Name</c>.</param>
    /// <param name="packagePublisher">The package's <c>Identity Publisher</c>, if read.</param>
    /// <param name="storedIdentity">The record's package family name or identity name (<c>StoreApp.BundleId</c>).</param>
    /// <param name="storeId">The record's Store id (<c>StoreApp.ExternalId</c>).</param>
    /// <returns></returns>
    public static MsixIdentityMatch Match(string packageName, string packagePublisher, string storedIdentity, string storeId)
    {
        var stored = storedIdentity?.Trim();

        if (string.IsNullOrEmpty(stored) || string.Equals(stored, storeId?.Trim(), StringComparison.OrdinalIgnoreCase))
            return new MsixIdentityMatch(null, null, "The Microsoft Store record has no package identity yet: sync the Microsoft connection to verify the package name.");

        var separator = stored.IndexOf('_');
        var storedName = separator < 0 ? stored : stored[..separator];
        var storedPublisherId = separator < 0 ? null : stored[(stored.LastIndexOf('_') + 1)..];

        if (string.IsNullOrWhiteSpace(packageName) || !string.Equals(packageName.Trim(), storedName, StringComparison.OrdinalIgnoreCase))
            return new MsixIdentityMatch(false, stored, $"Package {packageName} does not match the app ({stored}).");

        var packagePublisherId = PublisherId(packagePublisher);

        if (packagePublisherId != null && storedPublisherId?.Length == PublisherIdLength && !string.Equals(packagePublisherId, storedPublisherId, StringComparison.OrdinalIgnoreCase))
            return new MsixIdentityMatch(false, stored, $"Package {packageName} has publisher {packagePublisher} ({packageName}_{packagePublisherId}), not the app's ({stored}).");

        return new MsixIdentityMatch(true, stored, null);
    }
}
