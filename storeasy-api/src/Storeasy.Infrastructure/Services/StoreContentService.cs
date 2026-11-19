using Milvasoft.Core.Abstractions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Rules;
using Storeasy.Domain.Stores;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IStoreContentService"/>. The connection secret is opened for one call and dropped; store failures
/// are logged (redacted) and returned as <see cref="MessageKey.StoreUnreachable"/> (transient) or
/// <see cref="MessageKey.StoreRejected"/>.
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="connectionRepository"></param>
/// <param name="assetRepository"></param>
/// <param name="gateways"></param>
/// <param name="secretProtector"></param>
/// <param name="fileStorage"></param>
/// <param name="logger"></param>
public sealed class StoreContentService(IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                        IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                        IStoreasyRepositoryBase<Asset> assetRepository,
                                        IEnumerable<IStoreContentGateway> gateways,
                                        ISecretProtector secretProtector,
                                        IFileStorage fileStorage,
                                        IMilvaLogger logger) : IStoreContentService
{
    private const int _headerBytes = 64 * 1024;

    private static readonly Dictionary<string, string> _alpha3ToAlpha2 = BuildRegionMap();

    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = assetRepository;
    private readonly IEnumerable<IStoreContentGateway> _gateways = gateways;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IMilvaLogger _logger = logger;

    /// <inheritdoc/>
    public async Task<StoreAppContext> GetStoreAppAsync(int organizationId, int storeAppId, CancellationToken cancellationToken)
    {
        var row = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == storeAppId && s.OrganizationId == organizationId && !s.App.IsDeleted,
                                                                   projection: s => new { s.Id, s.AppId, AppName = s.App.Name, s.Store, s.ExternalId, s.StoreConnectionId, s.App.Profile },
                                                                   cancellationToken: cancellationToken);

        return row == null ? null : new StoreAppContext(organizationId, row.Id, row.AppId, row.AppName, row.Store, row.ExternalId, row.StoreConnectionId, row.Profile);
    }

    /// <inheritdoc/>
    public async Task<StoreAppContext> GetStoreAppOfAppAsync(int organizationId, int appId, StoreKind store, CancellationToken cancellationToken)
    {
        var row = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.AppId == appId && s.Store == store && s.OrganizationId == organizationId && !s.App.IsDeleted,
                                                                   projection: s => new { s.Id, s.AppId, AppName = s.App.Name, s.Store, s.ExternalId, s.StoreConnectionId, s.App.Profile },
                                                                   cancellationToken: cancellationToken);

        return row == null ? null : new StoreAppContext(organizationId, row.Id, row.AppId, row.AppName, row.Store, row.ExternalId, row.StoreConnectionId, row.Profile);
    }

    /// <inheritdoc/>
    public async Task<StoreContentRead> ReadAsync(StoreAppContext app, ContentParts parts, string versionId, CancellationToken cancellationToken)
    {
        var (gateway, secret, errorKey) = await OpenAsync(app, cancellationToken);

        if (errorKey != null)
            return new StoreContentRead(null, errorKey);

        try
        {
            return new StoreContentRead(await gateway.ReadAsync(new StoreContentTarget(secret, app.ExternalId, versionId), parts, cancellationToken));
        }
        catch (StoreCallException ex)
        {
            return Failure(app, ex, (key, detail) => new StoreContentRead(null, key, detail));
        }
    }

    /// <inheritdoc/>
    public async Task<StoreOptionsRead> ListOptionsAsync(StoreAppContext app, StoreOptionKind kind, string filter, CancellationToken cancellationToken)
    {
        var (gateway, secret, errorKey) = await OpenAsync(app, cancellationToken);

        if (errorKey != null)
            return new StoreOptionsRead([], errorKey);

        try
        {
            return new StoreOptionsRead(await gateway.ListOptionsAsync(new StoreContentTarget(secret, app.ExternalId), kind, filter, cancellationToken));
        }
        catch (StoreCallException ex)
        {
            return Failure(app, ex, (key, detail) => new StoreOptionsRead([], key, detail));
        }
    }

    /// <inheritdoc/>
    public async Task<List<StoreAppContext>> GetStoreAppsAsync(int organizationId, int? appId, CancellationToken cancellationToken)
    {
        var rows = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == organizationId && !s.App.IsDeleted && (appId == null ? s.App.HiddenDate == null : s.AppId == appId),
                                                         projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, App = new App { Name = s.App.Name, Profile = s.App.Profile }, Store = s.Store, ExternalId = s.ExternalId, StoreConnectionId = s.StoreConnectionId },
                                                         cancellationToken: cancellationToken);

        return [.. rows.OrderBy(r => r.AppId).ThenBy(r => r.Store).Select(r => new StoreAppContext(organizationId, r.Id, r.AppId, r.App.Name, r.Store, r.ExternalId, r.StoreConnectionId, r.App.Profile))];
    }

    /// <inheritdoc/>
    public async Task<List<InspectedAsset>> InspectAssetsAsync(int organizationId, IReadOnlyCollection<long> assetIds, CancellationToken cancellationToken)
    {
        if (assetIds == null || assetIds.Count == 0)
            return [];

        var ids = assetIds.Distinct().ToList();
        var assets = await _assetRepository.GetAllAsync(a => ids.Contains(a.Id) && a.OrganizationId == organizationId && a.Status == AssetStatus.Uploaded,
                                                        projection: a => new Asset { Id = a.Id, ObjectKey = a.ObjectKey, FileName = a.FileName, ContentType = a.ContentType },
                                                        cancellationToken: cancellationToken);

        var inspected = new List<InspectedAsset>();

        foreach (var asset in ids.Select(id => assets.FirstOrDefault(a => a.Id == id)).Where(a => a != null))
            inspected.Add(await InspectAsync(asset, cancellationToken));

        return inspected;
    }

    private async Task<InspectedAsset> InspectAsync(Asset asset, CancellationToken cancellationToken)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var header = new MemoryStream();
        var buffer = new byte[81920];
        long size = 0;

        await using (var stream = await _fileStorage.OpenReadAsync(asset.ObjectKey, cancellationToken))
        {
            int read;

            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                md5.AppendData(buffer, 0, read);
                sha256.AppendData(buffer, 0, read);

                if (header.Length < _headerBytes)
                    header.Write(buffer, 0, (int)Math.Min(read, _headerBytes - header.Length));

                size += read;
            }
        }

        return new InspectedAsset(asset.Id,
                                  asset.ObjectKey,
                                  asset.FileName,
                                  asset.ContentType,
                                  size,
                                  Convert.ToHexStringLower(md5.GetHashAndReset()),
                                  Convert.ToHexStringLower(sha256.GetHashAndReset()),
                                  ImageHeader.Read(header.ToArray()));
    }

    /// <inheritdoc/>
    public RuleSubject Subject(StoreAppContext app, StoreContentSnapshot snapshot)
    {
        var profile = app.Profile ?? new AppProfile();
        var apple = app.Store == StoreKind.Apple;

        IReadOnlyCollection<string> platforms = profile.Platforms ?? (apple ? [ApplePlatform(snapshot?.Platform)] : app.Store == StoreKind.Google ? ["android"] : ["windows"]);
        var families = profile.DeviceFamilies ?? DetectFamilies(app.Store, snapshot);
        var live = profile.IsLive ?? snapshot?.HasLiveVersion;

        return new RuleSubject
        {
            Store = app.Store,
            Platforms = platforms,
            DeviceFamilies = families,
            Regions = profile.Regions ?? Regions(snapshot),
            DeveloperRegion = profile.DeveloperRegion,
            AppType = profile.AppType,
            PackageType = apple ? "ipa" : app.Store == StoreKind.Google ? "aab" : null,
            Distribution = live == null ? null : live.Value ? "update" : "new-app",
            Features = profile.Features,
            BuildSdkName = apple ? "iOS" : null,
            BuildSdkVersion = profile.BuildSdkVersion,
        };
    }

    /// <summary>
    /// ISO 3166-1 alpha-2 code of an App Store territory (alpha-3); the code itself when it is already alpha-2 or unknown.
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static string ToAlpha2(string code) => code != null && _alpha3ToAlpha2.TryGetValue(code, out var alpha2) ? alpha2 : code;

    private async Task<(IStoreContentGateway Gateway, string Secret, string ErrorKey)> OpenAsync(StoreAppContext app, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == app.Store);

        if (gateway == null)
            return (null, null, MessageKey.StoreNotSupported);

        if (app.ConnectionId == null)
            return (null, null, MessageKey.StoreAppNotConnected);

        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == app.ConnectionId && c.OrganizationId == app.OrganizationId,
                                                                            projection: c => new { c.Store, c.EncryptedSecret, c.EncryptedDataKey, c.KeyVersion },
                                                                            cancellationToken: cancellationToken);

        if (connection?.EncryptedSecret == null || connection.EncryptedSecret.Length == 0)
            return (null, null, MessageKey.StoreAppNotConnected);

        byte[] secret = null;

        try
        {
            secret = _secretProtector.Unprotect(new ProtectedSecret(connection.EncryptedSecret, connection.EncryptedDataKey, connection.KeyVersion),
                                                StoreSecretContext.Connection(app.OrganizationId, connection.Store));

            return (gateway, Encoding.UTF8.GetString(secret), null);
        }
        catch (CryptographicException)
        {
            return (null, null, MessageKey.StoreAppNotConnected);
        }
        finally
        {
            if (secret != null)
                Array.Clear(secret);
        }
    }

    private T Failure<T>(StoreAppContext app, StoreCallException exception, Func<string, string, T> create)
    {
        _logger?.Warning("Store content read of {Store} record {StoreAppId} failed: {Error}", app.Store, app.StoreAppId, SecretRedactor.Redact(exception.Message));

        return create(exception.Transient ? MessageKey.StoreUnreachable : MessageKey.StoreRejected, SecretRedactor.RedactAndTrim(exception.Message, 300));
    }

    private static string ApplePlatform(string platform) => platform switch
    {
        "MAC_OS" => "macos",
        "TV_OS" => "tvos",
        "VISION_OS" => "visionos",
        _ => "ios",
    };

    /// <summary>
    /// Device families when the profile does not say: iPhone (phone on Play) always, iPad and tablets only when the store
    /// already holds media for them. iPad requirements therefore appear only for apps that support iPad.
    /// </summary>
    private static List<string> DetectFamilies(StoreKind store, StoreContentSnapshot snapshot)
    {
        var media = snapshot?.Media ?? [];

        if (store == StoreKind.Apple)
        {
            List<string> families = ["iphone"];

            if (media.Any(m => m.Items.Count > 0 && m.ClassKey != null && m.ClassKey.Contains("IPAD", StringComparison.Ordinal)))
                families.Add("ipad");

            return families;
        }

        if (store == StoreKind.Google)
        {
            List<string> families = ["phone"];

            if (media.Any(m => m.Items.Count > 0 && m.ClassKey == "sevenInchScreenshots"))
                families.Add("tablet-7");

            if (media.Any(m => m.Items.Count > 0 && m.ClassKey == "tenInchScreenshots"))
                families.Add("tablet-10");

            return families;
        }

        return null;
    }

    private static List<string> Regions(StoreContentSnapshot snapshot)
    {
        if (snapshot == null)
            return null;

        var available = snapshot.Values.Where(v => v.Key.StartsWith(ContentGroups.Availability + "|", StringComparison.Ordinal) && v.Value == "true")
                                       .Select(v => ToAlpha2(ContentKey.Parse(v.Key).Field))
                                       .ToList();

        return available.Count == 0 ? null : available;
    }

    private static Dictionary<string, string> BuildRegionMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);

                map.TryAdd(region.ThreeLetterISORegionName, region.TwoLetterISORegionName);
            }
            catch (ArgumentException)
            {
                // A culture without a region.
            }
        }

        return map;
    }
}
