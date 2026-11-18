using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.GetMockupExportPlan;

/// <summary>
/// Handles <see cref="GetMockupExportPlanQuery"/> with <see cref="MockupExportPlanner"/>, the same plan the export uses.
/// </summary>
/// <param name="templateRepository"></param>
/// <param name="appRepository"></param>
/// <param name="screenshotSizeSource"></param>
/// <param name="timeProvider"></param>
public class GetMockupExportPlanQueryHandler(IStoreasyRepositoryBase<MockupTemplate> templateRepository,
                                             IStoreasyRepositoryBase<App> appRepository,
                                             IScreenshotSizeSource screenshotSizeSource,
                                             TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetMockupExportPlanQuery, MockupExportPlanDto>
{
    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = templateRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IScreenshotSizeSource _screenshotSizeSource = screenshotSizeSource;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<MockupExportPlanDto>> Handle(GetMockupExportPlanQuery request, CancellationToken cancellationToken)
    {
        var template = await _templateRepository.GetMockupSnapshotAsync(request.OrganizationId, request.TemplateId, cancellationToken);

        if (template == null)
            return Response<MockupExportPlanDto>.Error(null, MessageKey.MockupTemplateNotFound);

        var stores = await _appRepository.GetAppStoresAsync(request.OrganizationId, template.AppId, cancellationToken);
        var (plan, errorKey) = MockupExportPlanner.PlanForTemplate(_screenshotSizeSource.Current,
                                                                   template,
                                                                   stores,
                                                                   request.Locales,
                                                                   request.ClassKeys,
                                                                   request.Targets,
                                                                   DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime));

        if (errorKey != null)
            return Response<MockupExportPlanDto>.Error(null, errorKey);

        return Response<MockupExportPlanDto>.Success(plan);
    }
}
