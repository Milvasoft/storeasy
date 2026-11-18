using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.GetArtifactCounts;

/// <summary>
/// Counts the organization's packages by status and source over a period (default the last 30 days, at most
/// <see cref="MaxDays"/>), optionally for one app, so the intake overview does not count a page of sessions.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetArtifactCountsQuery : IQuery<ArtifactCountsDto>, IOrganizationRequest
{
    /// <summary>
    /// Longest period.
    /// </summary>
    public const int MaxDays = 366;

    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only this app's packages.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Start (UTC); 30 days before <see cref="To"/> when empty.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// End (UTC); now when empty.
    /// </summary>
    public DateTime? To { get; set; }
}
