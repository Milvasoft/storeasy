using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Domain.Stores;
using Storeasy.Domain.Aso;
using System.Text.Json;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordResearch;

/// <summary>
/// Handles <see cref="GetAsoKeywordResearchQuery"/>: reads the research job of the app and store from storeasy's records,
/// its measuring step's progress and, when it succeeded, its saved result with the keywords tracked now. 404
/// StoreJobNotFound for a job id that is not a research of this app and store.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="JobRepository"></param>
/// <param name="TrackedRepository"></param>
/// <param name="Localizer"></param>
public record GetAsoKeywordResearchQueryHandler(IStoreContentService ContentService,
                                                IStoreasyRepositoryBase<StoreJob> JobRepository,
                                                IStoreasyRepositoryBase<AsoTrackedKeyword> TrackedRepository,
                                                IMilvaLocalizer Localizer) : IInterceptable, IQueryHandler<GetAsoKeywordResearchQuery, AsoKeywordResearchJobDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = JobRepository;
    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = TrackedRepository;
    private readonly IMilvaLocalizer _localizer = Localizer;

    /// <inheritdoc/>
    public async Task<Response<AsoKeywordResearchJobDto>> Handle(GetAsoKeywordResearchQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, request.Store, cancellationToken);

        if (app == null)
            return Response<AsoKeywordResearchJobDto>.Error(null, MessageKey.StoreAppNotFound);

        var jobId = request.JobId;

        if (jobId == null)
        {
            // The ids only, so older results are not read for nothing.
            var ids = await _jobRepository.GetAllAsync(j => j.OrganizationId == request.OrganizationId
                                                            && j.Kind == StoreJobKind.AsoKeywordResearch
                                                            && j.AppId == app.AppId
                                                            && j.Store == app.Store,
                                                       projection: j => new StoreJob { Id = j.Id },
                                                       cancellationToken: cancellationToken);

            if (ids.Count == 0)
                return Response<AsoKeywordResearchJobDto>.Success(null);

            jobId = ids.Max(j => j.Id);
        }

        var job = await _jobRepository.GetFirstOrDefaultAsync(j => j.Id == jobId
                                                                   && j.OrganizationId == request.OrganizationId
                                                                   && j.Kind == StoreJobKind.AsoKeywordResearch
                                                                   && j.AppId == app.AppId
                                                                   && j.Store == app.Store,
                                                              projection: j => new StoreJob
                                                              {
                                                                  Id = j.Id,
                                                                  Status = j.Status,
                                                                  CancelRequested = j.CancelRequested,
                                                                  Payload = j.Payload,
                                                                  Result = j.Result,
                                                                  Error = j.Error,
                                                                  CreationDate = j.CreationDate,
                                                                  EndDate = j.EndDate,
                                                                  Steps = j.Steps.Where(s => s.Name == AsoKeywordResearchResult.MeasureStep)
                                                                                 .OrderByDescending(s => s.Attempt)
                                                                                 .ThenByDescending(s => s.Order)
                                                                                 .Take(1)
                                                                                 .Select(s => new StoreJobStep { Progress = s.Progress, Total = s.Total })
                                                                                 .ToList(),
                                                              },
                                                              cancellationToken: cancellationToken);

        if (job == null)
            return Response<AsoKeywordResearchJobDto>.Error(null, MessageKey.StoreJobNotFound);

        var payload = string.IsNullOrWhiteSpace(job.Payload) ? null : JsonSerializer.Deserialize<AsoKeywordResearchPayload>(job.Payload);
        var step = job.Steps?.FirstOrDefault();
        var keywords = payload?.Keywords ?? [];
        var dto = new AsoKeywordResearchJobDto
        {
            JobId = job.Id,
            Status = job.Status,
            CancelRequested = job.CancelRequested,
            AppId = app.AppId,
            Store = app.Store,
            Country = payload?.Country,
            Keywords = keywords,
            Measured = (int)(step?.Progress ?? 0),
            Total = (int)(step?.Total ?? keywords.Count),
            Error = job.Status == StoreJobStatus.Failed ? ErrorText(job.Error) : null,
            CreationDate = job.CreationDate,
            EndDate = job.EndDate,
        };

        if (job.Status == StoreJobStatus.Succeeded && !string.IsNullOrWhiteSpace(job.Result))
        {
            var result = JsonSerializer.Deserialize<AsoKeywordResearchResult>(job.Result);
            var tracked = payload == null ? [] : await _trackedRepository.TrackedKeywordSetAsync(payload.StoreAppId, payload.Country, cancellationToken);

            dto.Items = result?.Items ?? [];
            dto.Items.ForEach(i => i.Tracked = tracked.Contains(i.Keyword));
            dto.Measured = dto.Total = Math.Max(dto.Total, dto.Items.Count);
        }

        return Response<AsoKeywordResearchJobDto>.Success(dto);
    }

    private string ErrorText(string error) => error switch
    {
        AsoKeywordResearchResult.SourceUnavailableError => _localizer[MessageKey.AsoSourceUnavailable],
        _ => error,
    };
}
