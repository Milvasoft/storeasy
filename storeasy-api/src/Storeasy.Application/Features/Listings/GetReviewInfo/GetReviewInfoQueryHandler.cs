using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Listings.GetReviewInfo;

/// <summary>
/// Handles <see cref="GetReviewInfoQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="answerRepository"></param>
public class GetReviewInfoQueryHandler(IStoreContentService contentService,
                                       IStoreasyRepositoryBase<SavedAnswer> answerRepository) : IInterceptable, IQueryHandler<GetReviewInfoQuery, ReviewInfoDto>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = answerRepository;

    /// <inheritdoc/>
    public async Task<Response<ReviewInfoDto>> Handle(GetReviewInfoQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Review, cancellationToken, request.VersionId, StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<ReviewInfoDto>();

        var answers = await _answerRepository.GetAllAsync(a => a.AppId == read.App.AppId && a.OrganizationId == request.OrganizationId && (a.Kind == SavedAnswerKind.ReviewLogin || a.Kind == SavedAnswerKind.ReviewNotes),
                                                          projection: a => new SavedAnswer { Kind = a.Kind, Answers = a.Answers, KeyVersion = a.KeyVersion },
                                                          cancellationToken: cancellationToken);

        var login = SavedAnswerDocuments.Read<ReviewLoginAnswers>(answers.FirstOrDefault(a => a.Kind == SavedAnswerKind.ReviewLogin)?.Answers);
        var notes = SavedAnswerDocuments.Read<ReviewNotesAnswers>(answers.FirstOrDefault(a => a.Kind == SavedAnswerKind.ReviewNotes)?.Answers);

        string Value(string field) => read.Snapshot.Get(Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Review, null, field));

        var info = new ReviewInfoDto
        {
            StoreAppId = request.StoreAppId,
            VersionId = read.Snapshot.VersionId,
            ContactFirstName = Value("contactFirstName"),
            ContactLastName = Value("contactLastName"),
            ContactEmail = Value("contactEmail"),
            ContactPhone = Value("contactPhone"),
            DemoAccountRequired = Value("demoAccountRequired") == "true",
            DemoAccountName = Value("demoAccountName"),
            Notes = Value("notes"),
            StorePasswordSet = read.Snapshot.StoreDemoPasswordSet,
            DemoPasswordSet = answers.Any(a => a.Kind == SavedAnswerKind.ReviewLogin && a.KeyVersion != null),
        };

        info.StoreDiffers = login != null && (login.ContactFirstName != info.ContactFirstName
                                              || login.ContactLastName != info.ContactLastName
                                              || login.ContactEmail != info.ContactEmail
                                              || login.ContactPhone != info.ContactPhone
                                              || login.DemoAccountRequired != info.DemoAccountRequired
                                              || login.DemoAccountName != info.DemoAccountName
                                              || (notes != null && notes.Notes != info.Notes));

        return Response<ReviewInfoDto>.Success(info);
    }
}
