using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.SaveMockupCaptions;

/// <summary>
/// Handles <see cref="SaveMockupCaptionsCommand"/>. The cells are replaced in one transaction (delete the sent cells,
/// insert the non empty ones). Overflow is measured afterwards in the narrowest class of the app's stores, so a save never
/// fails for a long caption: the editor shows the warning.
/// </summary>
/// <param name="TemplateRepository"></param>
/// <param name="CaptionRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="ScreenshotSizeSource"></param>
/// <param name="MockupRenderer"></param>
/// <param name="TransactionRunner"></param>
/// <param name="AuditTrail"></param>
[Log]
public record SaveMockupCaptionsCommandHandler(IStoreasyRepositoryBase<MockupTemplate> TemplateRepository,
                                               IStoreasyRepositoryBase<MockupCaption> CaptionRepository,
                                               IStoreasyRepositoryBase<App> AppRepository,
                                               IScreenshotSizeSource ScreenshotSizeSource,
                                               IMockupRenderer MockupRenderer,
                                               ITransactionRunner TransactionRunner,
                                               IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<SaveMockupCaptionsCommand, MockupCaptionsSavedDto>
{
    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = TemplateRepository;
    private readonly IStoreasyRepositoryBase<MockupCaption> _captionRepository = CaptionRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IScreenshotSizeSource _screenshotSizeSource = ScreenshotSizeSource;
    private readonly IMockupRenderer _mockupRenderer = MockupRenderer;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<MockupCaptionsSavedDto>> Handle(SaveMockupCaptionsCommand request, CancellationToken cancellationToken)
    {
        var template = await _templateRepository.GetFirstOrDefaultAsync(t => t.Id == request.TemplateId && t.OrganizationId == request.OrganizationId,
                                                                        projection: t => new MockupTemplate { Id = t.Id, AppId = t.AppId, Name = t.Name, Locales = t.Locales, SlotCount = t.SlotCount, Spec = t.Spec },
                                                                        cancellationToken: cancellationToken);

        if (template == null)
            return Response<MockupCaptionsSavedDto>.Error(null, MessageKey.MockupTemplateNotFound);

        var locales = template.Locales.ToDictionary(l => l, l => l, StringComparer.OrdinalIgnoreCase);
        var unknownLocale = request.Items.FirstOrDefault(i => !locales.ContainsKey(i.Locale));

        if (unknownLocale != null)
            return Response<MockupCaptionsSavedDto>.Error(null, MessageKey.MockupLocaleInvalid);

        if (request.Items.Any(i => i.SlotOrder > template.SlotCount))
            return Response<MockupCaptionsSavedDto>.Error(null, MessageKey.MockupSlotInvalid);

        var cells = request.Items.Select(i => new MockupCaption
        {
            MockupTemplateId = template.Id,
            Locale = locales[i.Locale],
            SlotOrder = i.SlotOrder,
            Title = string.IsNullOrWhiteSpace(i.Title) ? null : i.Title.Trim(),
            Subtitle = string.IsNullOrWhiteSpace(i.Subtitle) ? null : i.Subtitle.Trim(),
        }).ToList();

        var keys = cells.Select(c => c.Locale + "|" + c.SlotOrder.ToString()).ToList();
        var rows = cells.Where(c => c.Title != null || c.Subtitle != null).ToList();

        await _transactionRunner.RunAsync(async () =>
        {
            await _captionRepository.ExecuteDeleteAsync(c => c.MockupTemplateId == template.Id && keys.Contains(c.Locale + "|" + c.SlotOrder.ToString()), cancellationToken: cancellationToken);

            if (rows.Count > 0)
                await _captionRepository.AddRangeAsync(rows, cancellationToken);
        });

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.MockupCaptionsUpdated,
                                     template.Id.ToString(),
                                     $"{cells.Count} caption cells of mockup template '{template.Name}' saved ({string.Join(", ", cells.Select(c => c.Locale).Distinct())}).",
                                     cancellationToken: cancellationToken);

        var stores = await _appRepository.GetAppStoresAsync(request.OrganizationId, template.AppId, cancellationToken);
        var catalog = _screenshotSizeSource.Current;
        var narrowest = MockupRenderPlanner.NarrowestCaptionClass(catalog.Classes.Where(c => (stores.Count == 0 || stores.Contains(c.Store)) && c.Level != "optional"));
        var spec = MockupSpecRules.Upgrade(template.Spec);
        var warnings = new List<MockupWarningDto>();

        if (narrowest != null)
        {
            var frame = MockupExportRules.ResolveFrame(narrowest, spec.Frame.Kind);

            foreach (var row in rows)
            {
                var measure = _mockupRenderer.MeasureCaptions(spec, narrowest.ExportSize, frame, row.Locale, row.Title, row.Subtitle);

                warnings.AddRange(measure.Warnings.Where(w => w.Code == MockupWarningCodes.CaptionOverflow).Select(w => MockupWarningDto.From(w, narrowest.Key, row.Locale, row.SlotOrder)));
            }
        }

        return Response<MockupCaptionsSavedDto>.Success(new MockupCaptionsSavedDto
        {
            Saved = cells.Count,
            MeasuredClassKey = narrowest?.Key,
            Warnings = warnings,
        });
    }
}
