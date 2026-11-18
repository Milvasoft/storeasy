using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AssetDtos;

namespace Storeasy.Application.Features.Mockups.GetMockupExportZip;

/// <summary>
/// Returns a short lived presigned download URL of the zip of a finished export (every image and the manifest).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMockupExportZipQuery : IQuery<AssetDownloadDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Export job id.
    /// </summary>
    public long JobId { get; set; }
}
