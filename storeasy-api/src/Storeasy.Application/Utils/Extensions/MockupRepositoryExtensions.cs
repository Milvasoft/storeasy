using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Stores;
using Storeasy.Domain.Studio;
using System.Text.Json;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Reads shared by the mockup studio's handlers and its export job.
/// </summary>
public static class MockupRepositoryExtensions
{
    /// <summary>
    /// The template of the organization with its slots and captions, or null.
    /// </summary>
    /// <param name="templateRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="templateId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<MockupTemplateSnapshot> GetMockupSnapshotAsync(this IStoreasyRepositoryBase<MockupTemplate> templateRepository,
                                                                      int organizationId,
                                                                      int templateId,
                                                                      CancellationToken cancellationToken)
        => templateRepository.GetFirstOrDefaultAsync(t => t.Id == templateId && t.OrganizationId == organizationId,
                                                     projection: t => new MockupTemplateSnapshot
                                                     {
                                                         Id = t.Id,
                                                         AppId = t.AppId,
                                                         Name = t.Name,
                                                         PrimaryLocale = t.PrimaryLocale,
                                                         Locales = t.Locales,
                                                         SlotCount = t.SlotCount,
                                                         Version = t.Version,
                                                         Spec = t.Spec,
                                                         Slots = t.Slots.Select(s => new MockupSlot { Id = s.Id, Order = s.Order, Family = s.Family, Locale = s.Locale, AssetId = s.AssetId }).ToList(),
                                                         Captions = t.Captions.Select(c => new MockupCaption { Id = c.Id, Locale = c.Locale, SlotOrder = c.SlotOrder, Title = c.Title, Subtitle = c.Subtitle }).ToList(),
                                                     },
                                                     cancellationToken: cancellationToken);

    /// <summary>
    /// Object keys of the organization's uploaded assets among <paramref name="assetIds"/> (missing ones are left out).
    /// </summary>
    /// <param name="assetRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="assetIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<Dictionary<long, string>> GetUploadedAssetKeysAsync(this IStoreasyRepositoryBase<Asset> assetRepository,
                                                                                 int organizationId,
                                                                                 IReadOnlyCollection<long> assetIds,
                                                                                 CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
            return [];

        var assets = await assetRepository.GetAllAsync(a => a.OrganizationId == organizationId && assetIds.Contains(a.Id) && a.Status == AssetStatus.Uploaded,
                                                       projection: a => new Asset { Id = a.Id, ObjectKey = a.ObjectKey },
                                                       cancellationToken: cancellationToken);

        return assets.ToDictionary(a => a.Id, a => a.ObjectKey);
    }

    /// <summary>
    /// Stores the app has records in; empty when the app does not exist in the organization.
    /// </summary>
    /// <param name="appRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="appId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<List<StoreKind>> GetAppStoresAsync(this IStoreasyRepositoryBase<App> appRepository, int organizationId, int appId, CancellationToken cancellationToken)
        => await appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == organizationId,
                                                      projection: a => a.StoreApps.Select(s => s.Store).Distinct().ToList(),
                                                      cancellationToken: cancellationToken) ?? [];

    /// <summary>
    /// Reads the images of <paramref name="keys"/> through the studio cache.
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="keys">Object keys by asset id.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<Dictionary<long, byte[]>> ReadStudioImagesAsync(this IStudioAssetReader reader, IReadOnlyDictionary<long, string> keys, CancellationToken cancellationToken)
    {
        var images = new Dictionary<long, byte[]>();

        foreach (var (assetId, key) in keys)
            images[assetId] = await reader.ReadAsync(key, cancellationToken);

        return images;
    }

    /// <summary>
    /// The manifest of a finished export job of the organization, or the message key of why there is none
    /// (<see cref="MessageKey.MockupExportNotFound"/>, <see cref="MessageKey.MockupExportNotReady"/>).
    /// </summary>
    /// <param name="jobRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="jobId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<(MockupExportManifest Manifest, string ErrorKey)> GetMockupExportManifestAsync(this IStoreasyRepositoryBase<StoreJob> jobRepository,
                                                                                                           int organizationId,
                                                                                                           long jobId,
                                                                                                           CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetFirstOrDefaultAsync(j => j.Id == jobId && j.OrganizationId == organizationId && j.Kind == StoreJobKind.MockupExport,
                                                             projection: j => new StoreJob { Id = j.Id, Status = j.Status, Result = j.Result },
                                                             cancellationToken: cancellationToken);

        if (job == null)
            return (null, MessageKey.MockupExportNotFound);

        if (job.Status != StoreJobStatus.Succeeded || string.IsNullOrWhiteSpace(job.Result))
            return (null, MessageKey.MockupExportNotReady);

        return (JsonSerializer.Deserialize<MockupExportManifest>(job.Result), null);
    }
}
