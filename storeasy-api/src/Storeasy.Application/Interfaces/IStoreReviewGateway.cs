namespace Storeasy.Application.Interfaces;

/// <summary>
/// Customer reviews and developer replies: App Store Connect customer reviews and responses, Google Play reviews (the
/// API returns only recent reviews with text). Microsoft Store reviews are assisted (the reply API does not work), so it
/// throws a permanent <see cref="StoreCallException"/> there.
/// </summary>
public interface IStoreReviewGateway
{
    /// <summary>
    /// Reads the recent reviews of one app, newest first, with their replies.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="secretJson"></param>
    /// <param name="appExternalId">App Store Connect app id or Play package name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<StoreReviewRecord>> ListReviewsAsync(StoreKind store, string secretJson, string appExternalId, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes the reply of one review; an existing reply is replaced (Play overwrites, App Store deletes the old
    /// response and creates a new one).
    /// </summary>
    /// <param name="store"></param>
    /// <param name="secretJson"></param>
    /// <param name="appExternalId"></param>
    /// <param name="reviewExternalId"></param>
    /// <param name="existingReplyExternalId"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreReplyRecord> ReplyAsync(StoreKind store, string secretJson, string appExternalId, string reviewExternalId, string existingReplyExternalId, string body, CancellationToken cancellationToken);
}

/// <summary>
/// A review as the store reports it.
/// </summary>
/// <param name="ExternalId">Review id.</param>
/// <param name="AuthorName">Reviewer.</param>
/// <param name="Rating">Stars.</param>
/// <param name="Title">Title (App Store).</param>
/// <param name="Body">Text.</param>
/// <param name="Locale">Language, if known.</param>
/// <param name="Territory">Storefront or country, if known.</param>
/// <param name="AppVersion">Reviewed version, if known.</param>
/// <param name="Device">Device, if known.</param>
/// <param name="Date">UTC instant written or last edited.</param>
/// <param name="ReplyExternalId">Reply id, if the store has one.</param>
/// <param name="ReplyBody">Reply text, if any.</param>
/// <param name="ReplyDate">UTC instant of the reply.</param>
public sealed record StoreReviewRecord(string ExternalId,
                                       string AuthorName,
                                       int Rating,
                                       string Title,
                                       string Body,
                                       string Locale,
                                       string Territory,
                                       string AppVersion,
                                       string Device,
                                       DateTime Date,
                                       string ReplyExternalId,
                                       string ReplyBody,
                                       DateTime? ReplyDate);

/// <summary>
/// A published reply.
/// </summary>
/// <param name="ExternalId">Reply id (App Store response id; null for Play).</param>
/// <param name="Date">UTC instant.</param>
public sealed record StoreReplyRecord(string ExternalId, DateTime Date);
