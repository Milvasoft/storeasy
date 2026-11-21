using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Application.Utils.Analytics;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.StoreMetrics.GetStoreMetricsSummary;

/// <summary>
/// Handles <see cref="GetStoreMetricsSummaryQuery"/>. Removed apps are left out.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="MetricsQuery"></param>
/// <param name="TimeProvider"></param>
public record GetStoreMetricsSummaryQueryHandler(IStoreasyRepositoryBase<App> AppRepository,
                                                 IStoreMetricsQuery MetricsQuery,
                                                 TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetStoreMetricsSummaryQuery, StoreMetricsSummaryDto>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreMetricsQuery _metricsQuery = MetricsQuery;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<StoreMetricsSummaryDto>> Handle(GetStoreMetricsSummaryQuery request, CancellationToken cancellationToken)
    {
        var range = StoreMetricRange.Resolve(request.From, request.To, request.Days, _timeProvider.GetUtcNow());

        if (range == null)
            return Response<StoreMetricsSummaryDto>.Error(null, MessageKey.StoreMetricsRangeInvalid);

        var scope = new StoreMetricScope(request.OrganizationId, store: request.Store);

        var current = await _metricsQuery.GetByAppAsync(scope, range.From, range.To, cancellationToken);
        var previous = (await _metricsQuery.GetByAppAsync(scope, range.PreviousFrom, range.PreviousTo, cancellationToken)).ToDictionary(a => a.Key, a => a.Values);
        var totals = await _metricsQuery.GetTotalsAsync(scope, range.From, range.To, cancellationToken);
        var previousTotals = await _metricsQuery.GetTotalsAsync(scope, range.PreviousFrom, range.PreviousTo, cancellationToken);

        var appIds = current.Select(a => a.Key).Union(previous.Keys).ToList();
        var names = (await _appRepository.GetAllAsync(a => a.OrganizationId == request.OrganizationId && !a.IsDeleted && appIds.Contains(a.Id),
                                                      projection: a => new App { Id = a.Id, Name = a.Name },
                                                      cancellationToken: cancellationToken)).ToDictionary(a => a.Id, a => a.Name);

        var currentByApp = current.ToDictionary(a => a.Key, a => a.Values);

        var apps = appIds.Where(names.ContainsKey)
                         .Select(id =>
                         {
                             var values = currentByApp.GetValueOrDefault(id) ?? StoreMetricValues.Empty;

                             return new AppStoreMetricsDto
                             {
                                 AppId = id,
                                 AppName = names[id],
                                 Totals = StoreMetricValuesDto.From(values),
                                 Changes = StoreMetricChangesDto.From(values, previous.GetValueOrDefault(id) ?? StoreMetricValues.Empty),
                             };
                         })
                         .OrderByDescending(a => a.Totals.Downloads ?? 0)
                         .ThenBy(a => a.AppName, StringComparer.CurrentCultureIgnoreCase)
                         .ToList();

        return Response<StoreMetricsSummaryDto>.Success(new StoreMetricsSummaryDto
        {
            From = range.From,
            To = range.To,
            Totals = StoreMetricValuesDto.From(totals),
            Changes = StoreMetricChangesDto.From(totals, previousTotals),
            Apps = apps,
        });
    }
}
