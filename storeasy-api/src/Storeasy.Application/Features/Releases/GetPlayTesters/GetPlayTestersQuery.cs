using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.GetPlayTesters;

/// <summary>
/// Reads the tester Google Groups of a Play testing track live.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPlayTestersQuery : IQuery<PlayTestersDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (Google Play).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Track.
    /// </summary>
    public string Track { get; set; }
}
