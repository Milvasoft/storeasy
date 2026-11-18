using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.DeliveryRules.GetDeliveryRules;

/// <summary>
/// Reads the delivery rules of an app, one per platform (design 13.18).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetDeliveryRulesQuery : IQuery<DeliveryRulesDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }
}
