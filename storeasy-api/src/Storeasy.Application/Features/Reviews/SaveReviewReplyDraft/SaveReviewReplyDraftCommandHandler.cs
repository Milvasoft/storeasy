using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.SaveReviewReplyDraft;

/// <summary>
/// Handles <see cref="SaveReviewReplyDraftCommand"/>.
/// </summary>
/// <param name="ReviewRepository"></param>
public record SaveReviewReplyDraftCommandHandler(IStoreasyRepositoryBase<StoreReview> ReviewRepository) : IInterceptable, ICommandHandler<SaveReviewReplyDraftCommand, long>
{
    private readonly IStoreasyRepositoryBase<StoreReview> _reviewRepository = ReviewRepository;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SaveReviewReplyDraftCommand request, CancellationToken cancellationToken)
    {
        var updated = await _reviewRepository.ExecuteUpdateAsync(r => r.Id == request.ReviewId && r.OrganizationId == request.OrganizationId,
                                                                 new SetPropertyBuilder<StoreReview>().SetPropertyValue(r => r.DraftBody, string.IsNullOrWhiteSpace(request.Body) ? null : request.Body),
                                                                 cancellationToken);

        if (updated == 0)
            return Response<long>.Error(0, MessageKey.ReviewNotFound);

        return Response<long>.Success(request.ReviewId);
    }
}
