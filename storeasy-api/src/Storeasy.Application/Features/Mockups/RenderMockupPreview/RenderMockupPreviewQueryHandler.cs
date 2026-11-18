using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Studio;
using System.Diagnostics;

namespace Storeasy.Application.Features.Mockups.RenderMockupPreview;

/// <summary>
/// Handles <see cref="RenderMockupPreviewQuery"/>. Raw screenshots come through the studio cache; a too small screenshot
/// is drawn with a warning instead of failing, so the editor still shows the slot.
/// </summary>
/// <param name="templateRepository"></param>
/// <param name="assetRepository"></param>
/// <param name="screenshotSizeSource"></param>
/// <param name="mockupRenderer"></param>
/// <param name="studioAssetReader"></param>
/// <param name="fileStorage"></param>
/// <param name="studioOptions"></param>
public class RenderMockupPreviewQueryHandler(IStoreasyRepositoryBase<MockupTemplate> templateRepository,
                                             IStoreasyRepositoryBase<Asset> assetRepository,
                                             IScreenshotSizeSource screenshotSizeSource,
                                             IMockupRenderer mockupRenderer,
                                             IStudioAssetReader studioAssetReader,
                                             IFileStorage fileStorage,
                                             StudioOptions studioOptions) : IInterceptable, IQueryHandler<RenderMockupPreviewQuery, MockupPreviewDto>
{
    private const int _defaultWidth = 400;
    private const int _defaultSlots = 3;

    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = templateRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = assetRepository;
    private readonly IScreenshotSizeSource _screenshotSizeSource = screenshotSizeSource;
    private readonly IMockupRenderer _mockupRenderer = mockupRenderer;
    private readonly IStudioAssetReader _studioAssetReader = studioAssetReader;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly StudioOptions _studioOptions = studioOptions;

    /// <inheritdoc/>
    public async Task<Response<MockupPreviewDto>> Handle(RenderMockupPreviewQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var template = await _templateRepository.GetMockupSnapshotAsync(request.OrganizationId, request.TemplateId, cancellationToken);

        if (template == null)
            return Response<MockupPreviewDto>.Error(null, MessageKey.MockupTemplateNotFound);

        var locale = request.Locale == null ? template.PrimaryLocale : template.Locales.FirstOrDefault(l => string.Equals(l, request.Locale, StringComparison.OrdinalIgnoreCase));

        if (locale == null)
            return Response<MockupPreviewDto>.Error(null, MessageKey.MockupLocaleInvalid);

        var spec = MockupSpecRules.Upgrade(request.Spec ?? template.Spec);
        var screenshotClass = _screenshotSizeSource.Current.Find(request.ClassKey ?? spec.Canvas.ClassKey);

        if (screenshotClass == null)
            return Response<MockupPreviewDto>.Error(null, MessageKey.MockupClassUnknown);

        var slots = (request.SlotOrders ?? [.. Enumerable.Range(1, Math.Min(_defaultSlots, template.SlotCount))]).Where(s => s <= template.SlotCount).Distinct().ToList();

        if (screenshotClass.SingleImage)
            slots = [1];

        var images = await LoadImagesAsync(request.OrganizationId, template, spec, cancellationToken);

        if (images == null)
            return Response<MockupPreviewDto>.Error(null, MessageKey.StorageNotConfigured);

        var width = Math.Clamp(request.MaxWidth ?? _defaultWidth, RenderMockupPreviewQuery.MinWidth, Math.Max(RenderMockupPreviewQuery.MinWidth, _studioOptions.PreviewMaxWidth));
        var scale = Math.Min(1f, width / (float)screenshotClass.ExportSize.Width);

        var previews = slots.Select(slot =>
        {
            var (renderRequest, notes) = MockupRenderPlanner.Build(template, spec, screenshotClass, screenshotClass.ExportSize, locale, slot, images, scale, failWhenTooSmall: false);
            var result = _mockupRenderer.Render(renderRequest);
            var issues = notes.Concat(result.Warnings);

            if (result.Failure != null)
                issues = issues.Append(result.Failure);

            return new MockupPreviewImageDto
            {
                SlotOrder = slot,
                Width = result.Width,
                Height = result.Height,
                Url = result.Png == null ? null : "data:image/png;base64," + Convert.ToBase64String(result.Png),
                Warnings = [.. issues.Select(i => MockupWarningDto.From(i, screenshotClass.Key, locale, slot))],
            };
        }).ToList();

        return Response<MockupPreviewDto>.Success(new MockupPreviewDto
        {
            ClassKey = screenshotClass.Key,
            Locale = locale,
            Images = previews,
            DurationMs = stopwatch.ElapsedMilliseconds,
        });
    }

    /// <summary>
    /// Encoded images of the template by asset id; null when it has images but no bucket is configured.
    /// </summary>
    private async Task<Dictionary<long, byte[]>> LoadImagesAsync(int organizationId, MockupTemplateSnapshot template, MockupSpec spec, CancellationToken cancellationToken)
    {
        var keys = await _assetRepository.GetUploadedAssetKeysAsync(organizationId, template.AssetIds(spec), cancellationToken);

        if (keys.Count == 0)
            return [];

        if (!_fileStorage.IsConfigured)
            return null;

        return await _studioAssetReader.ReadStudioImagesAsync(keys, cancellationToken);
    }
}
