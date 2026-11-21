using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AsoImpact.GetAsoImpactRecent;

/// <summary>
/// Handles <see cref="GetAsoImpactRecentQuery"/>. Changes still waiting for release are left out; the events read for the
/// confounding check start one window and gap before the range.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="Analyzer"></param>
/// <param name="TimeProvider"></param>
public record GetAsoImpactRecentQueryHandler(IStoreasyRepositoryBase<App> AppRepository,
                                             IAsoImpactAnalyzer Analyzer,
                                             TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetAsoImpactRecentQuery, AsoImpactRecentDto>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IAsoImpactAnalyzer _analyzer = Analyzer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AsoImpactRecentDto>> Handle(GetAsoImpactRecentQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var from = today.AddDays(1 - (request.Days ?? GetAsoImpactRecentQuery.DefaultDays));
        var options = new ImpactOptions(request.WindowDays ?? ImpactOptions.DefaultWindowDays, ImpactOptions.DefaultGapDays, null, null, today);
        var events = await _analyzer.GetEventsAsync(new ImpactEventQuery(request.OrganizationId, null, null, from.AddDays(-(options.WindowDays + options.GapDays)), today), cancellationToken);

        var listed = events.Where(e => e.Date >= from && e.DateSource != ImpactDateSource.AwaitingRelease && (!request.Store.HasValue || e.Store == request.Store.Value))
                           .Take(request.Limit ?? GetAsoImpactRecentQuery.DefaultLimit)
                           .ToList();

        var appIds = listed.Select(e => e.AppId).Distinct().ToList();
        var names = appIds.Count == 0 ? [] : (await _appRepository.GetAllAsync(a => a.OrganizationId == request.OrganizationId && appIds.Contains(a.Id),
                                                                                projection: a => new App { Id = a.Id, Name = a.Name },
                                                                                cancellationToken: cancellationToken)).ToDictionary(a => a.Id, a => a.Name);
        var items = new List<AsoImpactSummaryDto>();

        foreach (var impactEvent in listed)
        {
            var report = await _analyzer.AnalyzeAsync(impactEvent, events, options, false, cancellationToken);

            items.Add(new AsoImpactSummaryDto
            {
                Event = AsoImpactEventDto.From(impactEvent, names.GetValueOrDefault(impactEvent.AppId)),
                Status = report.Status,
                PageViews = report.Metrics.FirstOrDefault(m => m.Metric == StoreMetricKind.PageViews),
                Downloads = report.Metrics.FirstOrDefault(m => m.Metric == StoreMetricKind.Downloads),
                ConversionRate = report.Metrics.FirstOrDefault(m => m.Metric == StoreMetricKind.ConversionRate),
                Signal = report.Conversion.Signal,
                Confounded = report.Confounders.Count > 0,
            });
        }

        return Response<AsoImpactRecentDto>.Success(new AsoImpactRecentDto { From = from, To = today, WindowDays = options.WindowDays, Items = items });
    }
}
