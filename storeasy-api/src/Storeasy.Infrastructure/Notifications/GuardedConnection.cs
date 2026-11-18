using Storeasy.Application.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// The connect step of the outgoing webhook client: the host is resolved again at connection time and a blocked address
/// is refused there, so a name that resolves to a public address when the subscription is saved and to a private one
/// later (DNS rebinding) is still not reached.
/// </summary>
public static class GuardedConnection
{
    /// <summary>
    /// A handler that connects only to addresses <paramref name="guard"/> allows and never follows redirects.
    /// </summary>
    /// <param name="guard"></param>
    /// <param name="hostResolver"></param>
    /// <returns></returns>
    public static SocketsHttpHandler CreateHandler(IWebhookUrlGuard guard, IHostResolver hostResolver) => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectCallback = async (context, cancellationToken) =>
        {
            var host = context.DnsEndPoint.Host;
            var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await hostResolver.ResolveAsync(host, cancellationToken);

            if (addresses.Length == 0 || addresses.Any(guard.IsBlocked))
                throw new HttpRequestException($"Connections to {host} are not allowed.");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };
}
