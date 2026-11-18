using Milvasoft.Components.CQRS.Query;

namespace Storeasy.Application.Features.Mockups.GetMockupExportManifest;

/// <summary>
/// Reads the manifest of a finished export job: image asset ids as sets by store, screenshot class and locale, in slot
/// order (the input of the screenshot upload plan; shape in storeasy-api/docs/studio.md).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMockupExportManifestQuery : IQuery<MockupExportManifest>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Export job id.
    /// </summary>
    public long JobId { get; set; }
}
