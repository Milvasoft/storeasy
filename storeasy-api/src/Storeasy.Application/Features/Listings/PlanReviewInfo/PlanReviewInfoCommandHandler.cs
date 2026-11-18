using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Listings.PlanReviewInfo;

/// <summary>
/// Handles <see cref="PlanReviewInfoCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="AnswerRepository"></param>
/// <param name="SecretProtector"></param>
[Log]
public record PlanReviewInfoCommandHandler(IStoreContentService ContentService,
                                           IChangePlanService ChangePlanService,
                                           IStoreasyRepositoryBase<SavedAnswer> AnswerRepository,
                                           ISecretProtector SecretProtector) : IInterceptable, ICommandHandler<PlanReviewInfoCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = AnswerRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanReviewInfoCommand request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Review, cancellationToken, request.VersionId, StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        if (!string.IsNullOrEmpty(request.DemoPassword))
        {
            var current = await _answerRepository.GetFirstOrDefaultAsync(a => a.AppId == read.App.AppId && a.OrganizationId == request.OrganizationId && a.Kind == SavedAnswerKind.ReviewLogin,
                                                                         projection: a => new SavedAnswer { Answers = a.Answers },
                                                                         cancellationToken: cancellationToken);

            await _answerRepository.SaveAnswerAsync(_secretProtector, request.OrganizationId, read.App.AppId, SavedAnswerKind.ReviewLogin, current?.Answers ?? "{}", request.DemoPassword, false, cancellationToken);
        }

        var answers = await _answerRepository.GetAllAsync(a => a.AppId == read.App.AppId && a.OrganizationId == request.OrganizationId && (a.Kind == SavedAnswerKind.ReviewLogin || a.Kind == SavedAnswerKind.ReviewNotes),
                                                          projection: a => new SavedAnswer { Kind = a.Kind, Answers = a.Answers, KeyVersion = a.KeyVersion },
                                                          cancellationToken: cancellationToken);

        var login = request.UseSavedAnswers ? SavedAnswerDocuments.Read<ReviewLoginAnswers>(answers.FirstOrDefault(a => a.Kind == SavedAnswerKind.ReviewLogin)?.Answers) : null;
        var notes = request.UseSavedAnswers ? SavedAnswerDocuments.Read<ReviewNotesAnswers>(answers.FirstOrDefault(a => a.Kind == SavedAnswerKind.ReviewNotes)?.Answers) : null;
        var passwordSealed = answers.Any(a => a.Kind == SavedAnswerKind.ReviewLogin && a.KeyVersion != null);
        var desired = new Dictionary<string, string>(StringComparer.Ordinal);

        void Want(string field, string value)
        {
            if (value != null)
                desired[Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Review, null, field)] = value;
        }

        var required = request.DemoAccountRequired ?? login?.DemoAccountRequired;

        Want("contactFirstName", request.ContactFirstName ?? login?.ContactFirstName);
        Want("contactLastName", request.ContactLastName ?? login?.ContactLastName);
        Want("contactEmail", request.ContactEmail ?? login?.ContactEmail);
        Want("contactPhone", request.ContactPhone ?? login?.ContactPhone);
        Want("demoAccountRequired", required == null ? null : required.Value ? "true" : "false");
        Want("demoAccountName", request.DemoAccountName ?? login?.DemoAccountName);
        Want("notes", request.Notes ?? notes?.Notes);

        var draft = FieldPlanner.Build(ChangePlanKind.ReviewInfo, "App Review information", read.Snapshot, desired, g => new Storeasy.Domain.JsonModels.PlanCall { Method = "PATCH", Path = "/v1/appStoreReviewDetails", Target = read.Snapshot.VersionString });
        var sendPassword = required != false && passwordSealed && (!string.IsNullOrEmpty(request.DemoPassword) || (request.UseSavedAnswers && !read.Snapshot.StoreDemoPasswordSet));

        if (sendPassword)
        {
            draft.Changes.DemoPasswordFromAnswers = true;
            draft.Items.Add(new Storeasy.Domain.JsonModels.PlanItem
            {
                Group = Storeasy.Domain.Content.ContentGroups.Review,
                Field = "demoAccountPassword",
                Before = read.Snapshot.StoreDemoPasswordSet ? "********" : null,
                After = "********",
                Action = read.Snapshot.StoreDemoPasswordSet ? ChangeAction.Update : ChangeAction.Create,
            });

            if (draft.Calls.Count == 0)
                draft.Calls.Add(new Storeasy.Domain.JsonModels.PlanCall { Method = "PATCH", Path = "/v1/appStoreReviewDetails", Target = read.Snapshot.VersionString });
        }

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, null, cancellationToken);
    }
}
