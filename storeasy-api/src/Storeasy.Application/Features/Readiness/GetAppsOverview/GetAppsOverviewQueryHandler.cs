using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Readiness.GetAppsOverview;

/// <summary>
/// Handles <see cref="GetAppsOverviewQuery"/>. Hidden apps are left out.
/// </summary>
/// <param name="contentService"></param>
/// <param name="readinessService"></param>
/// <param name="appRepository"></param>
/// <param name="rulesCatalog"></param>
/// <param name="timeProvider"></param>
public class GetAppsOverviewQueryHandler(IStoreContentService contentService,
                                         IReadinessService readinessService,
                                         IStoreasyRepositoryBase<App> appRepository,
                                         IRulesCatalog rulesCatalog,
                                         TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetAppsOverviewQuery, List<AppOverviewDto>>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IReadinessService _readinessService = readinessService;
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<AppOverviewDto>>> Handle(GetAppsOverviewQuery request, CancellationToken cancellationToken)
    {
        var records = await _contentService.GetStoreAppsAsync(request.OrganizationId, null, cancellationToken);
        var errors = new Dictionary<int, string>();

        if (request.Refresh)
        {
            foreach (var record in records.Where(r => r.Store is StoreKind.Apple or StoreKind.Google))
            {
                var report = await _readinessService.CheckAsync(record, cancellationToken);

                if (!report.IsSuccess)
                    errors[record.StoreAppId] = string.Join(" ", report.Messages?.Select(m => m.Key ?? m.Message) ?? []);
            }
        }

        var apps = await _appRepository.GetAllAsync(a => a.OrganizationId == request.OrganizationId && a.HiddenDate == null,
                                                    projection: a => new App
                                                    {
                                                        Id = a.Id,
                                                        Name = a.Name,
                                                        IconUrl = a.IconUrl,
                                                        StoreApps = a.StoreApps.Select(s => new StoreApp { Id = s.Id, Store = s.Store, ExternalId = s.ExternalId, LastReadiness = s.LastReadiness }).ToList(),
                                                    },
                                                    cancellationToken: cancellationToken);

        var steps = await _readinessService.GetStepsAsync(request.OrganizationId, null, cancellationToken);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var language = System.Globalization.CultureInfo.CurrentUICulture.Name;

        return Response<List<AppOverviewDto>>.Success([.. apps.OrderBy(a => a.Name).Select(a => new AppOverviewDto
        {
            AppId = a.Id,
            Name = a.Name,
            IconUrl = a.IconUrl,
            Stores = [.. a.StoreApps.OrderBy(s => s.Store).Select(s => new AppOverviewStoreDto
            {
                StoreAppId = s.Id,
                Store = s.Store,
                ExternalId = s.ExternalId,
                VersionString = s.LastReadiness?.VersionString,
                CheckedDate = s.LastReadiness?.CheckedDate,
                Ok = s.LastReadiness?.Ok ?? 0,
                Missing = s.LastReadiness?.Missing ?? 0,
                Warning = s.LastReadiness?.Warning ?? 0,
                Assisted = s.LastReadiness?.Assisted ?? 0,
                Unknown = s.LastReadiness?.Unknown ?? 0,
                MissingKeys = s.LastReadiness?.MissingKeys ?? [],
                Error = errors.GetValueOrDefault(s.Id),
            })],
            NextDeadline = ComplianceTimeline.Next(records.Where(r => r.AppId == a.Id)
                                                          .SelectMany(r => ComplianceTimeline.Evaluate(r, _contentService.Subject(r, null), _rulesCatalog.Current, steps.GetValueOrDefault((r.AppId, r.Store)) ?? [], today, language)),
                                                   today),
        })]);
    }
}
