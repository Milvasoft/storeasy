namespace Storeasy.Application.Interfaces;

/// <summary>
/// User reviews of a channel's products, read by the shared review sync (<see cref="StoreJobKind.SyncReviews"/>). A channel
/// registers it when its API returns reviews; the review sync asks it before the built in review gateway, so a channel
/// needs no branch in the shared code. Replies are answered by the shared reply job only for stores whose
/// <c>ReviewRules.ReplyMaxLength</c> is above zero; a channel whose API cannot reply leaves it at zero and the web app
/// points at the store's console (docs/adding-a-store.md).
/// </summary>
public interface IStoreChannelReviews
{
    /// <summary>
    /// The store whose reviews this reads.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// The recent reviews of one product, newest first.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="externalId">The product's id in the store.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<StoreReviewRecord>> ListReviewsAsync(string secretJson, string externalId, CancellationToken cancellationToken);
}

/// <summary>
/// Store page metrics of a channel's products, read by the shared metrics sync (<see cref="StoreJobKind.SyncStoreMetrics"/>)
/// for channels whose <c>StoreChannels</c> row has metrics. The sync asks it for every store it has no built in reader
/// for (docs/adding-a-store.md).
/// </summary>
public interface IStoreChannelMetrics
{
    /// <summary>
    /// The store whose metrics this reads.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// How many days the first read goes back.
    /// </summary>
    int BackfillDays { get; }

    /// <summary>
    /// Daily rows of one product from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="externalId"></param>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreMetricBatch> ReadMetricsAsync(string secretJson, string externalId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>
/// Delivery of a package received by artifact intake to a channel (<see cref="StoreJobKind.ProcessArtifact"/>). The intake
/// job reads and checks the package, then hands it to the channel of the session's store when one is registered; the
/// channel only uploads (nothing is published or submitted for review without an approved plan).
/// </summary>
public interface IArtifactChannel
{
    /// <summary>
    /// The store this channel delivers to.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// Whether the package matches the store record (<c>null</c> when the package carries no identity to compare; the
    /// reason then becomes a warning of the parse step).
    /// </summary>
    /// <param name="package"></param>
    /// <param name="record">The store record's external id and bundle id.</param>
    /// <returns></returns>
    (bool? Matches, string Reason) Match(Storeasy.Domain.Intake.ArtifactPackageInfo package, StoreAppRecord record);

    /// <summary>
    /// Uploads the package to the product; returns the outcome line, or null while the store still processes it (the job is
    /// then retried).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> DeliverAsync(ArtifactDeliveryRequest request, IStoreJobContext context, CancellationToken cancellationToken);
}

/// <summary>
/// A package to deliver.
/// </summary>
/// <param name="OrganizationId">Organization.</param>
/// <param name="SessionId">Intake session.</param>
/// <param name="StoreAppId">Store record.</param>
/// <param name="ExternalId">The product's id in the store.</param>
/// <param name="SecretJson">The connection's credential.</param>
/// <param name="ObjectKey">Storage key of the package.</param>
/// <param name="Size">Its size.</param>
/// <param name="Package">What intake read from it.</param>
/// <param name="StoreUploadId">What an earlier attempt of the same delivery recorded, or null.</param>
public sealed record ArtifactDeliveryRequest(int OrganizationId,
                                             long SessionId,
                                             int StoreAppId,
                                             string ExternalId,
                                             string SecretJson,
                                             string ObjectKey,
                                             long Size,
                                             Storeasy.Domain.Intake.ArtifactPackageInfo Package,
                                             string StoreUploadId);
