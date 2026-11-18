using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.PlanRollout;

/// <summary>
/// Handles <see cref="PlanRolloutCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
[Log]
public record PlanRolloutCommandHandler(IStoreContentService ContentService,
                                        IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<PlanRolloutCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanRolloutCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);
        var read = await _contentService.ReadContentAsync(app, ContentParts.Release, request.VersionId, null, cancellationToken);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        var draft = ReleasePlanner.Rollout(read.App.Store, read.Snapshot, new RolloutChange
        {
            Action = request.Action,
            Track = string.IsNullOrWhiteSpace(request.Track) ? null : request.Track.Trim(),
            UserFraction = request.Percentage == null ? null : request.Percentage / 100d,
        }, out var errorKey);

        if (draft == null)
            return Response<ChangePlanDto>.Error(null, errorKey);

        if (!string.IsNullOrWhiteSpace(request.Reason))
            draft.Title = $"{draft.Title} ({request.Reason.Trim()})";

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, null, cancellationToken);
    }
}
