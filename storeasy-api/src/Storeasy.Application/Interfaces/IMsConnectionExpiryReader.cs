using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Finds Microsoft Entra connections whose client secret expires soon. The secret expiry reminders (phase 4 notifications,
/// the rule watcher's <c>microsoft.api-access.client-secret-lifetime</c>) call it across organizations; the connections
/// screen calls it for one organization.
/// </summary>
public interface IMsConnectionExpiryReader
{
    /// <summary>
    /// Connections whose secret expires before <paramref name="until"/> (expired ones included), soonest first; all
    /// organizations when <paramref name="organizationId"/> is null.
    /// </summary>
    /// <param name="until"></param>
    /// <param name="organizationId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<MsExpiringConnectionDto>> GetExpiringAsync(DateTime until, int? organizationId, CancellationToken cancellationToken);
}
