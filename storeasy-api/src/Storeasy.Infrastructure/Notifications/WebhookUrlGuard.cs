using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;
using System.Net;
using System.Net.Sockets;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// Default <see cref="IWebhookUrlGuard"/>. Blocked: 0.0.0.0/8, 10/8, 100.64/10, 127/8, 169.254/16, 172.16/12, 192.0.0/24,
/// 192.0.2/24, 192.168/16, 198.18/15, 198.51.100/24, 203.0.113/24, 224/4 and above; IPv6 unspecified, loopback,
/// link-local, site-local, unique local, multicast, documentation, and the IPv4 ranges inside mapped, NAT64 and 6to4
/// addresses.
/// </summary>
/// <param name="hostResolver"></param>
/// <param name="options"></param>
public sealed class WebhookUrlGuard(IHostResolver hostResolver, OrgWebhookOptions options) : IWebhookUrlGuard
{
    private static readonly string[] _blockedHostSuffixes = [".localhost", ".local", ".internal", ".lan", ".home.arpa"];

    private readonly IHostResolver _hostResolver = hostResolver;
    private readonly OrgWebhookOptions _options = options;

    /// <inheritdoc/>
    public async Task<string> CheckAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !(uri.Scheme == Uri.UriSchemeHttps || (_options.AllowHttp && uri.Scheme == Uri.UriSchemeHttp))
            || !string.IsNullOrEmpty(uri.UserInfo)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return MessageKey.OrgWebhookUrlInvalid;
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();

        if (host == "localhost" || _blockedHostSuffixes.Any(host.EndsWith))
            return MessageKey.OrgWebhookUrlBlocked;

        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal))
            return IsBlocked(literal) ? MessageKey.OrgWebhookUrlBlocked : null;

        IPAddress[] addresses;

        try
        {
            addresses = await _hostResolver.ResolveAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            addresses = [];
        }

        if (addresses.Length == 0)
            return MessageKey.OrgWebhookUrlInvalid;

        return addresses.Any(IsBlocked) ? MessageKey.OrgWebhookUrlBlocked : null;
    }

    /// <inheritdoc/>
    public bool IsBlocked(IPAddress address)
    {
        if (address == null)
            return true;

        if (address.IsIPv4MappedToIPv6)
            return IsBlockedV4(address.MapToIPv4().GetAddressBytes());

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return IsBlockedV4(address.GetAddressBytes());

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return true;

        var bytes = address.GetAddressBytes();

        if (IPAddress.IPv6Any.Equals(address) || IPAddress.IPv6Loopback.Equals(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return true;

        // Unique local fc00::/7, documentation 2001:db8::/32.
        if ((bytes[0] & 0xFE) == 0xFC || (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8))
            return true;

        // NAT64 64:ff9b::/96 and IPv4 compatible ::/96 carry an IPv4 address in the last 4 bytes.
        var nat64 = bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xFF && bytes[3] == 0x9B && bytes.Skip(4).Take(8).All(b => b == 0);
        var compatible = bytes.Take(12).All(b => b == 0);

        if (nat64 || compatible)
            return IsBlockedV4(bytes[12..]);

        // 6to4 2002::/16 carries an IPv4 address in bytes 2 to 5.
        if (bytes[0] == 0x20 && bytes[1] == 0x02)
            return IsBlockedV4(bytes[2..6]);

        return false;
    }

    private static bool IsBlockedV4(byte[] b) => b[0] switch
    {
        0 or 10 or 127 => true,
        100 => b[1] >= 64 && b[1] <= 127,
        169 => b[1] == 254,
        172 => b[1] >= 16 && b[1] <= 31,
        192 => (b[1] == 0 && (b[2] == 0 || b[2] == 2)) || b[1] == 168,
        198 => b[1] == 18 || b[1] == 19 || (b[1] == 51 && b[2] == 100),
        203 => b[1] == 0 && b[2] == 113,
        >= 224 => true,
        _ => false,
    };
}
