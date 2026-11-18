using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.Rules.GetRuleCatalog;

/// <summary>
/// Reads the version of the loaded store rules catalog.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetRuleCatalogQuery : IQuery<RuleCatalogInfoDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
