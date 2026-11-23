using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetPackageGroupSuggestions;

/// <summary>
/// Suggested groups: tracked packages without a group that come from one source repository (for example every
/// Milvasoft.* package of Milvasoft/Milvasoft).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPackageGroupSuggestionsQuery : IQuery<List<PackageGroupSuggestionDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
