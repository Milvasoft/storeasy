using Storeasy.Application.Utils.MsStore;
using System.Text.Json.Nodes;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Typed client of the two Microsoft Store APIs: the MSIX submission API (<c>manage.devcenter.microsoft.com</c>: apps,
/// app, flight and add-on submissions, package rollout) and the MSI/EXE submission API (<c>api.store.microsoft.com</c>:
/// metadata, packages, readiness, submit). <c>secretJson</c> is the decrypted connection secret
/// <c>{"tenantId","clientId","clientSecret","sellerId"}</c>; tokens are cached per connection until shortly before they
/// expire. 429, 5xx and network failures are retried with backoff; failures surface as
/// <see cref="StoreCallException"/> with a redacted message (409 is permanent and means the API refuses the product).
/// Submissions are read and written as <see cref="JsonObject"/> so that a PUT sends back every field the API returned.
/// </summary>
public interface IMicrosoftStoreClient
{
    /// <summary>
    /// Gets a token for both APIs and lists one app: proves the Entra app works and has a role in Partner Center.
    /// </summary>
    Task<StoreVerification> VerifyAsync(string secretJson, CancellationToken cancellationToken);

    /// <summary>
    /// Lists every MSIX app of the account (all pages).
    /// </summary>
    Task<IReadOnlyList<MsApplicationRecord>> ListApplicationsAsync(string secretJson, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one MSIX app.
    /// </summary>
    Task<MsApplicationRecord> GetApplicationAsync(string secretJson, string applicationId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one app, flight or add-on submission.
    /// </summary>
    Task<JsonObject> GetSubmissionAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new submission of the owner (a copy of its last published one) and returns it, with the SAS address for
    /// files in <c>fileUploadUrl</c>.
    /// </summary>
    Task<JsonObject> CreateSubmissionAsync(string secretJson, MsSubmissionRef owner, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the content of a pending submission and returns what the API stored.
    /// </summary>
    Task<JsonObject> UpdateSubmissionAsync(string secretJson, MsSubmissionRef submission, JsonObject content, CancellationToken cancellationToken);

    /// <summary>
    /// Commits a pending submission (sends it on to certification).
    /// </summary>
    Task<MsStatusRecord> CommitSubmissionAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the status of a submission.
    /// </summary>
    Task<MsStatusRecord> GetSubmissionStatusAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a pending submission.
    /// </summary>
    Task DeleteSubmissionAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the gradual rollout of a published submission.
    /// </summary>
    Task<MsRolloutRecord> GetRolloutAsync(string secretJson, MsSubmissionRef submission, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the gradual rollout of a published submission (percentage, halt or finalize).
    /// </summary>
    Task<MsRolloutRecord> ChangeRolloutAsync(string secretJson, MsSubmissionRef submission, MsRolloutAction action, double? percentage, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the package flights of an app (all pages).
    /// </summary>
    Task<IReadOnlyList<MsFlightRecord>> ListFlightsAsync(string secretJson, string applicationId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a package flight.
    /// </summary>
    Task<MsFlightRecord> CreateFlightAsync(string secretJson, string applicationId, string friendlyName, IReadOnlyList<string> groupIds, string rankHigherThan, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the store ids of an app's add-ons (all pages).
    /// </summary>
    Task<IReadOnlyList<string>> ListAddonIdsAsync(string secretJson, string applicationId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one add-on.
    /// </summary>
    Task<MsAddonRecord> GetAddonAsync(string secretJson, string addonStoreId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an add-on of an app.
    /// </summary>
    Task<MsAddonRecord> CreateAddonAsync(string secretJson, string applicationId, string productId, string productType, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads the ZIP archive of a submission's new files (packages, images) to its SAS address as one block blob.
    /// </summary>
    Task UploadSubmissionFilesAsync(string fileUploadUrl, Stream zip, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads <paramref name="url"/> and hands the body stream and its declared length to <paramref name="read"/>.
    /// With <paramref name="guardAddress"/> the host must be public HTTPS (installer URLs typed by users); storage URLs
    /// pass false.
    /// </summary>
    Task<T> DownloadAsync<T>(string url, bool guardAddress, Func<Stream, long?, Task<T>> read, CancellationToken cancellationToken);

    /// <summary>
    /// MSI/EXE: reads the readiness of the product's draft.
    /// </summary>
    Task<MsProductStatusRecord> GetProductStatusAsync(string secretJson, string productId, CancellationToken cancellationToken);

    /// <summary>
    /// MSI/EXE: reads the installer packages of the draft.
    /// </summary>
    Task<IReadOnlyList<MsInstallerPackageRecord>> GetInstallerPackagesAsync(string secretJson, string productId, CancellationToken cancellationToken);

    /// <summary>
    /// MSI/EXE: replaces the whole package set of the draft (the API assigns new package ids).
    /// </summary>
    Task ReplaceInstallerPackagesAsync(string secretJson, string productId, IReadOnlyList<MsInstallerPackageRecord> packages, CancellationToken cancellationToken);

    /// <summary>
    /// MSI/EXE: commits the package set; readiness is then polled with <see cref="GetProductStatusAsync"/>.
    /// </summary>
    Task CommitInstallerPackagesAsync(string secretJson, string productId, CancellationToken cancellationToken);

    /// <summary>
    /// MSI/EXE: reads the draft metadata (<c>listings</c>, <c>properties</c>, <c>availability</c> modules).
    /// </summary>
    Task<JsonObject> GetProductMetadataAsync(string secretJson, string productId, CancellationToken cancellationToken);

    /// <summary>
    /// MSI/EXE: partially updates the draft metadata; fields not sent stay as they are.
    /// </summary>
    Task UpdateProductMetadataAsync(string secretJson, string productId, JsonObject patch, CancellationToken cancellationToken);

    /// <summary>
    /// MSI/EXE: submits the draft and returns the submission id.
    /// </summary>
    Task<string> SubmitProductAsync(string secretJson, string productId, CancellationToken cancellationToken);

    /// <summary>
    /// MSI/EXE: reads the publishing status of a submission.
    /// </summary>
    Task<MsPublishingStatusRecord> GetProductSubmissionStatusAsync(string secretJson, string productId, string submissionId, CancellationToken cancellationToken);
}
