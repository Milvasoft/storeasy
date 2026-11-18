namespace Storeasy.Application.Interfaces;

/// <summary>
/// App Store Connect calls of the events phase: webhook registrations (an App Store Connect webhook belongs to an app)
/// and App Store version reads. Failures throw <see cref="StoreCallException"/>; tests use a fake.
/// </summary>
public interface IStoreEventGateway
{
    /// <summary>
    /// Creates (or, when <paramref name="existingWebhookId"/> is given, updates) the webhook of one app and returns its id.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="appExternalId"></param>
    /// <param name="existingWebhookId"></param>
    /// <param name="registration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> UpsertWebhookAsync(string secretJson, string appExternalId, string existingWebhookId, StoreWebhookRegistration registration, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a webhook; a webhook that is already gone is not an error.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="webhookId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeleteWebhookAsync(string secretJson, string webhookId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one App Store version with its app and build.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="versionId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreVersionRecord> GetVersionAsync(string secretJson, string versionId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the App Store versions of one app, newest first.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="appExternalId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<StoreVersionRecord>> ListVersionsAsync(string secretJson, string appExternalId, CancellationToken cancellationToken);
}

/// <summary>
/// What a webhook registration asks the store for.
/// </summary>
/// <param name="Name">Name shown in App Store Connect.</param>
/// <param name="Url">storeasy's receiving address.</param>
/// <param name="Secret">HMAC secret.</param>
/// <param name="EventTypes">Event types.</param>
public sealed record StoreWebhookRegistration(string Name, string Url, string Secret, IReadOnlyList<string> EventTypes);

/// <summary>
/// An App Store version as the store reports it.
/// </summary>
/// <param name="Id">Version id.</param>
/// <param name="AppExternalId">App id.</param>
/// <param name="VersionString">User facing version.</param>
/// <param name="Platform">Platform (IOS, MAC_OS...).</param>
/// <param name="State">App version state (<c>appVersionState</c>, falling back to <c>appStoreState</c>).</param>
/// <param name="BuildNumber">Attached build number, if any.</param>
/// <param name="CreatedDate">UTC creation instant, if known.</param>
public sealed record StoreVersionRecord(string Id, string AppExternalId, string VersionString, string Platform, string State, string BuildNumber, DateTime? CreatedDate);
