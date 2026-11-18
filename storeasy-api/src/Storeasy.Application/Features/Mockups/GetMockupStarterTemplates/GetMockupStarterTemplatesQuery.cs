using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MockupDtos;

namespace Storeasy.Application.Features.Mockups.GetMockupStarterTemplates;

/// <summary>
/// Lists the built in starter designs a new template can start from.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMockupStarterTemplatesQuery : IQuery<List<MockupStarterTemplateDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
