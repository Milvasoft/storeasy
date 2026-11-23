using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetPackageDownloads;

/// <summary>
/// Downloads and usage of a tracked package as the syncs saved them: totals, the recent window, likes and points, the
/// daily series and the per version counts. Maven Central publishes no download counts.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPackageDownloadsQuery : IQuery<PackageDownloadsDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Tracked package.
    /// </summary>
    public int TrackedPackageId { get; set; }

    /// <summary>
    /// Days of the series (default and most 180).
    /// </summary>
    public int? Days { get; set; }
}
