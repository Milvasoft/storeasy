using Storeasy.Domain.Enums;
using System.Net;
using System.Net.Sockets;

namespace Storeasy.Domain.Intake;

/// <summary>
/// The pure rules of package intake (tools/CONTRACT.md 3, design 13): accepted files, part layout, delivery targets, CI
/// exit codes, claim patterns and download host checks. No database, no network.
/// </summary>
public static class IntakeRules
{
    /// <summary>
    /// Smallest part (S3 minimum).
    /// </summary>
    public const long MinPartSize = 5L * 1024 * 1024;

    /// <summary>
    /// Largest part.
    /// </summary>
    public const long MaxPartSize = 64L * 1024 * 1024;

    /// <summary>
    /// Preferred part size.
    /// </summary>
    public const long DefaultPartSize = 8L * 1024 * 1024;

    /// <summary>
    /// Most parts of an S3 multipart upload.
    /// </summary>
    public const int MaxParts = 10000;

    /// <summary>
    /// Longest release notes text.
    /// </summary>
    public const int NotesMaxLength = 4000;

    /// <summary>
    /// CI exit code: delivered, duplicate or held.
    /// </summary>
    public const int ExitOk = 0;

    /// <summary>
    /// CI exit code: the package was rejected.
    /// </summary>
    public const int ExitRejected = 2;

    /// <summary>
    /// CI exit code: identity failure.
    /// </summary>
    public const int ExitIdentity = 3;

    /// <summary>
    /// CI exit code: the store refused or failed.
    /// </summary>
    public const int ExitStoreError = 4;

    /// <summary>
    /// Play tracks storeasy never releases to automatically.
    /// </summary>
    public static readonly IReadOnlySet<string> ProductionTracks = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "production" };

    private static readonly Dictionary<string, (ArtifactPlatform Platform, PackageKind Kind)> _extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".ipa"] = (ArtifactPlatform.Ios, PackageKind.Ipa),
        [".aab"] = (ArtifactPlatform.Android, PackageKind.Aab),
        [".apk"] = (ArtifactPlatform.Android, PackageKind.Apk),
        [".msix"] = (ArtifactPlatform.Windows, PackageKind.Msix),
        [".msixbundle"] = (ArtifactPlatform.Windows, PackageKind.Msix),
        [".appx"] = (ArtifactPlatform.Windows, PackageKind.Msix),
        [".appxbundle"] = (ArtifactPlatform.Windows, PackageKind.Msix),
        [".msixupload"] = (ArtifactPlatform.Windows, PackageKind.Msix),
        [".appxupload"] = (ArtifactPlatform.Windows, PackageKind.Msix),
        [".xpi"] = (ArtifactPlatform.Extension, PackageKind.Xpi),
    };

    /// <summary>
    /// Platform and format of <paramref name="fileName"/> by its extension; null for an unsupported file.
    /// </summary>
    public static (ArtifactPlatform Platform, PackageKind Kind)? Classify(string fileName)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);

        return _extensions.TryGetValue(extension, out var value) ? value : null;
    }

    /// <summary>
    /// Store a platform's packages go to.
    /// </summary>
    public static StoreKind StoreOf(ArtifactPlatform platform) => platform switch
    {
        ArtifactPlatform.Ios => StoreKind.Apple,
        ArtifactPlatform.Android => StoreKind.Google,
        _ => StoreKind.Microsoft,
    };

    /// <summary>
    /// Store a package goes to: a Firefox extension (.xpi) goes to Firefox Add-ons, every other file to the store of its
    /// platform.
    /// </summary>
    public static StoreKind StoreOf(ArtifactPlatform platform, string fileName)
        => Classify(fileName)?.Kind == PackageKind.Xpi ? StoreKind.Firefox : StoreOf(platform);

    /// <summary>
    /// Part size for a file of <paramref name="size"/> bytes: <see cref="DefaultPartSize"/>, grown in whole MiB until the
    /// file fits in <see cref="MaxParts"/> parts, never above <see cref="MaxPartSize"/>.
    /// </summary>
    public static long PartSizeFor(long size)
    {
        var partSize = DefaultPartSize;

        while (PartCount(size, partSize) > MaxParts && partSize < MaxPartSize)
        {
            partSize += 1024 * 1024;
        }

        return Math.Clamp(partSize, MinPartSize, MaxPartSize);
    }

    /// <summary>
    /// Number of parts of a file (ceil(size / partSize)).
    /// </summary>
    public static int PartCount(long size, long partSize) => size <= 0 || partSize <= 0 ? 0 : (int)((size + partSize - 1) / partSize);

    /// <summary>
    /// Whether a delivery rule may send packages to <paramref name="target"/> automatically: test channels only (no
    /// production track, no external TestFlight group, no review submission).
    /// </summary>
    public static bool IsTargetAllowed(ArtifactPlatform platform, DeliveryTarget target, string targetId)
    {
        return (platform, target) switch
        {
            (_, DeliveryTarget.None) => true,
            (ArtifactPlatform.Ios, DeliveryTarget.TestFlightGroup) => !string.IsNullOrWhiteSpace(targetId),
            (ArtifactPlatform.Android, DeliveryTarget.PlayTrack) => !string.IsNullOrWhiteSpace(targetId) && !ProductionTracks.Contains(targetId.Trim()),
            (ArtifactPlatform.Windows, DeliveryTarget.MsFlight) => !string.IsNullOrWhiteSpace(targetId),
            _ => false,
        };
    }

    /// <summary>
    /// Whether a session in <paramref name="status"/> is final for the CI.
    /// </summary>
    public static bool IsFinal(ArtifactSessionStatus status) => status is ArtifactSessionStatus.Delivered
                                                                       or ArtifactSessionStatus.Rejected
                                                                       or ArtifactSessionStatus.Duplicate
                                                                       or ArtifactSessionStatus.Held
                                                                       or ArtifactSessionStatus.Failed;

    /// <summary>
    /// CI exit code of a final status.
    /// </summary>
    public static int ExitCodeOf(ArtifactSessionStatus status) => status switch
    {
        ArtifactSessionStatus.Rejected => ExitRejected,
        ArtifactSessionStatus.Failed => ExitStoreError,
        _ => ExitOk,
    };

    /// <summary>
    /// Whether <paramref name="sha256"/> is 64 lower case hex characters.
    /// </summary>
    public static bool IsSha256(string sha256) => sha256 is { Length: 64 } && sha256.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    /// <summary>
    /// Glob match of a claim value against a rule pattern: <c>*</c> matches any run of characters (including <c>/</c>),
    /// everything else literally and case sensitively. An empty pattern matches anything.
    /// </summary>
    public static bool GlobMatches(string pattern, string value)
    {
        if (string.IsNullOrEmpty(pattern))
            return true;

        if (value == null)
            return false;

        var parts = pattern.Split('*');

        if (parts.Length == 1)
            return string.Equals(pattern, value, StringComparison.Ordinal);

        if (!value.StartsWith(parts[0], StringComparison.Ordinal))
            return false;

        var position = parts[0].Length;

        for (var i = 1; i < parts.Length - 1; i++)
        {
            var index = value.IndexOf(parts[i], position, StringComparison.Ordinal);

            if (index < 0)
                return false;

            position = index + parts[i].Length;
        }

        var last = parts[^1];

        return value.Length - position >= last.Length && value.EndsWith(last, StringComparison.Ordinal);
    }

    /// <summary>
    /// Branch name of a git ref (<c>refs/heads/main</c> to <c>main</c>); other refs stay as they are.
    /// </summary>
    public static string BranchOf(string gitRef) => gitRef != null && gitRef.StartsWith("refs/heads/", StringComparison.Ordinal) ? gitRef["refs/heads/".Length..] : gitRef;

    /// <summary>
    /// Whether <paramref name="host"/> is one of <paramref name="allowedDomains"/> (exact, or a subdomain of a
    /// <c>*.domain</c> entry).
    /// </summary>
    public static bool IsHostAllowed(string host, IEnumerable<string> allowedDomains)
    {
        if (string.IsNullOrWhiteSpace(host) || allowedDomains == null)
            return false;

        host = host.Trim().TrimEnd('.').ToLowerInvariant();

        foreach (var raw in allowedDomains)
        {
            var domain = raw?.Trim().TrimEnd('.').ToLowerInvariant();

            if (string.IsNullOrEmpty(domain))
                continue;

            if (domain.StartsWith("*.", StringComparison.Ordinal))
            {
                if (host.EndsWith(domain[1..], StringComparison.Ordinal) && host.Length > domain.Length - 1)
                    return true;
            }
            else if (host == domain)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an allowed domain entry is well formed (a host name or <c>*.host</c>, no scheme, path, port or IP).
    /// </summary>
    public static bool IsValidDomainEntry(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry) || entry.Length > 253)
            return false;

        var host = entry.StartsWith("*.", StringComparison.Ordinal) ? entry[2..] : entry;

        return Uri.CheckHostName(host) == UriHostNameType.Dns && host.Contains('.') && !IPAddress.TryParse(host, out _);
    }

    /// <summary>
    /// Whether an address must never be downloaded from: loopback, private, link local, unique local, multicast,
    /// unspecified, carrier grade NAT and the IPv4 mapped forms of those.
    /// </summary>
    public static bool IsBlockedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.Broadcast))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();

            return b[0] == 10
                   || b[0] == 0
                   || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                   || (b[0] == 192 && b[1] == 168)
                   || (b[0] == 169 && b[1] == 254)
                   || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                   || b[0] >= 224;
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal;
    }
}
