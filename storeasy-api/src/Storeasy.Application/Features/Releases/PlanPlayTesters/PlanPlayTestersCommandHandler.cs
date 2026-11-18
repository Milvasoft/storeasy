using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.PlanPlayTesters;

/// <summary>
/// Handles <see cref="PlanPlayTestersCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
[Log]
public record PlanPlayTestersCommandHandler(IStoreContentService ContentService,
                                            IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<PlanPlayTestersCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanPlayTestersCommand request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Testers, cancellationToken, store: StoreKind.Google);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        if (read.Snapshot.Tracks.All(t => t.Track != request.Track))
            return Response<ChangePlanDto>.Error(null, MessageKey.TrackInvalid);

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, ReleasePlanner.Testers(read.Snapshot, request.Track, request.GoogleGroups), request.DryRun, null, cancellationToken);
    }
}
