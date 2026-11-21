using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Experiments;

/// <summary>
/// Pure rules of store listing experiments: what each store supports and how, the limits of each kind, how App Store
/// states map onto <see cref="AsoExperimentStatus"/>, which writes can be planned in which state, and the permission and
/// typed confirmation each write needs. No database, no store.
/// </summary>
public static class AsoExperimentRules
{
    /// <summary>
    /// Most treatments of an App Store product page optimization (Apple's limit).
    /// </summary>
    public const int MaxAppleTreatments = 3;

    /// <summary>
    /// Most variants of a Play store listing experiment (Play Console's limit).
    /// </summary>
    public const int MaxPlayVariants = 3;

    /// <summary>
    /// Longest promotional text of a custom product page.
    /// </summary>
    public const int PromotionalTextMaxLength = 170;

    /// <summary>
    /// Longest Play short description.
    /// </summary>
    public const int ShortDescriptionMaxLength = 80;

    /// <summary>
    /// Longest Play full description.
    /// </summary>
    public const int FullDescriptionMaxLength = 4000;

    /// <summary>
    /// Longest alternate icon name.
    /// </summary>
    public const int IconNameMaxLength = 100;

    /// <summary>
    /// Longest variant name.
    /// </summary>
    public const int VariantNameMaxLength = 100;

    /// <summary>
    /// Kinds of each store, in the order they are offered.
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static IReadOnlyList<AsoExperimentKind> KindsOf(StoreKind store) => store switch
    {
        StoreKind.Apple => [AsoExperimentKind.ProductPageOptimization, AsoExperimentKind.CustomProductPage],
        StoreKind.Google => [AsoExperimentKind.PlayListingExperiment, AsoExperimentKind.PlayCustomListing],
        _ => [],
    };

    /// <summary>
    /// How <paramref name="store"/> supports <paramref name="kind"/>: App Store through the API, Play as an assisted step
    /// (the Android Publisher API has no experiments or custom store listings), Microsoft Store not at all (Partner Center
    /// has no listing experiments API).
    /// </summary>
    /// <param name="store"></param>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static AsoStoreSupport Support(StoreKind store, AsoExperimentKind kind) => (store, kind) switch
    {
        (StoreKind.Apple, AsoExperimentKind.ProductPageOptimization or AsoExperimentKind.CustomProductPage) => AsoStoreSupport.Api,
        (StoreKind.Google, AsoExperimentKind.PlayListingExperiment or AsoExperimentKind.PlayCustomListing) => AsoStoreSupport.Assisted,
        _ => AsoStoreSupport.NotSupported,
    };

    /// <summary>
    /// Whether records of <paramref name="kind"/> are written through the store's API (and so through change plans).
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static bool IsApple(AsoExperimentKind kind) => kind is AsoExperimentKind.ProductPageOptimization or AsoExperimentKind.CustomProductPage;

    /// <summary>
    /// Most variants of <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static int MaxVariants(AsoExperimentKind kind) => kind switch
    {
        AsoExperimentKind.ProductPageOptimization => MaxAppleTreatments,
        AsoExperimentKind.PlayListingExperiment => MaxPlayVariants,
        _ => 1,
    };

    /// <summary>
    /// Whether <paramref name="kind"/> splits traffic (and so has a traffic proportion).
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static bool HasTraffic(AsoExperimentKind kind) => kind is AsoExperimentKind.ProductPageOptimization or AsoExperimentKind.PlayListingExperiment;

    /// <summary>
    /// Whether a record in <paramref name="status"/> can still be edited in storeasy: drafts, records the store still lets
    /// edit, and rejected ones (fixed and submitted again). Play records stay editable until they end.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="status"></param>
    /// <returns></returns>
    public static bool Editable(AsoExperimentKind kind, AsoExperimentStatus status)
    {
        if (!IsApple(kind))
            return status is not (AsoExperimentStatus.Completed or AsoExperimentStatus.Stopped);

        if (kind == AsoExperimentKind.CustomProductPage)
            return status != AsoExperimentStatus.InReview;

        return status is AsoExperimentStatus.Draft or AsoExperimentStatus.Prepared or AsoExperimentStatus.Rejected;
    }

    /// <summary>
    /// Status of an App Store experiment from its state and dates. A started experiment keeps its review state at Apple
    /// (ACCEPTED or APPROVED) with a start date, so a start date without an end date reads as running.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public static AsoExperimentStatus StatusOfExperiment(string state, DateTime? startDate, DateTime? endDate) => state switch
    {
        "PREPARE_FOR_SUBMISSION" or "READY_FOR_REVIEW" => AsoExperimentStatus.Prepared,
        "WAITING_FOR_REVIEW" or "IN_REVIEW" => AsoExperimentStatus.InReview,
        "ACCEPTED" or "APPROVED" when startDate != null && endDate == null => AsoExperimentStatus.Running,
        "ACCEPTED" or "APPROVED" when startDate != null => AsoExperimentStatus.Completed,
        "ACCEPTED" or "APPROVED" => AsoExperimentStatus.Approved,
        "REJECTED" => AsoExperimentStatus.Rejected,
        "STOPPED" => AsoExperimentStatus.Stopped,
        "COMPLETED" => AsoExperimentStatus.Completed,
        _ => AsoExperimentStatus.Prepared,
    };

    /// <summary>
    /// Status of an App Store custom product page from its latest version's state and whether the page is visible: an
    /// approved, visible page is live (<see cref="AsoExperimentStatus.Running"/>).
    /// </summary>
    /// <param name="versionState"></param>
    /// <param name="visible"></param>
    /// <returns></returns>
    public static AsoExperimentStatus StatusOfCustomPage(string versionState, bool? visible) => versionState switch
    {
        "PREPARE_FOR_SUBMISSION" or "READY_FOR_REVIEW" => AsoExperimentStatus.Prepared,
        "WAITING_FOR_REVIEW" or "IN_REVIEW" => AsoExperimentStatus.InReview,
        "ACCEPTED" or "APPROVED" or "REPLACED_WITH_NEW_VERSION" => visible == false ? AsoExperimentStatus.Approved : AsoExperimentStatus.Running,
        "REJECTED" => AsoExperimentStatus.Rejected,
        _ => AsoExperimentStatus.Prepared,
    };

    /// <summary>
    /// Writes that can be planned for a record now. Play records have none: they are run in Play Console.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="status"></param>
    /// <param name="written">Whether the record exists in the store.</param>
    /// <param name="reviewRequired"></param>
    /// <param name="hasWinner"></param>
    /// <returns></returns>
    public static IReadOnlyList<AsoExperimentOperation> Operations(AsoExperimentKind kind, AsoExperimentStatus status, bool written, bool? reviewRequired, bool hasWinner)
    {
        var operations = new List<AsoExperimentOperation>();

        if (!IsApple(kind))
            return operations;

        if (Editable(kind, status))
            operations.Add(AsoExperimentOperation.Push);

        if (written && status is AsoExperimentStatus.Prepared or AsoExperimentStatus.Rejected && (kind == AsoExperimentKind.CustomProductPage || reviewRequired != false))
            operations.Add(AsoExperimentOperation.Submit);

        if (kind != AsoExperimentKind.ProductPageOptimization)
            return operations;

        if (written && (status == AsoExperimentStatus.Approved || (status == AsoExperimentStatus.Prepared && reviewRequired == false)))
            operations.Add(AsoExperimentOperation.Start);

        if (written && status == AsoExperimentStatus.Running)
            operations.Add(AsoExperimentOperation.Stop);

        if (hasWinner && status is AsoExperimentStatus.Running or AsoExperimentStatus.Stopped or AsoExperimentStatus.Completed)
            operations.Add(AsoExperimentOperation.ApplyWinner);

        return operations;
    }

    /// <summary>
    /// Permission needed to approve a plan of <paramref name="operation"/>: writing a draft is content editing, a
    /// submission needs the submit permission, and whatever changes the live product page (start, stop, applying the
    /// winner) needs the production release permission.
    /// </summary>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static OrgPermission PermissionOf(AsoExperimentOperation operation) => operation switch
    {
        AsoExperimentOperation.Push => OrgPermission.EditContent,
        AsoExperimentOperation.Submit => OrgPermission.SubmitForReview,
        _ => OrgPermission.ReleaseProduction,
    };

    /// <summary>
    /// Whether the approver must type the confirmation text: every write but writing a draft.
    /// </summary>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static bool NeedsConfirmation(AsoExperimentOperation operation) => operation != AsoExperimentOperation.Push;

    /// <summary>
    /// The confirmation text: the app name and the experiment name, e.g. "Pusula Icon test".
    /// </summary>
    /// <param name="appName"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string ConfirmationText(string appName, string name) => $"{appName?.Trim()} {name?.Trim()}".Trim();

    /// <summary>
    /// Audit action of a plan of <paramref name="operation"/>.
    /// </summary>
    /// <param name="operation"></param>
    /// <returns></returns>
    public static AuditAction AuditActionOf(AsoExperimentOperation operation) => operation switch
    {
        AsoExperimentOperation.Submit => AuditAction.AsoExperimentSubmitted,
        AsoExperimentOperation.Start => AuditAction.AsoExperimentStarted,
        AsoExperimentOperation.Stop => AuditAction.AsoExperimentStopped,
        AsoExperimentOperation.ApplyWinner => AuditAction.AsoExperimentWinnerApplied,
        _ => AuditAction.AsoExperimentWritten,
    };

    /// <summary>
    /// Whether <paramref name="status"/> is final (the record will not change in the store any more).
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static bool IsFinal(AsoExperimentStatus status) => status is AsoExperimentStatus.Completed or AsoExperimentStatus.Stopped;
}
