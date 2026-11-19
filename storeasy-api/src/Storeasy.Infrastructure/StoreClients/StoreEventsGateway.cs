using Storeasy.Application.Interfaces;
using System.Text.Json;

namespace Storeasy.Infrastructure.StoreClients;

/// <summary>
/// App Store Connect webhooks and App Store versions (<see cref="IStoreEventGateway"/>), customer reviews of both stores
/// (<see cref="IStoreReviewGateway"/>) and TestFlight (<see cref="ITestFlightGateway"/>), over <see cref="StoreApiClient"/>.
/// The endpoints follow the store-ops engine where it has them.
/// </summary>
/// <param name="client"></param>
public sealed partial class StoreEventsGateway(StoreApiClient client) : IStoreEventGateway
{
    /// <summary>
    /// App Store Connect API base address.
    /// </summary>
    public const string AppleBase = Stores.StoreGateway.AppleBaseUrl;

    private readonly StoreApiClient _client = client;

    /// <inheritdoc/>
    public async Task<string> UpsertWebhookAsync(string secretJson, string appExternalId, string existingWebhookId, StoreWebhookRegistration registration, CancellationToken cancellationToken)
    {
        var token = _client.AppleToken(secretJson);
        var attributes = new { enabled = true, eventTypes = registration.EventTypes, name = registration.Name, secret = registration.Secret, url = registration.Url };

        if (!string.IsNullOrEmpty(existingWebhookId))
        {
            var updated = await _client.JsonOrNotFoundAsync(HttpMethod.Patch,
                                                            $"{AppleBase}/v1/webhooks/{Uri.EscapeDataString(existingWebhookId)}",
                                                            token,
                                                            new { data = new { type = "webhooks", id = existingWebhookId, attributes } },
                                                            cancellationToken);

            if (updated.HasValue)
                return existingWebhookId;
        }

        var created = await _client.JsonAsync(HttpMethod.Post,
                                              $"{AppleBase}/v1/webhooks",
                                              token,
                                              new { data = new { type = "webhooks", attributes, relationships = new { app = new { data = new { type = "apps", id = appExternalId } } } } },
                                              cancellationToken);

        return StoreApiClient.Text(StoreApiClient.Child(created, "data"), "id") ?? throw new StoreCallException("The webhook response has no id.", null, false);
    }

    /// <inheritdoc/>
    public async Task DeleteWebhookAsync(string secretJson, string webhookId, CancellationToken cancellationToken)
        => await _client.JsonOrNotFoundAsync(HttpMethod.Delete, $"{AppleBase}/v1/webhooks/{Uri.EscapeDataString(webhookId)}", _client.AppleToken(secretJson), null, cancellationToken);

    /// <inheritdoc/>
    public async Task<string> PingWebhookAsync(string secretJson, string webhookId, CancellationToken cancellationToken)
    {
        var created = await _client.JsonAsync(HttpMethod.Post,
                                              $"{AppleBase}/v1/webhookPings",
                                              _client.AppleToken(secretJson),
                                              new { data = new { type = "webhookPings", relationships = new { webhook = new { data = new { type = "webhooks", id = webhookId } } } } },
                                              cancellationToken);

        return StoreApiClient.Text(StoreApiClient.Child(created, "data"), "id");
    }

    /// <inheritdoc/>
    public async Task<StoreVersionRecord> GetVersionAsync(string secretJson, string versionId, CancellationToken cancellationToken)
    {
        var root = await _client.JsonAsync(HttpMethod.Get,
                                           $"{AppleBase}/v1/appStoreVersions/{Uri.EscapeDataString(versionId)}?include=app,build&fields[appStoreVersions]=versionString,platform,appVersionState,appStoreState,createdDate,app,build&fields[builds]=version",
                                           _client.AppleToken(secretJson),
                                           null,
                                           cancellationToken);

        return ParseVersion(StoreApiClient.Child(root, "data"), [.. StoreApiClient.Items(root, "included")], null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreVersionRecord>> ListVersionsAsync(string secretJson, string appExternalId, CancellationToken cancellationToken)
    {
        var (data, included) = await _client.AppleListAsync($"{AppleBase}/v1/apps/{Uri.EscapeDataString(appExternalId)}/appStoreVersions?limit=20&include=build&fields[appStoreVersions]=versionString,platform,appVersionState,appStoreState,createdDate,build&fields[builds]=version",
                                                            _client.AppleToken(secretJson),
                                                            cancellationToken,
                                                            maxPages: 1);

        return [.. data.Select(v => ParseVersion(v, included, appExternalId)).OrderByDescending(v => v.CreatedDate)];
    }

    /// <summary>
    /// Maps an <c>appStoreVersions</c> resource (the build number comes from the included build).
    /// </summary>
    public static StoreVersionRecord ParseVersion(JsonElement version, List<JsonElement> included, string appExternalId)
    {
        var attributes = StoreApiClient.Child(version, "attributes");
        var buildId = StoreApiClient.RelationshipId(version, "build");
        var build = buildId == null ? default : included.FirstOrDefault(i => StoreApiClient.Text(i, "type") == "builds" && StoreApiClient.Text(i, "id") == buildId);

        return new StoreVersionRecord(StoreApiClient.Text(version, "id"),
                                      StoreApiClient.RelationshipId(version, "app") ?? appExternalId,
                                      StoreApiClient.Text(attributes, "versionString"),
                                      StoreApiClient.Text(attributes, "platform"),
                                      StoreApiClient.Text(attributes, "appVersionState") ?? StoreApiClient.Text(attributes, "appStoreState"),
                                      build.ValueKind == JsonValueKind.Object ? StoreApiClient.Text(StoreApiClient.Child(build, "attributes"), "version") : null,
                                      StoreApiClient.Date(attributes, "createdDate"));
    }
}
