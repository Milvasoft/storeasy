using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.GetPlayTesters;

/// <summary>
/// Handles <see cref="GetPlayTestersQuery"/>.
/// </summary>
/// <param name="contentService"></param>
public class GetPlayTestersQueryHandler(IStoreContentService contentService) : IInterceptable, IQueryHandler<GetPlayTestersQuery, PlayTestersDto>
{
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public async Task<Response<PlayTestersDto>> Handle(GetPlayTestersQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Testers, cancellationToken, store: StoreKind.Google);

        if (!read.IsSuccess)
            return read.Error<PlayTestersDto>();

        var track = read.Snapshot.Tracks.FirstOrDefault(t => t.Track == request.Track);

        return track == null
            ? Response<PlayTestersDto>.Error(null, MessageKey.TrackInvalid)
            : Response<PlayTestersDto>.Success(new PlayTestersDto { StoreAppId = request.StoreAppId, Track = track.Track, GoogleGroups = track.GoogleGroups ?? [] });
    }
}
