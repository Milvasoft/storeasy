using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.GetReleaseOverview;

/// <summary>
/// Handles <see cref="GetReleaseOverviewQuery"/>.
/// </summary>
/// <param name="contentService"></param>
public class GetReleaseOverviewQueryHandler(IStoreContentService contentService) : IInterceptable, IQueryHandler<GetReleaseOverviewQuery, ReleaseOverviewDto>
{
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public async Task<Response<ReleaseOverviewDto>> Handle(GetReleaseOverviewQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Release | ContentParts.Testers, cancellationToken);

        if (!read.IsSuccess)
            return read.Error<ReleaseOverviewDto>();

        return Response<ReleaseOverviewDto>.Success(new ReleaseOverviewDto
        {
            StoreAppId = request.StoreAppId,
            Store = read.App.Store,
            Versions = [.. read.Snapshot.Versions.Select(v => new StoreVersionDto
            {
                VersionId = v.Id,
                VersionString = v.VersionString,
                Platform = v.Platform,
                State = v.State,
                ReleaseType = v.ReleaseType,
                EarliestReleaseDate = v.EarliestReleaseDate,
                BuildId = v.BuildId,
                BuildNumber = v.BuildNumber,
                PhasedRelease = v.PhasedReleaseState == null ? null : new PhasedReleaseDto { State = v.PhasedReleaseState, CurrentDayNumber = v.PhasedReleaseDay, StartDate = v.PhasedReleaseStartDate },
            })],
            Tracks = [.. read.Snapshot.Tracks.Select(t => new TrackDto
            {
                Track = t.Track,
                GoogleGroups = t.GoogleGroups,
                Releases = [.. t.Releases.Select(r => new TrackReleaseDto
                {
                    Name = r.Name,
                    VersionCodes = r.VersionCodes,
                    Status = r.Status,
                    UserFraction = r.UserFraction,
                    ReleaseNotes = r.ReleaseNotes,
                    Countries = r.Countries,
                    IncludeRestOfWorld = r.IncludeRestOfWorld,
                })],
            })],
        });
    }
}
