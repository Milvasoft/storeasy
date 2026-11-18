namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// What storeasy knows about a product beyond the store content, for <see cref="MsChangePlanner"/>: the product flags of
/// the last sync, storeasy's row of the pending submission, and the names already taken.
/// </summary>
public sealed class MsPlanContext
{
    /// <summary>
    /// Which API manages the product.
    /// </summary>
    public MsProductKind Kind { get; init; }

    /// <summary>
    /// Store id of the product.
    /// </summary>
    public string ProductId { get; init; }

    /// <summary>
    /// Whether the first submission was made in Partner Center.
    /// </summary>
    public bool FirstSubmissionDone { get; init; }

    /// <summary>
    /// Whether the app uses mandatory updates.
    /// </summary>
    public bool HasMandatoryUpdate { get; init; }

    /// <summary>
    /// Whether the price is on Pricing Version 2.
    /// </summary>
    public bool PricingV2 { get; init; }

    /// <summary>
    /// storeasy's row of the pending submission the content was read from; null when storeasy has none.
    /// </summary>
    public MsPendingSubmissionInfo Pending { get; init; }

    /// <summary>
    /// Whether the add-on of an add-on change may be written by the API.
    /// </summary>
    public bool AddonWritable { get; init; } = true;

    /// <summary>
    /// Why the add-on is Partner Center only.
    /// </summary>
    public MsAssistedReason? AddonAssistedReason { get; init; }

    /// <summary>
    /// Names of the app's flights.
    /// </summary>
    public IReadOnlyCollection<string> FlightNames { get; init; } = [];

    /// <summary>
    /// Product ids of the app's add-ons.
    /// </summary>
    public IReadOnlyCollection<string> AddonProductIds { get; init; } = [];

    /// <summary>
    /// File names of the uploaded assets the change adds, by asset id (as they will appear in the submission).
    /// </summary>
    public IReadOnlyDictionary<long, string> AssetFileNames { get; init; } = new Dictionary<long, string>();
}

/// <summary>
/// storeasy's row of a pending submission.
/// </summary>
public sealed class MsPendingSubmissionInfo
{
    /// <summary>
    /// Whether storeasy created it.
    /// </summary>
    public bool CreatedByStoreasy { get; init; }

    /// <summary>
    /// Whether it is known to have been changed in Partner Center.
    /// </summary>
    public bool ModifiedOutsideApi { get; init; }

    /// <summary>
    /// Fingerprint of the content storeasy last wrote.
    /// </summary>
    public string ContentHash { get; init; }
}
