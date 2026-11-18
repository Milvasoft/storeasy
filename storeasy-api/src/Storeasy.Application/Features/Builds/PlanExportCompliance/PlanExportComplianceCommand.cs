using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Builds.PlanExportCompliance;

/// <summary>
/// Plans the export compliance answer (usesNonExemptEncryption) of the build attached to the App Store version (PUT builds/export-compliance).
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanExportComplianceCommand : ICommand<ChangePlanDto>, IOrganizationRequest
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
    /// Whether the build uses non exempt encryption.
    /// </summary>
    public bool UsesNonExemptEncryption { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
