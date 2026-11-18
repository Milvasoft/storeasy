using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.TokenDtos;

namespace Storeasy.Application.Features.Tokens.CreatePersonalAccessToken;

/// <summary>
/// Creates a personal access token (sea_) of the caller for one organization (design 16.12). The value is returned once;
/// only its hash is stored. A viewer may create read only tokens only.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record CreatePersonalAccessTokenCommand : ICommand<PersonalAccessTokenCreatedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Name that tells where the token is used.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Whether the token may only read.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Lifetime in days (30, 90 or 365).
    /// </summary>
    public int ExpiryDays { get; set; } = 90;
}
