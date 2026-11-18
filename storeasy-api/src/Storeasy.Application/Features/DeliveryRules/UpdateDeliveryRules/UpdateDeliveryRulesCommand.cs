using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.DeliveryRules.UpdateDeliveryRules;

/// <summary>
/// Replaces the delivery rules of an app (design 13.18). Targets are test channels only: a production track, an external
/// TestFlight group or a review submission is refused (400 DeliveryTargetNotAllowed).
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record UpdateDeliveryRulesCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Rules, at most one per platform. A platform left out has no rule.
    /// </summary>
    public List<DeliveryRuleDto> Rules { get; set; } = [];
}
