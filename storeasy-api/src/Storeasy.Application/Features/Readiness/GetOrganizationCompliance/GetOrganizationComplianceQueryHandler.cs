using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Readiness.GetOrganizationCompliance;

/// <summary>
/// Handles <see cref="GetOrganizationComplianceQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="readinessService"></param>
/// <param name="rulesCatalog"></param>
/// <param name="timeProvider"></param>
public class GetOrganizationComplianceQueryHandler(IStoreContentService contentService,
                                                   IReadinessService readinessService,
                                                   IRulesCatalog rulesCatalog,
                                                   TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetOrganizationComplianceQuery, List<ComplianceItemDto>>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IReadinessService _readinessService = readinessService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<ComplianceItemDto>>> Handle(GetOrganizationComplianceQuery request, CancellationToken cancellationToken)
    {
        var records = await _contentService.GetStoreAppsAsync(request.OrganizationId, null, cancellationToken);
        var steps = await _readinessService.GetStepsAsync(request.OrganizationId, null, cancellationToken);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var language = System.Globalization.CultureInfo.CurrentUICulture.Name;

        return Response<List<ComplianceItemDto>>.Success([.. records.SelectMany(r => ComplianceTimeline.Evaluate(r, _contentService.Subject(r, null), _rulesCatalog.Current, steps.GetValueOrDefault((r.AppId, r.Store)) ?? [], today, language))
                                                                    .Where(i => i.DueDate != null)
                                                                    .Where(i => request.From == null || i.DueDate >= request.From)
                                                                    .Where(i => request.To == null || i.DueDate <= request.To)
                                                                    .Where(i => !request.OpenOnly || i.Status != ComplianceStatus.Done)
                                                                    .OrderBy(i => i.DueDate)
                                                                    .ThenBy(i => i.AppName)]);
    }
}
