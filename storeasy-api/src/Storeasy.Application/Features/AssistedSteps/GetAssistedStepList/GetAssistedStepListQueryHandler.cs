using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.GetAssistedStepList;

/// <summary>
/// Handles <see cref="GetAssistedStepListQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="stepRepository"></param>
/// <param name="rulesCatalog"></param>
/// <param name="localizer"></param>
public class GetAssistedStepListQueryHandler(IStoreContentService contentService,
                                             IStoreasyRepositoryBase<AssistedStep> stepRepository,
                                             IRulesCatalog rulesCatalog,
                                             IMilvaLocalizer localizer) : IInterceptable, IQueryHandler<GetAssistedStepListQuery, List<AssistedStepListDto>>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = stepRepository;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<Response<List<AssistedStepListDto>>> Handle(GetAssistedStepListQuery request, CancellationToken cancellationToken)
    {
        var records = await _contentService.GetStoreAppsAsync(request.OrganizationId, request.AppId, cancellationToken);
        var catalog = _rulesCatalog.Current;
        var definitions = records.Where(r => request.Store == null || r.Store == request.Store)
                                 .SelectMany(r => AssistedStepCatalog.For(catalog, _contentService.Subject(r, null)).Select(d => (r.AppId, Definition: d)))
                                 .ToList();

        var appIds = records.Select(r => r.AppId).Distinct().ToList();
        var rows = await _stepRepository.GetAllAsync(s => s.OrganizationId == request.OrganizationId && appIds.Contains(s.AppId),
                                                     projection: s => new AssistedStep
                                                     {
                                                         Id = s.Id,
                                                         AppId = s.AppId,
                                                         Store = s.Store,
                                                         Key = s.Key,
                                                         Status = s.Status,
                                                         CompletedDate = s.CompletedDate,
                                                         CompletedByUserName = s.CompletedByUserName,
                                                         CompletedChannel = s.CompletedChannel,
                                                     },
                                                     cancellationToken: cancellationToken);

        var missing = definitions.Where(d => !rows.Any(r => r.AppId == d.AppId && r.Store == d.Definition.Store && r.Key == d.Definition.Key))
                                 .Select(d => new AssistedStep { OrganizationId = request.OrganizationId, AppId = d.AppId, Store = d.Definition.Store, Key = d.Definition.Key, Status = AssistedStepStatus.Todo })
                                 .ToList();

        if (missing.Count > 0)
        {
            try
            {
                await _stepRepository.AddRangeAsync(missing, cancellationToken);
                rows.AddRange(missing);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                // A concurrent read created them first; they are listed on the next read.
            }
        }

        var language = System.Globalization.CultureInfo.CurrentUICulture.Name;

        return Response<List<AssistedStepListDto>>.Success([.. rows.Where(r => request.Store == null || r.Store == request.Store)
                                                                   .Where(r => request.Status == null || r.Status == request.Status)
                                                                   .Select(r => ContentViews.Step(r, AssistedStepCatalog.Find(catalog, r.Store, r.Key), catalog, _localizer, language))
                                                                   .OrderBy(s => s.AppId)
                                                                   .ThenBy(s => s.Store)
                                                                   .ThenBy(s => s.DueDate ?? DateOnly.MaxValue)]);
    }
}
