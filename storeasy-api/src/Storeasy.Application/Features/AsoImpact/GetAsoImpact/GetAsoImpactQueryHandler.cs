using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AsoImpact.GetAsoImpact;

/// <summary>
/// Handles <see cref="GetAsoImpactQuery"/>: finds the event among the app's events of the last 400 days (404
/// AsoImpactEventNotFound otherwise) and compares the windows with <see cref="IAsoImpactAnalyzer"/>.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="Analyzer"></param>
/// <param name="TimeProvider"></param>
public record GetAsoImpactQueryHandler(IStoreasyRepositoryBase<App> AppRepository,
                                       IAsoImpactAnalyzer Analyzer,
                                       TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetAsoImpactQuery, AsoImpactReportDto>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IAsoImpactAnalyzer _analyzer = Analyzer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AsoImpactReportDto>> Handle(GetAsoImpactQuery request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id, Name = a.Name },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<AsoImpactReportDto>.Error(null, MessageKey.AppNotFound);

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var events = await _analyzer.GetEventsAsync(new ImpactEventQuery(request.OrganizationId, request.AppId, null, today.AddDays(1 - StoreMetricRules.MaxRangeDays), today), cancellationToken);
        var target = events.FirstOrDefault(e => string.Equals(e.Id, request.EventId?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (target == null)
            return Response<AsoImpactReportDto>.Error(null, MessageKey.AsoImpactEventNotFound);

        var options = new ImpactOptions(request.WindowDays ?? ImpactOptions.DefaultWindowDays, request.GapDays ?? ImpactOptions.DefaultGapDays, request.Country, request.Source, today);
        var report = await _analyzer.AnalyzeAsync(target, events, options, request.IncludeRanks ?? true, cancellationToken);

        report.Event.AppName = app.Name;

        return Response<AsoImpactReportDto>.Success(report);
    }
}
