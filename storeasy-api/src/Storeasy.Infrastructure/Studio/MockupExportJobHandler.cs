using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Studio;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Storeasy.Infrastructure.Studio;

/// <summary>
/// The <see cref="StoreJobKind.MockupExport"/> job (docs 12.11): renders every selected class, locale and slot of a
/// template, stores each PNG as an asset (purpose MockupOutput), zips them with the manifest, and returns the
/// <see cref="MockupExportManifest"/> as the job result for the screenshot upload plan. Steps on the activity hub:
/// <c>assets.load</c>, one <c>render.size</c> per class with per image progress, <c>asset.store</c>, <c>zip.store</c>. An
/// image that cannot be produced (raw screenshot too small or unreadable) fails its class's step and is listed in the
/// manifest's failures; the other images still count. When the job is cancelled or fails, the objects it already wrote are
/// deleted. Nothing is written to a store.
/// </summary>
/// <param name="templateRepository"></param>
/// <param name="assetRepository"></param>
/// <param name="screenshotSizeSource"></param>
/// <param name="mockupRenderer"></param>
/// <param name="studioAssetReader"></param>
/// <param name="fileStorage"></param>
/// <param name="timeProvider"></param>
public sealed class MockupExportJobHandler(IStoreasyRepositoryBase<MockupTemplate> templateRepository,
                                           IStoreasyRepositoryBase<Asset> assetRepository,
                                           IScreenshotSizeSource screenshotSizeSource,
                                           IMockupRenderer mockupRenderer,
                                           IStudioAssetReader studioAssetReader,
                                           IFileStorage fileStorage,
                                           TimeProvider timeProvider) : IStoreJobHandler
{
    private const string _pngType = "image/png";
    private const string _zipType = "application/zip";

    private static readonly JsonSerializerOptions _zipJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = templateRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = assetRepository;
    private readonly IScreenshotSizeSource _screenshotSizeSource = screenshotSizeSource;
    private readonly IMockupRenderer _mockupRenderer = mockupRenderer;
    private readonly IStudioAssetReader _studioAssetReader = studioAssetReader;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.MockupExport;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<MockupExportPayload>() ?? throw new StoreJobException("The job has no template.", transient: false);

        if (!_fileStorage.IsConfigured)
            throw new StoreJobException("No bucket is configured.", transient: false);

        var template = await _templateRepository.GetMockupSnapshotAsync(context.OrganizationId, payload.TemplateId, cancellationToken)
                       ?? throw new StoreJobException("The template was deleted.", transient: false);

        var catalog = _screenshotSizeSource.Current;
        var spec = MockupSpecRules.Upgrade(template.Spec);
        var written = new List<string>();
        var savedAssetIds = new List<long>();
        var tempZip = Path.Combine(Path.GetTempPath(), $"storeasy-mockups-{context.JobId}-{Guid.NewGuid():N}.zip");

        try
        {
            // 1. Raw screenshots and the background image.
            var loadStep = await context.BeginStepAsync("assets.load", StepDirection.Inbound, cancellationToken: cancellationToken);
            var keys = await _assetRepository.GetUploadedAssetKeysAsync(context.OrganizationId, template.AssetIds(spec), cancellationToken);
            var images = await _studioAssetReader.ReadStudioImagesAsync(keys, cancellationToken);

            await context.EndStepAsync(loadStep, StoreJobStepStatus.Succeeded, $"{images.Count} images.", cancellationToken);

            var manifest = new MockupExportManifest
            {
                JobId = context.JobId,
                TemplateId = template.Id,
                AppId = template.AppId,
                TemplateVersion = template.Version,
                CatalogVersion = catalog.CatalogVersion,
            };

            var produced = new List<(Asset Asset, MockupExportSet Set, MockupExportImage Image)>();

            await using (var zipStream = new FileStream(tempZip, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    // 2. Render class by class, required classes first (the order of the plan).
                    foreach (var target in payload.Targets)
                    {
                        var screenshotClass = catalog.Find(target.ClassKey);

                        if (screenshotClass == null)
                            continue;

                        await RenderClassAsync(context, template, spec, screenshotClass, new PixelSize(target.Width, target.Height), payload.Locales, images, zip, manifest, produced, written, cancellationToken);
                    }

                    if (produced.Count == 0)
                        throw new StoreJobException($"No image could be produced ({manifest.Failures.Count} failed).", transient: false);

                    // 3. Asset rows of every image, in one write.
                    var storeStep = await context.BeginStepAsync("asset.store", StepDirection.Internal, total: produced.Count, cancellationToken: cancellationToken);

                    await _assetRepository.AddRangeAsync([.. produced.Select(p => p.Asset)], cancellationToken);

                    foreach (var (asset, _, image) in produced)
                        image.AssetId = asset.Id;

                    savedAssetIds.AddRange(produced.Select(p => p.Asset.Id));

                    await context.EndStepAsync(storeStep, StoreJobStepStatus.Succeeded, $"{produced.Count} images stored.", cancellationToken);

                    manifest.ImageCount = produced.Count;
                    manifest.TotalBytes = produced.Sum(p => p.Asset.Size);
                    manifest.CreatedDate = Now();

                    var entry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);

                    await using (var manifestStream = entry.Open())
                        await JsonSerializer.SerializeAsync(manifestStream, manifest, _zipJson, cancellationToken);
                }

                // 4. The zip.
                var zipStep = await context.BeginStepAsync("zip.store", StepDirection.Outbound, total: zipStream.Length, cancellationToken: cancellationToken);
                var zipAsset = NewAsset(context.OrganizationId, $"mockups-{context.JobId}.zip", _zipType, zipStream.Length, await Sha256Async(zipStream, cancellationToken));

                zipStream.Position = 0;

                await _fileStorage.WriteAsync(zipAsset.ObjectKey, zipStream, _zipType, cancellationToken);
                written.Add(zipAsset.ObjectKey);

                await _assetRepository.AddAsync(zipAsset, cancellationToken);

                savedAssetIds.Add(zipAsset.Id);

                manifest.ZipAssetId = zipAsset.Id;

                await context.EndStepAsync(zipStep, StoreJobStepStatus.Succeeded, $"{zipStream.Length / 1024} KB.", cancellationToken);
            }

            await _templateRepository.ExecuteUpdateAsync(template.Id,
                                                         new SetPropertyBuilder<MockupTemplate>().SetPropertyValue(t => t.LastExportJobId, (long?)context.JobId)
                                                                                                 .SetPropertyValue(t => t.LastExportDate, (DateTime?)manifest.CreatedDate)
                                                                                                 .SetPropertyValue(t => t.LastExportImageCount, (int?)manifest.ImageCount),
                                                         cancellationToken);

            var failed = manifest.Failures.Count == 0 ? string.Empty : $", {manifest.Failures.Count} failed";

            return new StoreJobOutcome($"{manifest.ImageCount} mockups exported from template '{template.Name}'{failed}.", manifest);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await DeleteWrittenAsync(written);

            if (savedAssetIds.Count > 0)
                await _assetRepository.ExecuteDeleteAsync(a => savedAssetIds.Contains(a.Id), cancellationToken: CancellationToken.None);

            throw;
        }
        finally
        {
            File.Delete(tempZip);
        }
    }

    private async Task RenderClassAsync(IStoreJobContext context,
                                        Application.Utils.Models.MockupTemplateSnapshot template,
                                        MockupSpec spec,
                                        ScreenshotClass screenshotClass,
                                        PixelSize size,
                                        List<string> locales,
                                        IReadOnlyDictionary<long, byte[]> images,
                                        ZipArchive zip,
                                        MockupExportManifest manifest,
                                        List<(Asset Asset, MockupExportSet Set, MockupExportImage Image)> produced,
                                        List<string> written,
                                        CancellationToken cancellationToken)
    {
        var perLocale = MockupExportRules.ImagesPerLocale(screenshotClass, template.SlotCount);
        var total = perLocale * locales.Count;
        var step = await context.BeginStepAsync("render.size", StepDirection.Internal, $"{screenshotClass.Store} {screenshotClass.LabelEn} {size}", total, cancellationToken);
        var done = 0;
        var failures = 0;
        var storeClassKey = MediaClasses.All.FirstOrDefault(c => c.Kind == MediaKind.Screenshot && c.RuleId == screenshotClass.Key)?.ClassKey;

        foreach (var locale in locales)
        {
            var set = new MockupExportSet { Store = screenshotClass.Store, ClassKey = screenshotClass.Key, StoreClassKey = storeClassKey, Locale = locale, Width = size.Width, Height = size.Height };

            for (var slot = 1; slot <= perLocale; slot++)
            {
                var (request, notes) = MockupRenderPlanner.Build(template, spec, screenshotClass, size, locale, slot, images, 1, failWhenTooSmall: true);
                var result = _mockupRenderer.Render(request);

                manifest.Warnings.AddRange(notes.Concat(result.Warnings).Select(w => Issue(screenshotClass, locale, slot, w)));

                if (result.Failure != null)
                {
                    failures++;
                    manifest.Failures.Add(Issue(screenshotClass, locale, slot, result.Failure));
                }
                else
                {
                    var zipPath = MockupExportRules.ZipEntryPath(screenshotClass.Store, screenshotClass.Key, locale, slot);
                    var asset = NewAsset(context.OrganizationId, MockupExportRules.OutputFileName(screenshotClass.Key, locale, slot), _pngType, result.Png.Length, Convert.ToHexStringLower(SHA256.HashData(result.Png)));

                    using (var png = new MemoryStream(result.Png, writable: false))
                        await _fileStorage.WriteAsync(asset.ObjectKey, png, _pngType, cancellationToken);

                    written.Add(asset.ObjectKey);

                    var entry = zip.CreateEntry(zipPath, CompressionLevel.NoCompression);

                    await using (var entryStream = entry.Open())
                        await entryStream.WriteAsync(result.Png, cancellationToken);

                    var image = new MockupExportImage
                    {
                        SlotOrder = slot,
                        Size = result.Png.Length,
                        Sha256 = asset.Sha256,
                        Md5 = Convert.ToHexStringLower(MD5.HashData(result.Png)),
                        ZipPath = zipPath,
                    };

                    set.Images.Add(image);
                    produced.Add((asset, set, image));
                }

                await context.ReportProgressAsync(step, ++done, cancellationToken);
            }

            if (set.Images.Count > 0)
                manifest.Sets.Add(set);
        }

        await context.EndStepAsync(step,
                                   failures == 0 ? StoreJobStepStatus.Succeeded : StoreJobStepStatus.Failed,
                                   failures == 0 ? $"{done} images." : $"{done - failures} of {done} images; {failures} failed.",
                                   cancellationToken);
    }

    private Asset NewAsset(int organizationId, string fileName, string contentType, long size, string sha256) => new()
    {
        OrganizationId = organizationId,
        Purpose = AssetPurpose.MockupOutput,
        ObjectKey = AssetRules.ObjectKey(organizationId, AssetPurpose.MockupOutput, fileName, Guid.NewGuid()),
        FileName = fileName,
        ContentType = contentType,
        Size = size,
        Sha256 = sha256,
        Status = AssetStatus.Uploaded,
        UploadDate = Now(),
    };

    private static MockupExportIssue Issue(ScreenshotClass screenshotClass, string locale, int slot, MockupRenderIssue issue) => new()
    {
        ClassKey = screenshotClass.Key,
        Locale = locale,
        SlotOrder = slot,
        Code = issue.Code,
        Message = issue.Message,
    };

    private static async Task<string> Sha256Async(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;

        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    /// <summary>
    /// Best effort removal of the objects of a failed or cancelled attempt (their asset rows were never written or roll
    /// back with the job).
    /// </summary>
    private async Task DeleteWrittenAsync(List<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await _fileStorage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception)
            {
                // The object stays orphaned; the bucket's lifecycle rule or a later cleanup removes it.
            }
        }
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
