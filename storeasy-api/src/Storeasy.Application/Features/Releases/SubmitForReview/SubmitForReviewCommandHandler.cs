using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Content;

namespace Storeasy.Application.Features.Releases.SubmitForReview;

/// <summary>
/// Handles <see cref="SubmitForReviewCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
[Log]
public record SubmitForReviewCommandHandler(IStoreContentService ContentService,
                                            IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<SubmitForReviewCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(SubmitForReviewCommand request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Release, cancellationToken, request.VersionId, StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        if (!read.Snapshot.VersionEditable)
            return Response<ChangePlanDto>.Error(null, MessageKey.VersionNotEditable);

        var expected = ChangePlanRules.ConfirmationText(read.App.AppName, read.Snapshot.VersionString);

        if (!ChangePlanRules.ConfirmationMatches(expected, request.Confirm))
            return Response<ChangePlanDto>.Error(null, MessageKey.PlanConfirmMismatch);

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, ReleasePlanner.Submit(read.Snapshot), false, request.Confirm, cancellationToken);
    }
}
