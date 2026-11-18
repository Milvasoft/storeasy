using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.PlanDataSafety;

/// <summary>
/// Plans sending the Google Play Data safety form generated from the app's saved Data safety answers (applications.dataSafety). Play cannot be read back, so the plan always writes and carries no store fingerprint.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanDataSafetyCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
