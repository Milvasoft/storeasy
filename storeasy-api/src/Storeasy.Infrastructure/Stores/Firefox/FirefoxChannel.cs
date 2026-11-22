using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Globalization;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Firefox;

/// <summary>
/// The Firefox Add-ons engine as a store channel: the credential is an AMO API key (JWT issuer and secret), the products are
/// the add-ons the account authors (listed by the API), a product's builds are its listed and unlisted versions. It also
/// reads the ratings and answers them as the developer.
/// </summary>
/// <param name="client"></param>
/// <param name="options"></param>
public sealed class FirefoxChannel(IAmoClient client, FirefoxOptions options) : IStoreChannel, IStoreChannelReviews, IStoreChannelReplies
{
    private readonly IAmoClient _client = client;
    private readonly FirefoxOptions _options = options;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Firefox;

    /// <inheritdoc/>
    public StoreSecretInspection Inspect(string secretJson) => InspectSecret(secretJson);

    /// <summary>
    /// The offline check of a Firefox secret.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <returns></returns>
    public static StoreSecretInspection InspectSecret(string secretJson)
    {
        var credentials = FirefoxCredentialParser.Parse(secretJson, out var errorKey);

        if (credentials == null)
            return new StoreSecretInspection(false, errorKey, null, null);

        var fingerprint = new ConnectionFingerprint
        {
            KeyId = credentials.Issuer,
            AccountLogin = $"user {credentials.UserId}",
            TokenKind = "JWT",
        };

        return new StoreSecretInspection(true, null, fingerprint, JsonSerializer.Serialize(credentials));
    }

    /// <inheritdoc/>
    public async Task<StoreVerification> VerifyAsync(string secretJson, string productId, CancellationToken cancellationToken)
    {
        if (FirefoxCredentialParser.Parse(secretJson, out var errorKey) == null)
            return new StoreVerification(ConnectionStatus.Rejected, errorKey);

        try
        {
            await _client.GetProfileAsync(secretJson, cancellationToken);
        }
        catch (StoreCallException ex)
        {
            return new StoreVerification(ex.Transient ? ConnectionStatus.Unreachable : ConnectionStatus.Rejected, $"addons.mozilla.org refused the API key: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(productId))
            return new StoreVerification(ConnectionStatus.Verified, null);

        try
        {
            await _client.GetAddonAsync(secretJson, productId.Trim(), cancellationToken);
        }
        catch (StoreCallException ex)
        {
            return new StoreVerification(ex.Transient ? ConnectionStatus.Unreachable : ConnectionStatus.Rejected, $"The API key cannot read add-on {productId}: {ex.Message}");
        }

        return new StoreVerification(ConnectionStatus.Verified, null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreAppRecord>> ListAppsAsync(string secretJson, CancellationToken cancellationToken)
        => [.. (await _client.ListAddonsAsync(secretJson, cancellationToken)).Select(RecordOf)];

    /// <inheritdoc/>
    public async Task<StoreAppRecord> GetAppAsync(string secretJson, string externalId, CancellationToken cancellationToken)
        => RecordOf(await _client.GetAddonAsync(secretJson, externalId, cancellationToken));

    /// <summary>
    /// The store record of an add-on: the numeric id, the guid as the bundle id, the name.
    /// </summary>
    /// <param name="addon"></param>
    /// <returns></returns>
    public static StoreAppRecord RecordOf(AmoAddonRead addon) => new(addon.Id.ToString(CultureInfo.InvariantCulture), addon.Guid, addon.NameIn() ?? addon.Slug);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreBuildRecord>> ListBuildsAsync(string secretJson, string externalId, CancellationToken cancellationToken)
        => BuildsOf(await _client.ListVersionsAsync(secretJson, externalId, _options.VersionPages, cancellationToken));

    /// <summary>
    /// The versions as builds: approved versions are valid, versions awaiting review are processing, disabled ones invalid;
    /// the channel is kept as the track.
    /// </summary>
    /// <param name="versions"></param>
    /// <returns></returns>
    public static List<StoreBuildRecord> BuildsOf(IEnumerable<AmoVersionRead> versions)
        => [.. versions.Where(v => v.Id > 0 && !string.IsNullOrEmpty(v.Version))
                       .Select(v => new StoreBuildRecord(v.Id.ToString(CultureInfo.InvariantCulture),
                                                         v.Version,
                                                         v.Version,
                                                         v.FileStatus switch
                                                         {
                                                             FirefoxFileStatus.Public => BuildProcessingState.Valid,
                                                             FirefoxFileStatus.Unreviewed => BuildProcessingState.Processing,
                                                             FirefoxFileStatus.Disabled => BuildProcessingState.Invalid,
                                                             _ => BuildProcessingState.Processing,
                                                         },
                                                         v.Created,
                                                         null,
                                                         v.Channel.ToString().ToLowerInvariant(),
                                                         v.MinFirefox))];

    /// <inheritdoc/>
    public async Task<string> GetIconUrlAsync(string secretJson, string externalId, CancellationToken cancellationToken)
        => (await _client.GetAddonAsync(secretJson, externalId, cancellationToken)).IconUrl;

    /// <inheritdoc/>
    public StoreJobRequest SyncJob(int organizationId, int connectionId) => new(organizationId, StoreJobKind.FirefoxSync, new FirefoxSyncPayload(connectionId), StoreKind.Firefox);

    /// <inheritdoc/>
    public bool SameAccount(ConnectionFingerprint current, ConnectionFingerprint next)
        => current?.KeyId == null || string.Equals(FirefoxCredentialParser.UserIdOf(current.KeyId), FirefoxCredentialParser.UserIdOf(next?.KeyId), StringComparison.Ordinal);

    /// <inheritdoc/>
    public string CredentialLabel(ConnectionFingerprint fingerprint) => fingerprint?.KeyId == null ? null : $"AMO API key {fingerprint.KeyId}";

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreReviewRecord>> ListReviewsAsync(string secretJson, string externalId, CancellationToken cancellationToken)
        => _client.ListRatingsAsync(secretJson, externalId, _options.RatingPages, cancellationToken);

    /// <inheritdoc/>
    public Task<StoreReplyRecord> ReplyAsync(string secretJson, string externalId, string reviewExternalId, string existingReplyExternalId, string body, CancellationToken cancellationToken)
        => _client.ReplyAsync(secretJson, reviewExternalId, body, cancellationToken);
}
