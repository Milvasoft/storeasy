using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Assets;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Stores;
using System.Text;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Repository helpers of the Microsoft Store features: loading a product of the organization and opening the secret of
/// its connection for a store read inside the request.
/// </summary>
public static class MsStoreRepositoryExtensions
{
    /// <summary>
    /// Loads the Microsoft Store product of <paramref name="storeAppId"/> in <paramref name="organizationId"/>; null when
    /// the record does not exist, belongs to another organization or another store.
    /// </summary>
    /// <param name="productRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<MsProductContext> GetMsProductAsync(this IStoreasyRepositoryBase<MsProduct> productRepository, int organizationId, int storeAppId, CancellationToken cancellationToken)
        => productRepository.GetFirstOrDefaultAsync(p => p.StoreAppId == storeAppId && p.OrganizationId == organizationId && p.StoreApp.Store == StoreKind.Microsoft && !p.StoreApp.App.IsDeleted,
                                                    projection: p => new MsProductContext
                                                    {
                                                        StoreAppId = p.StoreAppId,
                                                        AppId = p.StoreApp.AppId,
                                                        ProductId = p.StoreApp.ExternalId,
                                                        ConnectionId = p.StoreApp.StoreConnectionId,
                                                        Kind = p.Kind,
                                                        FirstSubmissionDone = p.FirstSubmissionDone,
                                                        HasMandatoryUpdate = p.HasMandatoryUpdate,
                                                        PricingV2 = p.PricingV2,
                                                        IsReady = p.IsReady,
                                                        PendingSubmissionId = p.PendingSubmissionId,
                                                    },
                                                    cancellationToken: cancellationToken);

    /// <summary>
    /// Whether the organization has a Microsoft Store product for <paramref name="storeAppId"/>.
    /// </summary>
    /// <param name="productRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<bool> MsProductExistsAsync(this IStoreasyRepositoryBase<MsProduct> productRepository, int organizationId, int storeAppId, CancellationToken cancellationToken)
    {
        var id = await productRepository.GetFirstOrDefaultAsync(p => p.StoreAppId == storeAppId && p.OrganizationId == organizationId && p.StoreApp.Store == StoreKind.Microsoft && !p.StoreApp.App.IsDeleted,
                                                                projection: p => (int?)p.Id,
                                                                cancellationToken: cancellationToken);

        return id.HasValue;
    }

    /// <summary>
    /// Microsoft connections whose client secret expires before <paramref name="until"/> (expired ones included), of
    /// one organization or of all when <paramref name="organizationId"/> is null. For the expiry reminders.
    /// </summary>
    /// <param name="connectionRepository"></param>
    /// <param name="until"></param>
    /// <param name="now"></param>
    /// <param name="organizationId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<List<MsExpiringConnectionDto>> GetExpiringMsConnectionsAsync(this IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                                                                          DateTime until,
                                                                                          DateTime now,
                                                                                          int? organizationId,
                                                                                          CancellationToken cancellationToken)
    {
        var connections = await connectionRepository.GetAllAsync(c => c.Store == StoreKind.Microsoft
                                                                      && c.SecretExpiryDate != null
                                                                      && c.SecretExpiryDate <= until
                                                                      && (organizationId == null || c.OrganizationId == organizationId),
                                                                 projection: c => new MsExpiringConnectionDto
                                                                 {
                                                                     ConnectionId = c.Id,
                                                                     OrganizationId = c.OrganizationId,
                                                                     Name = c.Name,
                                                                     CredentialExpiryDate = c.SecretExpiryDate.Value,
                                                                 },
                                                                 cancellationToken: cancellationToken);

        foreach (var connection in connections)
            connection.DaysLeft = (int)Math.Floor((connection.CredentialExpiryDate - now).TotalDays);

        return [.. connections.OrderBy(c => c.CredentialExpiryDate)];
    }

    /// <summary>
    /// Opens the secret of a Microsoft connection of the organization for one store read; null when the connection does
    /// not exist. Keep the result only for the calls of the request.
    /// </summary>
    /// <param name="connectionRepository"></param>
    /// <param name="secretProtector"></param>
    /// <param name="organizationId"></param>
    /// <param name="connectionId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<string> OpenMsSecretAsync(this IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                                       ISecretProtector secretProtector,
                                                       int organizationId,
                                                       int? connectionId,
                                                       CancellationToken cancellationToken)
    {
        if (connectionId == null)
            return null;

        var connection = await connectionRepository.GetFirstOrDefaultAsync(c => c.Id == connectionId && c.OrganizationId == organizationId && c.Store == StoreKind.Microsoft,
                                                                           projection: c => new StoreConnection { Id = c.Id, EncryptedSecret = c.EncryptedSecret, EncryptedDataKey = c.EncryptedDataKey, KeyVersion = c.KeyVersion },
                                                                           cancellationToken: cancellationToken);

        if (connection?.EncryptedSecret == null)
            return null;

        var secret = secretProtector.Unprotect(new ProtectedSecret(connection.EncryptedSecret, connection.EncryptedDataKey, connection.KeyVersion),
                                               StoreSecretContext.Connection(organizationId, StoreKind.Microsoft));

        try
        {
            return Encoding.UTF8.GetString(secret);
        }
        finally
        {
            Array.Clear(secret);
        }
    }

    /// <summary>
    /// Resolves the uploaded assets a change adds to their paths in the submission's ZIP archive. Returns null when one
    /// is missing, belongs to another organization, is not uploaded yet or is not of the right kind (PNG images; MSIX
    /// packages uploaded as build artifacts).
    /// </summary>
    /// <param name="assetRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="assetIds"></param>
    /// <param name="packages">Whether the assets are packages (otherwise listing images).</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<Dictionary<long, string>> GetMsZipEntryNamesAsync(this IStoreasyRepositoryBase<Asset> assetRepository,
                                                                              int organizationId,
                                                                              IReadOnlyCollection<long> assetIds,
                                                                              bool packages,
                                                                              CancellationToken cancellationToken)
    {
        if (assetIds.Count == 0)
            return [];

        var ids = assetIds.Distinct().ToList();
        var assets = await assetRepository.GetAllAsync(a => ids.Contains(a.Id) && a.OrganizationId == organizationId && a.Status == AssetStatus.Uploaded,
                                                       projection: a => new Asset { Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, Purpose = a.Purpose },
                                                       cancellationToken: cancellationToken);

        var valid = assets.Where(a => packages
                                     ? a.Purpose == AssetPurpose.BuildArtifact && MsSubmissionJson.PackageExtensions.Contains(Path.GetExtension(a.FileName ?? string.Empty))
                                     : a.Purpose != AssetPurpose.BuildArtifact && string.Equals(a.ContentType, "image/png", StringComparison.OrdinalIgnoreCase))
                          .ToList();

        if (valid.Count != ids.Count)
            return null;

        return valid.ToDictionary(a => a.Id, a => MsSubmissionJson.ZipEntryName(packages ? MsSubmissionJson.PackagesFolder : MsSubmissionJson.ImagesFolder, a.Id, a.FileName));
    }

    /// <summary>
    /// Builds the planning context of a product: storeasy's row of the pending submission the content came from, the
    /// add-on's writability and the names already taken.
    /// </summary>
    /// <param name="submissionRepository"></param>
    /// <param name="flightRepository"></param>
    /// <param name="addonRepository"></param>
    /// <param name="product"></param>
    /// <param name="target"></param>
    /// <param name="submissionId"></param>
    /// <param name="addonStoreId"></param>
    /// <param name="assetFileNames"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<MsPlanContext> GetMsPlanContextAsync(this IStoreasyRepositoryBase<MsSubmission> submissionRepository,
                                                                  IStoreasyRepositoryBase<MsFlight> flightRepository,
                                                                  IStoreasyRepositoryBase<MsAddon> addonRepository,
                                                                  MsProductContext product,
                                                                  MsSubmissionTarget target,
                                                                  string submissionId,
                                                                  string addonStoreId,
                                                                  IReadOnlyDictionary<long, string> assetFileNames,
                                                                  CancellationToken cancellationToken)
    {
        var pending = submissionId == null
            ? null
            : await submissionRepository.GetFirstOrDefaultAsync(s => s.StoreAppId == product.StoreAppId && s.Target == target && s.ExternalId == submissionId,
                                                                projection: s => new MsPendingSubmissionInfo { CreatedByStoreasy = s.CreatedByStoreasy, ModifiedOutsideApi = s.ModifiedOutsideApi, ContentHash = s.ContentHash },
                                                                cancellationToken: cancellationToken);

        var flightNames = await flightRepository.GetAllAsync(f => f.StoreAppId == product.StoreAppId, projection: f => new MsFlight { FriendlyName = f.FriendlyName }, cancellationToken: cancellationToken);
        var addons = await addonRepository.GetAllAsync(a => a.StoreAppId == product.StoreAppId,
                                                       projection: a => new MsAddon { StoreId = a.StoreId, ProductId = a.ProductId, ApiWritable = a.ApiWritable, AssistedReason = a.AssistedReason },
                                                       cancellationToken: cancellationToken);

        var addon = addons.FirstOrDefault(a => a.StoreId == addonStoreId);

        return new MsPlanContext
        {
            Kind = product.Kind,
            ProductId = product.ProductId,
            FirstSubmissionDone = product.FirstSubmissionDone,
            HasMandatoryUpdate = product.HasMandatoryUpdate,
            PricingV2 = product.PricingV2,
            Pending = pending,
            AddonWritable = addon?.ApiWritable ?? true,
            AddonAssistedReason = addon?.AssistedReason,
            FlightNames = [.. flightNames.Select(f => f.FriendlyName).Where(n => n != null)],
            AddonProductIds = [.. addons.Select(a => a.ProductId).Where(p => p != null)],
            AssetFileNames = assetFileNames ?? new Dictionary<long, string>(),
        };
    }
}
