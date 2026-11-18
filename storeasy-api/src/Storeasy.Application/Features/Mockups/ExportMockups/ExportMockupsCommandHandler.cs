using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.ExportMockups;

/// <summary>
/// Handles <see cref="ExportMockupsCommand"/>: plans with <see cref="MockupExportPlanner"/>, queues one job with the
/// selected classes and their sizes, and writes the audit entry. The job draws the template version of this moment.
/// </summary>
/// <param name="TemplateRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="ScreenshotSizeSource"></param>
/// <param name="FileStorage"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record ExportMockupsCommandHandler(IStoreasyRepositoryBase<MockupTemplate> TemplateRepository,
                                          IStoreasyRepositoryBase<App> AppRepository,
                                          IScreenshotSizeSource ScreenshotSizeSource,
                                          IFileStorage FileStorage,
                                          IStoreJobQueue StoreJobQueue,
                                          IAuditTrail AuditTrail,
                                          TimeProvider TimeProvider) : IInterceptable, ICommandHandler<ExportMockupsCommand, long>
{
    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = TemplateRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IScreenshotSizeSource _screenshotSizeSource = ScreenshotSizeSource;
    private readonly IFileStorage _fileStorage = FileStorage;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(ExportMockupsCommand request, CancellationToken cancellationToken)
    {
        if (!_fileStorage.IsConfigured)
            return Response<long>.Error(0, MessageKey.StorageNotConfigured);

        var template = await _templateRepository.GetMockupSnapshotAsync(request.OrganizationId, request.TemplateId, cancellationToken);

        if (template == null)
            return Response<long>.Error(0, MessageKey.MockupTemplateNotFound);

        var stores = await _appRepository.GetAppStoresAsync(request.OrganizationId, template.AppId, cancellationToken);
        var (plan, errorKey) = MockupExportPlanner.PlanForTemplate(_screenshotSizeSource.Current,
                                                                   template,
                                                                   stores,
                                                                   request.Locales,
                                                                   request.ClassKeys,
                                                                   request.Targets,
                                                                   DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime));

        if (errorKey != null)
            return Response<long>.Error(0, errorKey);

        if (plan.Total == 0)
            return Response<long>.Error(0, MessageKey.MockupNothingToExport);

        var targets = plan.Items.Where(i => i.Selected).Select(i => new MockupExportTarget(i.ClassKey, i.Width, i.Height)).ToList();
        var payload = new MockupExportPayload(template.Id, template.Version, plan.Locales, targets);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.MockupExport, payload, AppId: template.AppId, MaxAttempts: 2)], cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.MockupExported,
                                     template.Id.ToString(),
                                     $"Export of mockup template '{template.Name}' queued as job {jobIds[0]}: {plan.Total} images, {targets.Count} classes, locales {string.Join(", ", plan.Locales)}.",
                                     details: new { JobId = jobIds[0], plan.Total, Classes = targets.Select(t => t.ClassKey).ToList(), plan.Locales },
                                     cancellationToken: cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
