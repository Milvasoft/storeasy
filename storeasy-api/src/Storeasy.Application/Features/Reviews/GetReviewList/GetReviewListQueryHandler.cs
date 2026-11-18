using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.GetReviewList;

/// <summary>
/// Handles <see cref="GetReviewListQuery"/>. The read mark is the caller's own.
/// </summary>
/// <param name="ReviewRepository"></param>
/// <param name="CurrentUser"></param>
public record GetReviewListQueryHandler(IStoreasyRepositoryBase<StoreReview> ReviewRepository, ICurrentUser CurrentUser) : IInterceptable, IListQueryHandler<GetReviewListQuery, ReviewListDto>
{
    private readonly IStoreasyRepositoryBase<StoreReview> _reviewRepository = ReviewRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<ListResponse<ReviewListDto>> Handle(GetReviewListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(ReviewListDto.ReviewDate), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        var userId = _currentUser.GetRequiredId();
        var locale = request.Locale?.Trim().ToLower();
        var search = request.Search?.Trim().ToLower();

        var response = await _reviewRepository.GetAllAsync(request,
                                                           condition: r => r.OrganizationId == request.OrganizationId
                                                                           && !r.App.IsDeleted
                                                                           && (request.AppId == null || r.AppId == request.AppId)
                                                                           && (request.Store == null || r.Store == request.Store)
                                                                           && (request.MinRating == null || r.Rating >= request.MinRating)
                                                                           && (request.MaxRating == null || r.Rating <= request.MaxRating)
                                                                           && (locale == null || r.Locale.ToLower().StartsWith(locale))
                                                                           && (request.HasReply == null || (r.ReplyState == ReviewReplyState.Published) == request.HasReply)
                                                                           && (request.IsRead == null || r.Reads.Any(x => x.UserId == userId) == request.IsRead)
                                                                           && (request.Since == null || r.ReviewDate >= request.Since)
                                                                           && (search == null || r.Body.ToLower().Contains(search) || r.Title.ToLower().Contains(search) || r.AuthorName.ToLower().Contains(search)),
                                                           projection: r => new ReviewListDto
                                                           {
                                                               Id = r.Id,
                                                               AppId = r.AppId,
                                                               AppName = r.App.Name,
                                                               Store = r.Store,
                                                               StoreAppId = r.StoreAppId,
                                                               ExternalReviewId = r.ExternalReviewId,
                                                               Rating = r.Rating,
                                                               Title = r.Title,
                                                               Body = r.Body,
                                                               AuthorName = r.AuthorName,
                                                               Locale = r.Locale,
                                                               Territory = r.Territory,
                                                               AppVersion = r.AppVersion,
                                                               Device = r.Device,
                                                               ReviewDate = r.ReviewDate,
                                                               IsRead = r.Reads.Any(x => x.UserId == userId),
                                                               ReplyBody = r.ReplyBody,
                                                               ReplyState = r.ReplyState,
                                                               ReplyDate = r.ReplyDate,
                                                               ReplyByUserName = r.ReplyByUserName,
                                                               ReplyError = r.ReplyError,
                                                               ReplyJobId = r.ReplyJobId,
                                                               DraftBody = r.DraftBody,
                                                           },
                                                           cancellationToken: cancellationToken);

        response.Data?.ForEach(r => r.ReplyMaxLength = ReviewRules.ReplyMaxLength(r.Store));

        return response;
    }
}
