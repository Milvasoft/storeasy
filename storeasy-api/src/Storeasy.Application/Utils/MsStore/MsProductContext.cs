namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// A Microsoft Store record of an organization with what the handlers need from its product row.
/// </summary>
public sealed class MsProductContext
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; init; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; init; }

    /// <summary>
    /// Store id of the product.
    /// </summary>
    public string ProductId { get; init; }

    /// <summary>
    /// Connection the record is read with; null when it was removed.
    /// </summary>
    public int? ConnectionId { get; init; }

    /// <summary>
    /// Which API manages the product.
    /// </summary>
    public MsProductKind Kind { get; init; }

    /// <summary>
    /// Whether the first submission was made.
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
    /// MSI/EXE draft readiness.
    /// </summary>
    public bool IsReady { get; init; }

    /// <summary>
    /// Pending app submission.
    /// </summary>
    public string PendingSubmissionId { get; init; }
}
