using System.Net;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// SSRF protection of outgoing webhooks: an address must be absolute HTTPS without credentials, and its host must resolve
/// only to public addresses (no loopback, private, link-local, carrier grade NAT, multicast or reserved ranges, IPv4
/// mapped forms included). Checked when a subscription is saved and again at every connection.
/// </summary>
public interface IWebhookUrlGuard
{
    /// <summary>
    /// Checks <paramref name="url"/>; returns null when it may be called, otherwise the <see cref="Utils.Constants.MessageKey"/> of the problem.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> CheckAsync(string url, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a connection to <paramref name="address"/> must be refused.
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    bool IsBlocked(IPAddress address);
}
