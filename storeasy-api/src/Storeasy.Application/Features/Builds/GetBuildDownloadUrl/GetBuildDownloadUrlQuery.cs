using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AppDtos;

namespace Storeasy.Application.Features.Builds.GetBuildDownloadUrl;

/// <summary>
/// Returns a short lived presigned download URL of the package (IPA, AAB, APK, MSIX) storeasy keeps for a build. Only a
/// build that came through the package intake and whose file was not deleted yet has one. Every call is audited.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetBuildDownloadUrlQuery : IQuery<BuildDownloadDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Build id (BuildDto.Id).
    /// </summary>
    public long BuildId { get; set; }
}
