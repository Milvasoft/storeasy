using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AnswerDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.GetSavedAnswers;

/// <summary>
/// Handles <see cref="GetSavedAnswersQuery"/>.
/// </summary>
/// <param name="appRepository"></param>
/// <param name="answerRepository"></param>
public class GetSavedAnswersQueryHandler(IStoreasyRepositoryBase<App> appRepository,
                                         IStoreasyRepositoryBase<SavedAnswer> answerRepository) : IInterceptable, IQueryHandler<GetSavedAnswersQuery, SavedAnswerSetDto>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = answerRepository;

    /// <inheritdoc/>
    public async Task<Response<SavedAnswerSetDto>> Handle(GetSavedAnswersQuery request, CancellationToken cancellationToken)
    {
        var appId = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId, projection: a => (int?)a.Id, cancellationToken: cancellationToken);

        if (appId == null)
            return Response<SavedAnswerSetDto>.Error(null, MessageKey.AppNotFound);

        var rows = await _answerRepository.GetAllAsync(a => a.AppId == request.AppId && a.OrganizationId == request.OrganizationId,
                                                       projection: a => new SavedAnswer
                                                       {
                                                           Kind = a.Kind,
                                                           Answers = a.Answers,
                                                           KeyVersion = a.KeyVersion,
                                                           CreationDate = a.CreationDate,
                                                           CreatorUserName = a.CreatorUserName,
                                                           LastModificationDate = a.LastModificationDate,
                                                           LastModifierUserName = a.LastModifierUserName,
                                                       },
                                                       cancellationToken: cancellationToken);

        return Response<SavedAnswerSetDto>.Success(new SavedAnswerSetDto
        {
            AppId = request.AppId,
            DemoPasswordSet = rows.Any(r => r.Kind == SavedAnswerKind.ReviewLogin && r.KeyVersion != null),
            Sections = [.. rows.OrderBy(r => r.Kind).Select(r => new SavedAnswerSectionDto
            {
                Key = r.Kind,
                UpdatedDate = r.LastModificationDate ?? r.CreationDate,
                UpdatedByUserName = r.LastModifierUserName ?? r.CreatorUserName,
                Answers = System.Text.Json.JsonDocument.Parse(r.Answers ?? "{}").RootElement.Clone(),
            })],
        });
    }
}
