using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Domain.Stores;
using Storeasy.Application.Utils.Aso;
using System.Text.Json;

namespace Storeasy.Application.Features.AsoKeywords.ResearchAsoKeywords;

/// <summary>
/// Handles <see cref="ResearchAsoKeywordsCommand"/>: resolves the store record, normalizes the keywords and queues an
/// <see cref="StoreJobKind.AsoKeywordResearch"/> job (or answers the identical one still on the way). The job measures the
/// keywords one by one with <see cref="IAsoKeywordResearcher"/>, so the request never waits for the paced store calls.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="Researcher"></param>
/// <param name="JobRepository"></param>
/// <param name="StoreJobQueue"></param>
public record ResearchAsoKeywordsCommandHandler(IStoreContentService ContentService,
                                                IAsoKeywordResearcher Researcher,
                                                IStoreasyRepositoryBase<StoreJob> JobRepository,
                                                IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<ResearchAsoKeywordsCommand, long>
{
    /// <summary>
    /// Attempts of a research job: a second one only when the runner stopped during the first (measurements already made
    /// are reused from the cache).
    /// </summary>
    public const int MaxAttempts = 2;

    private readonly IStoreContentService _contentService = ContentService;
    private readonly IAsoKeywordResearcher _researcher = Researcher;
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = JobRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(ResearchAsoKeywordsCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, request.Store, cancellationToken);

        if (app == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        if (!_researcher.Supports(app.Store))
            return Response<long>.Error(0, MessageKey.AsoStoreNotSupported);

        var country = AsoCountries.Normalize(request.Country);
        var keywords = AsoExtensions.NormalizeKeywords(request.Keywords, country);

        if (keywords.Count == 0)
            return Response<long>.Error(0, MessageKey.AsoKeywordsInvalid);

        var payload = new AsoKeywordResearchPayload(app.StoreAppId, country, AsoCountries.LanguageOf(request.Language, country), [.. keywords.Take(ResearchAsoKeywordsCommand.MaxKeywords)]);
        var payloadJson = JsonSerializer.Serialize(payload);

        // A double click or a retried call answers the job already on the way instead of measuring the same words twice.
        var running = await _jobRepository.GetFirstOrDefaultAsync(j => j.OrganizationId == request.OrganizationId
                                                                       && j.Kind == StoreJobKind.AsoKeywordResearch
                                                                       && j.AppId == app.AppId
                                                                       && j.Store == app.Store
                                                                       && (j.Status == StoreJobStatus.Queued || j.Status == StoreJobStatus.Running)
                                                                       && !j.CancelRequested
                                                                       && j.Payload == payloadJson,
                                                                  projection: j => new StoreJob { Id = j.Id },
                                                                  cancellationToken: cancellationToken);

        if (running != null)
            return Response<long>.Success(running.Id);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.AsoKeywordResearch, payload, app.Store, app.AppId, MaxAttempts)], cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
