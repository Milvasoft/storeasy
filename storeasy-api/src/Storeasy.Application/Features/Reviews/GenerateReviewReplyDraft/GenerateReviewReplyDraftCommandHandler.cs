using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Reviews.GenerateReviewReplyDraft;

/// <summary>
/// Handles <see cref="GenerateReviewReplyDraftCommand"/>: reads the review, opens the organization's key, asks
/// <see cref="IReplyDraftWriter"/> for a reply, fits it into the store's limit (<see cref="ReviewRules.FitReply"/>) and
/// saves it as the draft. No [Log]: the review and the draft are customer text; the audit entry keeps the model and the
/// token counts only.
/// </summary>
/// <param name="ReviewRepository"></param>
/// <param name="SettingRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="DraftWriter"></param>
/// <param name="AuditTrail"></param>
/// <param name="Options"></param>
public record GenerateReviewReplyDraftCommandHandler(IStoreasyRepositoryBase<StoreReview> ReviewRepository,
                                                     IStoreasyRepositoryBase<OrganizationAiSetting> SettingRepository,
                                                     ISecretProtector SecretProtector,
                                                     IReplyDraftWriter DraftWriter,
                                                     IAuditTrail AuditTrail,
                                                     AiOptions Options) : IInterceptable, ICommandHandler<GenerateReviewReplyDraftCommand, GeneratedReplyDraftDto>
{
    private readonly IStoreasyRepositoryBase<StoreReview> _reviewRepository = ReviewRepository;
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = SettingRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IReplyDraftWriter _draftWriter = DraftWriter;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly AiOptions _options = Options;

    /// <inheritdoc/>
    public async Task<Response<GeneratedReplyDraftDto>> Handle(GenerateReviewReplyDraftCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetFirstOrDefaultAsync(r => r.Id == request.ReviewId && r.OrganizationId == request.OrganizationId,
                                                                    projection: r => new
                                                                    {
                                                                        r.Id,
                                                                        r.Store,
                                                                        r.Rating,
                                                                        r.Title,
                                                                        r.Body,
                                                                        r.Locale,
                                                                        r.Territory,
                                                                        r.AppVersion,
                                                                        r.ReplyBody,
                                                                        r.ReplyState,
                                                                        AppName = r.App.Name,
                                                                    },
                                                                    cancellationToken: cancellationToken);

        if (review == null)
            return Response<GeneratedReplyDraftDto>.Error(null, MessageKey.ReviewNotFound);

        var maxLength = ReviewRules.ReplyMaxLength(review.Store);

        if (maxLength == 0)
            return Response<GeneratedReplyDraftDto>.Error(null, MessageKey.ReviewReplyNotSupported);

        if (review.ReplyState == ReviewReplyState.Sending)
            return Response<GeneratedReplyDraftDto>.Error(null, MessageKey.ReviewReplyInProgress);

        var setting = await _settingRepository.OpenAiSettingAsync(_secretProtector, request.OrganizationId, cancellationToken);

        if (setting == null)
            return Response<GeneratedReplyDraftDto>.Error(null, MessageKey.AiKeyNotSet);

        var result = await _draftWriter.WriteAsync(new ReplyDraftRequest(setting.ApiKey,
                                                                         review.Store,
                                                                         review.AppName,
                                                                         review.Rating,
                                                                         review.Title,
                                                                         review.Body,
                                                                         review.Locale,
                                                                         review.Territory,
                                                                         review.AppVersion,
                                                                         review.ReplyState == ReviewReplyState.Published ? review.ReplyBody : null,
                                                                         maxLength,
                                                                         _options.ReplyDraftModelFor(setting.ReplyDraftModel)),
                                                   cancellationToken);

        if (result.Outcome != ReplyDraftOutcome.Written)
            return Response<GeneratedReplyDraftDto>.Error(null, FailureKey(result.Outcome));

        var body = ReviewRules.FitReply(result.Text, maxLength);

        if (body == null)
            return Response<GeneratedReplyDraftDto>.Error(null, MessageKey.AiReplyFailed);

        var shortened = body != result.Text.Trim();

        // The same write as reviews/reply-draft, except while a reply is being sent (that one owns the draft then).
        var saved = await _reviewRepository.ExecuteUpdateAsync(r => r.Id == review.Id && r.OrganizationId == request.OrganizationId && r.ReplyState != ReviewReplyState.Sending,
                                                               new SetPropertyBuilder<StoreReview>().SetPropertyValue(r => r.DraftBody, body),
                                                               cancellationToken);

        if (saved == 0)
            return Response<GeneratedReplyDraftDto>.Error(null, MessageKey.ReviewReplyInProgress);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.ReviewReplyDraftGenerated,
                                     review.Id.ToString(),
                                     $"Reply draft for review {review.Id} written with AI ({ReviewRules.Length(body)} characters).",
                                     review.Store,
                                     details: new { result.Model, result.InputTokens, result.OutputTokens, Shortened = shortened },
                                     cancellationToken: cancellationToken);

        return Response<GeneratedReplyDraftDto>.Success(new GeneratedReplyDraftDto { ReviewId = review.Id, Body = body, Shortened = shortened, Model = result.Model });
    }

    private static string FailureKey(ReplyDraftOutcome outcome) => outcome switch
    {
        ReplyDraftOutcome.KeyRejected => MessageKey.AiKeyRejected,
        ReplyDraftOutcome.RateLimited => MessageKey.AiRateLimited,
        ReplyDraftOutcome.Unavailable => MessageKey.AiUnavailable,
        ReplyDraftOutcome.Refused => MessageKey.AiReplyRefused,
        _ => MessageKey.AiReplyFailed,
    };
}
