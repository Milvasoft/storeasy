using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Readiness.GetAppCompliance;

/// <summary>
/// Handles <see cref="GetAppComplianceQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="readinessService"></param>
/// <param name="rulesCatalog"></param>
/// <param name="timeProvider"></param>
public class GetAppComplianceQueryHandler(IStoreContentService contentService,
                                          IReadinessService readinessService,
                                          IRulesCatalog rulesCatalog,
                                          TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetAppComplianceQuery, List<ComplianceItemDto>>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IReadinessService _readinessService = readinessService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<ComplianceItemDto>>> Handle(GetAppComplianceQuery request, CancellationToken cancellationToken)
    {
        var records = await _contentService.GetStoreAppsAsync(request.OrganizationId, request.AppId, cancellationToken);

        if (records.Count == 0)
            return Response<List<ComplianceItemDto>>.Error(null, MessageKey.AppNotFound);

        var steps = await _readinessService.GetStepsAsync(request.OrganizationId, request.AppId, cancellationToken);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var language = System.Globalization.CultureInfo.CurrentUICulture.Name;

        return Response<List<ComplianceItemDto>>.Success([.. records.SelectMany(r => ComplianceTimeline.Evaluate(r, _contentService.Subject(r, null), _rulesCatalog.Current, steps.GetValueOrDefault((r.AppId, r.Store)) ?? [], today, language))
                                                                    .OrderBy(i => i.DueDate ?? DateOnly.MaxValue)]);
    }
}
