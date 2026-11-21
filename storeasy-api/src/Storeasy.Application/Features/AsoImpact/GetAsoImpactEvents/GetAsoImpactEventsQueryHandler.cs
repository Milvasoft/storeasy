using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AsoImpact.GetAsoImpactEvents;

/// <summary>
/// Handles <see cref="GetAsoImpactEventsQuery"/>. An app of another organization answers 404.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="Analyzer"></param>
/// <param name="TimeProvider"></param>
public record GetAsoImpactEventsQueryHandler(IStoreasyRepositoryBase<App> AppRepository,
                                             IAsoImpactAnalyzer Analyzer,
                                             TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetAsoImpactEventsQuery, List<AsoImpactEventDto>>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IAsoImpactAnalyzer _analyzer = Analyzer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<AsoImpactEventDto>>> Handle(GetAsoImpactEventsQuery request, CancellationToken cancellationToken)
    {
        var to = request.To ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var from = request.From ?? to.AddDays(1 - (request.Days is > 0 ? request.Days.Value : GetAsoImpactEventsQuery.DefaultDays));

        if (from > to || to.DayNumber - from.DayNumber + 1 > StoreMetricRules.MaxRangeDays)
            return Response<List<AsoImpactEventDto>>.Error(null, MessageKey.StoreMetricsRangeInvalid);

        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id, Name = a.Name },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<List<AsoImpactEventDto>>.Error(null, MessageKey.AppNotFound);

        var events = await _analyzer.GetEventsAsync(new ImpactEventQuery(request.OrganizationId, request.AppId, request.StoreAppId, from, to), cancellationToken);

        return Response<List<AsoImpactEventDto>>.Success([.. events.Where(e => !request.Store.HasValue || e.Store == request.Store.Value).Select(e => AsoImpactEventDto.From(e, app.Name))]);
    }
}
