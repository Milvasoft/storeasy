using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.MarkReviewsRead;

/// <summary>
/// Handles <see cref="MarkReviewsReadCommand"/>. Only the organization's reviews are marked.
/// </summary>
/// <param name="ReviewRepository"></param>
/// <param name="ReadRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
public record MarkReviewsReadCommandHandler(IStoreasyRepositoryBase<StoreReview> ReviewRepository,
                                            IStoreasyRepositoryBase<ReviewRead> ReadRepository,
                                            ICurrentUser CurrentUser,
                                            TimeProvider TimeProvider) : IInterceptable, ICommandHandler<MarkReviewsReadCommand, int>
{
    private readonly IStoreasyRepositoryBase<StoreReview> _reviewRepository = ReviewRepository;
    private readonly IStoreasyRepositoryBase<ReviewRead> _readRepository = ReadRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(MarkReviewsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();
        var requested = request.ReviewIds.Distinct().ToList();

        var reviews = await _reviewRepository.GetAllAsync(r => requested.Contains(r.Id) && r.OrganizationId == request.OrganizationId,
                                                          projection: r => new StoreReview { Id = r.Id },
                                                          cancellationToken: cancellationToken);

        var ids = reviews.Select(r => r.Id).ToList();

        if (ids.Count == 0)
            return Response<int>.Error(0, MessageKey.ReviewNotFound);

        if (!request.IsRead)
        {
            await _readRepository.ExecuteDeleteAsync(r => r.UserId == userId && ids.Contains(r.StoreReviewId), cancellationToken: cancellationToken);

            return Response<int>.Success(ids.Count);
        }

        var already = (await _readRepository.GetAllAsync(r => r.UserId == userId && ids.Contains(r.StoreReviewId),
                                                         projection: r => new ReviewRead { StoreReviewId = r.StoreReviewId },
                                                         cancellationToken: cancellationToken)).Select(r => r.StoreReviewId).ToHashSet();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var marks = ids.Where(id => !already.Contains(id)).Select(id => new ReviewRead { UserId = userId, StoreReviewId = id, ReadDate = now }).ToList();

        if (marks.Count > 0)
        {
            try
            {
                await _readRepository.AddRangeAsync(marks, cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent request marked some of them first; the marks exist either way.
            }
        }

        return Response<int>.Success(ids.Count);
    }
}
