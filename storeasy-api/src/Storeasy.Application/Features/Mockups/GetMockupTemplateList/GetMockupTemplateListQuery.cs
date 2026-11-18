using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.MockupDtos;

namespace Storeasy.Application.Features.Mockups.GetMockupTemplateList;

/// <summary>
/// Pages through the organization's mockup templates, optionally of one app.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMockupTemplateListQuery : ListRequest, IListRequestQuery<MockupTemplateListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only the templates of this app; null for every app.
    /// </summary>
    public int? AppId { get; set; }
}
