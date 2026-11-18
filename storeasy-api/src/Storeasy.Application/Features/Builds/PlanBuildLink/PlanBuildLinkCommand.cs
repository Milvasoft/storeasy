using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Builds.PlanBuildLink;

/// <summary>
/// Plans attaching a VALID build to the App Store version (and, when given, its export compliance answer) as one change plan.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanBuildLinkCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Version; empty for the editable one.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// storeasy build id (from builds).
    /// </summary>
    public long BuildId { get; set; }

    /// <summary>
    /// Export compliance answer, when the build has none.
    /// </summary>
    public bool? UsesNonExemptEncryption { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
