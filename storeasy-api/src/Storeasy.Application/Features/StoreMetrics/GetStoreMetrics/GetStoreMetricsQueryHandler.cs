using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Application.Utils.Analytics;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.StoreMetrics.GetStoreMetrics;

/// <summary>
/// Handles <see cref="GetStoreMetricsQuery"/>. An app of another organization answers 404.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="MetricsQuery"></param>
/// <param name="TimeProvider"></param>
public record GetStoreMetricsQueryHandler(IStoreasyRepositoryBase<App> AppRepository,
                                          IStoreMetricsQuery MetricsQuery,
                                          TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetStoreMetricsQuery, StoreMetricsReportDto>
{
    /// <summary>
    /// Most countries in the country breakdown.
    /// </summary>
    public const int CountryLimit = 50;

    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreMetricsQuery _metricsQuery = MetricsQuery;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<StoreMetricsReportDto>> Handle(GetStoreMetricsQuery request, CancellationToken cancellationToken)
    {
        var range = StoreMetricRange.Resolve(request.From, request.To, request.Days, _timeProvider.GetUtcNow());

        if (range == null)
            return Response<StoreMetricsReportDto>.Error(null, MessageKey.StoreMetricsRangeInvalid);

        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<StoreMetricsReportDto>.Error(null, MessageKey.AppNotFound);

        var scope = new StoreMetricScope(request.OrganizationId, request.AppId, store: request.Store, country: request.Country, source: request.Source);

        var totals = await _metricsQuery.GetTotalsAsync(scope, range.From, range.To, cancellationToken);
        var previous = await _metricsQuery.GetTotalsAsync(scope, range.PreviousFrom, range.PreviousTo, cancellationToken);
        var days = await _metricsQuery.GetDailyAsync(scope, range.From, range.To, cancellationToken);
        var sources = await _metricsQuery.GetBySourceAsync(scope, range.From, range.To, cancellationToken);
        var countries = await _metricsQuery.GetByCountryAsync(scope, range.From, range.To, CountryLimit, cancellationToken);
        var stores = await _metricsQuery.GetByStoreAsync(scope, range.From, range.To, cancellationToken);

        return Response<StoreMetricsReportDto>.Success(new StoreMetricsReportDto
        {
            From = range.From,
            To = range.To,
            PreviousFrom = range.PreviousFrom,
            PreviousTo = range.PreviousTo,
            Totals = StoreMetricValuesDto.From(totals),
            PreviousTotals = StoreMetricValuesDto.From(previous),
            Changes = StoreMetricChangesDto.From(totals, previous),
            Days = [.. days.Select(StoreMetricDayDto.From)],
            Sources = [.. sources.Select(s => new StoreMetricBreakdownDto { Source = s.Key, Totals = StoreMetricValuesDto.From(s.Values) })],
            Countries = [.. countries.Select(c => new StoreMetricBreakdownDto { Country = c.Key, Totals = StoreMetricValuesDto.From(c.Values) })],
            Stores = [.. stores.Select(s => new StoreMetricBreakdownDto { Store = s.Key, Totals = StoreMetricValuesDto.From(s.Values) })],
        });
    }
}
