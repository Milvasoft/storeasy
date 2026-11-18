using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.Organizations.GetOrganizationSettings;

/// <summary>
/// Reads the organization's retention and language settings. Member managers (admins) read them; only owners change
/// them; viewers get 403 (design 15.20, 15.22).
/// </summary>
[OrgAuth(OrgPermission.ManageMembers)]
public record GetOrganizationSettingsQuery : IQuery<OrganizationSettingsDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
