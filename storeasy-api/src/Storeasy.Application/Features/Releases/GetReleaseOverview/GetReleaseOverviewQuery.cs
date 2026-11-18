using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.GetReleaseOverview;

/// <summary>
/// Reads the versions (App Store: state, release type, phased release, attached build) or the tracks (Play: releases, rollout, countries, testers) of a store record live.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetReleaseOverviewQuery : IQuery<ReleaseOverviewDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
