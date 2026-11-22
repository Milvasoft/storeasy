namespace Storeasy.Application.Interfaces;

/// <summary>
/// The Chrome Web Store API v2 (<c>chromewebstore.googleapis.com</c>), with an access token from the connection's Google
/// service account or OAuth client and refresh token. Items are addressed as <c>publishers/{publisherId}/items/{itemId}</c>;
/// the API cannot create an item, list a publisher's items, edit the store listing or answer reviews. Failures surface as
/// <see cref="StoreCallException"/>.
/// </summary>
public interface IChromeWebStoreClient
{
    /// <summary>
    /// Proves the credential: an access token for the Chrome Web Store scope and, when <paramref name="itemId"/> is given,
    /// the status of that item under the connection's publisher.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId">Optional extension id to probe.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreVerification> VerifyAsync(string secretJson, string itemId, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET .../items/{itemId}:fetchStatus</c>.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ChromeItemStatusRead> FetchStatusAsync(string secretJson, string itemId, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST /upload/v2/.../items/{itemId}:upload</c> with the ZIP as the body. The store may answer
    /// <see cref="ChromeUploadState.InProgress"/>; the status then tells when it is done.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId"></param>
    /// <param name="package">The ZIP.</param>
    /// <param name="length">Its size in bytes.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ChromeUploadRead> UploadAsync(string secretJson, string itemId, Stream package, long length, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST .../items/{itemId}:publish</c>.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ChromePublishRead> PublishAsync(string secretJson, string itemId, ChromePublishRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST .../items/{itemId}:setPublishedDeployPercentage</c> (only higher than the current one; items with more than
    /// 10,000 seven day users).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId"></param>
    /// <param name="percentage"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SetDeployPercentageAsync(string secretJson, string itemId, int percentage, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST .../items/{itemId}:cancelSubmission</c>.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CancelSubmissionAsync(string secretJson, string itemId, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST .../items/{itemId}:rollback</c>.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task RollbackAsync(string secretJson, string itemId, ChromeRollbackRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET .../items/{itemId}:fetchReviews</c>, newest first, up to <paramref name="maxPages"/> pages of 200.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId"></param>
    /// <param name="maxPages"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<StoreReviewRecord>> FetchReviewsAsync(string secretJson, string itemId, int maxPages, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET .../items/{itemId}/ratingSummary</c>.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ChromeRatingSummaryRead> GetRatingSummaryAsync(string secretJson, string itemId, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET .../items/{itemId}:queryMetrics</c> for the days from <paramref name="from"/> to <paramref name="to"/>,
    /// sliced by at most one dimension (<c>country</c>, <c>os</c>, ...), every page.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="itemId"></param>
    /// <param name="metrics">Metric names (<c>installs</c>, <c>impressions</c>, <c>page-views</c>, <c>users-7d</c>, ...).</param>
    /// <param name="dimension">One dimension or null.</param>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<ChromeMetricRead>> QueryMetricsAsync(string secretJson, string itemId, IReadOnlyList<string> metrics, string dimension, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>
/// One revision of an item (published or submitted).
/// </summary>
/// <param name="State">State.</param>
/// <param name="Version">Package version (<c>crxVersion</c>) of its first distribution channel.</param>
/// <param name="DeployPercentage">Deploy percentage of its first distribution channel.</param>
public sealed record ChromeRevisionRead(ChromeItemState State, string Version, int? DeployPercentage);

/// <summary>
/// What <c>fetchStatus</c> reports.
/// </summary>
/// <param name="ItemId">Extension id.</param>
/// <param name="Published">The published revision, or null when the item is not published.</param>
/// <param name="Submitted">The revision submitted since the last publish, or null.</param>
/// <param name="LastUploadState">State of the last asynchronous upload in the last 24 hours.</param>
/// <param name="Warned">Warned for a policy violation.</param>
/// <param name="TakenDown">Taken down for a policy violation.</param>
public sealed record ChromeItemStatusRead(string ItemId, ChromeRevisionRead Published, ChromeRevisionRead Submitted, ChromeUploadState LastUploadState, bool Warned, bool TakenDown);

/// <summary>
/// What an upload reports.
/// </summary>
/// <param name="State">Upload state.</param>
/// <param name="Version">Package version the store read from the manifest (unset while in progress).</param>
public sealed record ChromeUploadRead(ChromeUploadState State, string Version);

/// <summary>
/// A publish call.
/// </summary>
/// <param name="Staged">Stage the revision after approval instead of publishing it (<c>STAGED_PUBLISH</c>).</param>
/// <param name="DeployPercentage">Initial deploy percentage; null keeps the developer dashboard's value.</param>
/// <param name="SkipReview">Ask the store to skip the review where the item qualifies (refused otherwise).</param>
public sealed record ChromePublishRequest(bool Staged, int? DeployPercentage, bool SkipReview);

/// <summary>
/// What a publish call reports.
/// </summary>
/// <param name="State">State of the submission.</param>
/// <param name="Warnings">Non blocking warnings the store returned.</param>
public sealed record ChromePublishRead(ChromeItemState State, IReadOnlyList<string> Warnings);

/// <summary>
/// A rollback call.
/// </summary>
/// <param name="TargetVersion">Earlier published version to publish again; null for the last one.</param>
/// <param name="NewVersion">Version number given to the rolled back package (higher than the published one).</param>
/// <param name="Reason">Reason, kept by the store for auditing.</param>
public sealed record ChromeRollbackRequest(string TargetVersion, string NewVersion, string Reason);

/// <summary>
/// The rating summary of an item.
/// </summary>
/// <param name="Rating">Average shown on the store page, null without ratings.</param>
/// <param name="Count">Number of ratings.</param>
/// <param name="WithText">Number of ratings with a review text.</param>
public sealed record ChromeRatingSummaryRead(double? Rating, long Count, long WithText);

/// <summary>
/// One row of <c>queryMetrics</c>.
/// </summary>
/// <param name="Date">Day.</param>
/// <param name="Dimension">Value of the requested dimension; null without one.</param>
/// <param name="Values">Metric name to value.</param>
public sealed record ChromeMetricRead(DateOnly Date, string Dimension, IReadOnlyDictionary<string, long> Values);
