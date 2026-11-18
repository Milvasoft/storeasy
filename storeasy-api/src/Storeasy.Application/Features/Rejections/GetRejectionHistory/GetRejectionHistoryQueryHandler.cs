using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Events;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.GetRejectionHistory;

/// <summary>
/// Handles <see cref="GetRejectionHistoryQuery"/>.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="VersionStateRepository"></param>
/// <param name="CaseRepository"></param>
public record GetRejectionHistoryQueryHandler(IStoreasyRepositoryBase<App> AppRepository,
                                              IStoreasyRepositoryBase<AppVersionState> VersionStateRepository,
                                              IStoreasyRepositoryBase<RejectionCase> CaseRepository) : IInterceptable, IQueryHandler<GetRejectionHistoryQuery, RejectionHistoryDto>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreasyRepositoryBase<AppVersionState> _versionStateRepository = VersionStateRepository;
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = CaseRepository;

    /// <inheritdoc/>
    public async Task<Response<RejectionHistoryDto>> Handle(GetRejectionHistoryQuery request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId,
                                                              projection: a => new { a.Id },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<RejectionHistoryDto>.Error(null, MessageKey.AppNotFound);

        var states = await _versionStateRepository.GetAllAsync(s => s.AppId == app.Id && s.OrganizationId == request.OrganizationId && s.SubmittedDate != null,
                                                               projection: s => new AppVersionState { StoreAppId = s.StoreAppId, VersionString = s.VersionString, BuildNumber = s.BuildNumber, State = s.State, SubmittedDate = s.SubmittedDate },
                                                               cancellationToken: cancellationToken);

        var cases = (await _caseRepository.GetAllAsync(c => c.AppId == app.Id && c.OrganizationId == request.OrganizationId,
                                                       projection: RejectionCaseListDto.Projection,
                                                       cancellationToken: cancellationToken)).Select(c => c.WithCounts()).OrderBy(c => c.OpenedDate).ToList();

        var submissions = states.Select(s => new SubmissionHistoryItemDto
        {
            StoreAppId = s.StoreAppId,
            VersionString = s.VersionString,
            BuildNumber = s.BuildNumber,
            State = s.State,
            SubmittedDate = s.SubmittedDate,
            Rejections = [.. cases.Where(c => c.StoreAppId == s.StoreAppId && c.VersionString == s.VersionString)],
        }).ToList();

        // Cases opened by hand (or before the version state was kept) still belong to the history.
        submissions.AddRange(cases.Where(c => !states.Any(s => s.StoreAppId == c.StoreAppId && s.VersionString == c.VersionString))
                                  .GroupBy(c => (c.StoreAppId, c.VersionString))
                                  .Select(g => new SubmissionHistoryItemDto
                                  {
                                      StoreAppId = g.Key.StoreAppId,
                                      VersionString = g.Key.VersionString,
                                      BuildNumber = g.First().BuildNumber,
                                      SubmittedDate = g.Min(c => c.OpenedDate),
                                      Rejections = [.. g],
                                  }));

        var repeated = cases.SelectMany(c => (c.Guidelines ?? []).Select(g => g.Code).Distinct())
                            .GroupBy(code => code)
                            .Where(g => g.Count() > 1)
                            .Select(g => g.Key)
                            .ToList();

        return Response<RejectionHistoryDto>.Success(new RejectionHistoryDto
        {
            AppId = app.Id,
            Submissions = [.. submissions.OrderBy(s => s.SubmittedDate)],
            SubmissionCount = submissions.Count,
            RejectionCount = cases.Count,
            RepeatedGuidelines = repeated,
        });
    }
}
