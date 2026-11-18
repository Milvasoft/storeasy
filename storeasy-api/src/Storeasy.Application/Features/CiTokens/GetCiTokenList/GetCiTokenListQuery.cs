using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.CiTokens.GetCiTokenList;

/// <summary>
/// Lists the organization's CI tokens that are not revoked (design 13.8). Values are never returned.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetCiTokenListQuery : IQuery<List<CiTokenDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
