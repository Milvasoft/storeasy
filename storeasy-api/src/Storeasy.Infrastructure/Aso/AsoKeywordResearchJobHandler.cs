using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// The <see cref="StoreJobKind.AsoKeywordResearch"/> job: measures the keywords of the payload one by one with
/// <see cref="IAsoKeywordResearcher"/> (paced store calls, cached measurements), reports "measured / total" on its
/// <see cref="AsoKeywordResearchResult.MeasureStep"/> step after each keyword, which is also where a cancel request is
/// seen, and saves the measurements as the job's result. When no store source answered for any keyword the job fails with
/// <see cref="AsoKeywordResearchResult.SourceUnavailableError"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="researcher"></param>
public sealed class AsoKeywordResearchJobHandler(IStoreContentService contentService, IAsoKeywordResearcher researcher) : IStoreJobHandler
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IAsoKeywordResearcher _researcher = researcher;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.AsoKeywordResearch;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<AsoKeywordResearchPayload>() ?? throw new StoreJobException("The job has no keywords.", transient: false);
        var app = await _contentService.GetStoreAppAsync(context.OrganizationId, payload.StoreAppId, cancellationToken) ?? throw new StoreJobException("The store record was removed.", transient: false);

        if (!_researcher.Supports(app.Store))
            throw new StoreJobException($"Keyword research does not support {app.Store}.", transient: false);

        var keywords = payload.Keywords ?? [];
        var items = new List<AsoResearchItem>();
        var step = await context.BeginStepAsync(AsoKeywordResearchResult.MeasureStep,
                                                StepDirection.Inbound,
                                                $"{keywords.Count} keywords in {payload.Country?.ToUpperInvariant()}",
                                                keywords.Count,
                                                cancellationToken);

        foreach (var keyword in keywords)
        {
            items.AddRange(await _researcher.ResearchAsync(app.Store, payload.Country, payload.Language, [keyword], app.ExternalId, cancellationToken));

            await context.ReportProgressAsync(step, items.Count, cancellationToken);
        }

        if (items.Count > 0 && items.All(i => i.Metric.Popularity == null && !i.ResultsRead))
        {
            await context.EndStepAsync(step, StoreJobStepStatus.Failed, AsoKeywordResearchResult.SourceUnavailableError, cancellationToken);

            throw new StoreJobException(AsoKeywordResearchResult.SourceUnavailableError, transient: false);
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{items.Count} keywords measured.", cancellationToken);

        var result = new AsoKeywordResearchResult { Items = [.. items.Select(i => i.ToResearchDto(app.Store, payload.Country, app.ExternalId))] };
        var ranked = result.Items.Count(i => i.CurrentRank != null);

        return new StoreJobOutcome($"{items.Count} keywords measured for {app.AppName} ({app.Store}, {payload.Country?.ToUpperInvariant()}); the app ranks for {ranked}.", result);
    }
}
