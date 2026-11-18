using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MockupDtos;

namespace Storeasy.Application.Features.Mockups.GetMockupFrames;

/// <summary>
/// Lists storeasy's device frames with their defaults and the screenshot classes (sizes from the rules catalog) drawn with
/// each.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMockupFramesQuery : IQuery<List<MockupFrameDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
