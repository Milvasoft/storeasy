using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetRegistryConnectionList;

/// <summary>
/// The credentials of the package registries module (GitHub token, registry API keys), without their secrets.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetRegistryConnectionListQuery : IQuery<List<RegistryConnectionDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
