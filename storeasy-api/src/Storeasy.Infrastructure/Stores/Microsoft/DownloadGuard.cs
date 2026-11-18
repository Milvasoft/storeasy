using Storeasy.Application.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// SSRF guard of user typed download addresses (MSI/EXE installer URLs): the host must resolve only to public addresses,
/// never to loopback, private, link local, carrier grade NAT or unique local ones.
/// </summary>
public static class DownloadGuard
{
    /// <summary>
    /// Throws a permanent <see cref="StoreCallException"/> unless every address of the host of <paramref name="uri"/> is
    /// public.
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task EnsurePublicAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            throw new StoreCallException("The download host is not public.", null, false);

        IPAddress[] addresses;

        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.IdnHost, cancellationToken);
            }
            catch (SocketException)
            {
                throw new StoreCallException("The download host could not be resolved.", null, false);
            }
        }

        if (addresses.Length == 0 || addresses.Any(a => !IsPublic(a)))
            throw new StoreCallException("The download host is not public.", null, false);
    }

    /// <summary>
    /// Whether <paramref name="address"/> is a public unicast address.
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || (address.GetAddressBytes()[0] & 0xFE) == 0xFC);

        var b = address.GetAddressBytes();

        return !(b[0] == 10
                 || b[0] == 0
                 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                 || (b[0] == 192 && b[1] == 168)
                 || (b[0] == 169 && b[1] == 254)
                 || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                 || b[0] >= 224);
    }
}
