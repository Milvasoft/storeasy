using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.PlanIapChanges;

/// <summary>
/// Plans In-App Purchase changes (create, fields, localizations, price schedule, availability, review screenshot; Play one-time products). A price change of a product on sale needs ReleaseProduction.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanIapChangesCommand : ICommand<ChangePlanDto>, IOrganizationRequest, IMonetizationPlanRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Changes in the order they are written.
    /// </summary>
    public List<MonetizationChangeDto> Changes { get; set; } = [];

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Typed confirmation, when <see cref="DryRun"/> is false and the plan needs one.
    /// </summary>
    public string Confirm { get; set; }
}
