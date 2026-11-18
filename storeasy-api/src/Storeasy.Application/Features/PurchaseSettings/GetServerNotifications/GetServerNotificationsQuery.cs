using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MonetizationDtos;

namespace Storeasy.Application.Features.PurchaseSettings.GetServerNotifications;

/// <summary>
/// Reads where the store sends purchase notifications: App Store Server Notifications addresses live, or the Play assisted step.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetServerNotificationsQuery : IQuery<ServerNotificationsDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
