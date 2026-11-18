using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MockupDtos;

namespace Storeasy.Application.Features.Mockups.GetMockupTemplateDetail;

/// <summary>
/// Reads one mockup template with its design, slots and captions.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMockupTemplateDetailQuery : IQuery<MockupTemplateDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Template id.
    /// </summary>
    public int TemplateId { get; set; }
}
