using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.GetRejectionCaseDetail;

/// <summary>
/// Handles <see cref="GetRejectionCaseDetailQuery"/>. A guideline an earlier case of the same app cited too is marked as
/// repeated.
/// </summary>
/// <param name="CaseRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="StepRepository"></param>
public record GetRejectionCaseDetailQueryHandler(IStoreasyRepositoryBase<RejectionCase> CaseRepository,
                                                 IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                                 IStoreasyRepositoryBase<AssistedStep> StepRepository) : IInterceptable, IQueryHandler<GetRejectionCaseDetailQuery, RejectionCaseDto>
{
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = CaseRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = StepRepository;

    /// <inheritdoc/>
    public async Task<Response<RejectionCaseDto>> Handle(GetRejectionCaseDetailQuery request, CancellationToken cancellationToken)
    {
        var rejectionCase = await _caseRepository.GetCaseAsync(request.OrganizationId, request.CaseId, cancellationToken);

        if (rejectionCase == null)
            return Response<RejectionCaseDto>.Error(null, MessageKey.RejectionCaseNotFound);

        var storeApp = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == rejectionCase.StoreAppId,
                                                                        projection: s => new { s.ExternalId, AppName = s.App.Name },
                                                                        cancellationToken: cancellationToken);

        var earlier = await _caseRepository.GetAllAsync(c => c.AppId == rejectionCase.AppId && c.OrganizationId == request.OrganizationId && c.Id != rejectionCase.Id && c.OpenedDate < rejectionCase.OpenedDate,
                                                        projection: c => new RejectionCase { Guidelines = c.Guidelines },
                                                        cancellationToken: cancellationToken);

        var earlierCodes = earlier.SelectMany(c => c.Guidelines ?? []).Select(g => g.Code).ToHashSet();

        var dto = RejectionCases.ToDto(rejectionCase, storeApp?.AppName, storeApp?.ExternalId, earlierCodes);

        if (rejectionCase.Store == StoreKind.Apple)
        {
            var step = await _stepRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId && s.AppId == rejectionCase.AppId && s.Store == StoreKind.Apple && s.Key == RejectionReplyStepExtensions.ReplyStepKey,
                                                                    projection: s => new AssistedStep { Id = s.Id, Status = s.Status, CompletedDate = s.CompletedDate, CompletedByUserName = s.CompletedByUserName },
                                                                    cancellationToken: cancellationToken);

            dto.ReplyStepId = step?.Id;

            // The reply may have been recorded on the assisted step screen instead of here.
            if (dto.ReplyDraft.SentDate == null && step?.Status == AssistedStepStatus.Done && step.CompletedDate >= rejectionCase.OpenedDate)
            {
                dto.ReplyDraft.SentDate = step.CompletedDate;
                dto.ReplyDraft.SentByUserName = step.CompletedByUserName;
            }
        }

        return Response<RejectionCaseDto>.Success(dto);
    }
}
