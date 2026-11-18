using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Features.Reviews.CreateSavedReply;
using Storeasy.Application.Features.Reviews.DeleteSavedReply;
using Storeasy.Application.Features.Reviews.GetReviewList;
using Storeasy.Application.Features.Reviews.GetSavedReplyList;
using Storeasy.Application.Features.Reviews.MarkReviewsRead;
using Storeasy.Application.Features.Reviews.ReplyToReview;
using Storeasy.Application.Features.Reviews.SaveReviewReplyDraft;
using Storeasy.Application.Features.Reviews.SyncReviews;
using Storeasy.Application.Features.Reviews.UpdateSavedReply;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// The organization's review inbox (App Store and Google Play), replies and saved replies.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ReviewsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Pages through the reviews with filters (app, store, rating, language, replied, read).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<ListResponse<ReviewListDto>> GetReviewsAsync(GetReviewListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Publishes (or replaces) a reply as a store job. Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("reply")]
    [Idempotent]
    [RateLimit(300, 3600, "reviews-reply")]
    public Task<Response<long>> ReplyAsync(ReplyToReviewCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Marks reviews as read or unread for the caller.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("read")]
    public Task<Response<int>> MarkReadAsync(MarkReviewsReadCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves an unsent reply draft.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("reply-draft")]
    public Task<Response<long>> SaveReplyDraftAsync(SaveReviewReplyDraftCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads new reviews now. Returns the job ids.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(30, 3600, "reviews-sync")]
    public Task<Response<List<long>>> SyncAsync(SyncReviewsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the saved replies (the review's language first).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("saved-replies")]
    public Task<Response<List<SavedReplyDto>>> GetSavedRepliesAsync([FromQuery] GetSavedReplyListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds a saved reply.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("saved-reply")]
    [Idempotent]
    public Task<Response<int>> CreateSavedReplyAsync(CreateSavedReplyCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Changes a saved reply.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("saved-reply")]
    public Task<Response<int>> UpdateSavedReplyAsync(UpdateSavedReplyCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes a saved reply.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("saved-reply")]
    public Task<Response<int>> DeleteSavedReplyAsync(DeleteSavedReplyCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
