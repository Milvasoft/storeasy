namespace Storeasy.Application.Interfaces;

/// <summary>
/// Store listing experiments and custom product pages of one store's API (App Store Connect only: Google Play and the
/// Microsoft Store have no such API). Reads are live; writes run inside the change plan apply job, every store call a
/// step.
/// </summary>
public interface IAsoExperimentGateway
{
    /// <summary>
    /// Store of the gateway.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// Reads every experiment (with its treatments) and custom product page (with its latest version and localizations) of
    /// the app.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreAsoRead> ReadAsync(StoreContentTarget target, CancellationToken cancellationToken);

    /// <summary>
    /// Writes an approved experiment plan.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="changes"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AsoApplyResult> ApplyAsync(StoreContentTarget target, AsoExperimentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Experiments and custom product pages of an app as read from the store.
/// </summary>
public sealed class StoreAsoRead
{
    /// <summary>
    /// Product page optimization experiments.
    /// </summary>
    public List<StoreExperimentState> Experiments { get; set; } = [];

    /// <summary>
    /// Custom product pages.
    /// </summary>
    public List<StoreCustomPageState> CustomPages { get; set; } = [];
}

/// <summary>
/// An App Store experiment (v2) as read.
/// </summary>
public sealed class StoreExperimentState
{
    /// <summary>
    /// Experiment id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Apple state (PREPARE_FOR_SUBMISSION, READY_FOR_REVIEW, WAITING_FOR_REVIEW, IN_REVIEW, ACCEPTED, APPROVED, REJECTED,
    /// COMPLETED, STOPPED).
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Platform (IOS, MAC_OS, ...).
    /// </summary>
    public string Platform { get; set; }

    /// <summary>
    /// Share of the traffic the treatments get, in percent.
    /// </summary>
    public int? TrafficProportion { get; set; }

    /// <summary>
    /// Whether App Review must approve it before it starts.
    /// </summary>
    public bool? ReviewRequired { get; set; }

    /// <summary>
    /// Start.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Treatments.
    /// </summary>
    public List<StoreTreatmentState> Treatments { get; set; } = [];
}

/// <summary>
/// An App Store experiment treatment as read.
/// </summary>
public sealed class StoreTreatmentState
{
    /// <summary>
    /// Treatment id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Alternate icon name.
    /// </summary>
    public string AppIconName { get; set; }

    /// <summary>
    /// When the treatment was applied to the product page in App Store Connect, if it was.
    /// </summary>
    public DateTime? PromotedDate { get; set; }
}

/// <summary>
/// An App Store custom product page as read, with its latest version.
/// </summary>
public sealed class StoreCustomPageState
{
    /// <summary>
    /// Page id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Address of the page (with its <c>ppid</c>).
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Whether the page is visible.
    /// </summary>
    public bool? Visible { get; set; }

    /// <summary>
    /// Latest version id.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// State of the latest version.
    /// </summary>
    public string VersionState { get; set; }

    /// <summary>
    /// Localizations of the latest version.
    /// </summary>
    public List<StoreCustomPageLocalization> Localizations { get; set; } = [];
}

/// <summary>
/// A custom product page localization as read.
/// </summary>
public sealed class StoreCustomPageLocalization
{
    /// <summary>
    /// Localization id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Promotional text.
    /// </summary>
    public string PromotionalText { get; set; }
}

/// <summary>
/// What an approved experiment plan writes (stored as the plan's changes; never a secret).
/// </summary>
public sealed class AsoExperimentChangeSet
{
    /// <summary>
    /// storeasy experiment record.
    /// </summary>
    public int ExperimentId { get; set; }

    /// <summary>
    /// Kind of the record.
    /// </summary>
    public AsoExperimentKind Kind { get; set; }

    /// <summary>
    /// What the plan does.
    /// </summary>
    public AsoExperimentOperation Operation { get; set; }

    /// <summary>
    /// The store's id, null when the plan creates it.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Custom product page version to write to (null: the latest editable one, or a new one).
    /// </summary>
    public string ExternalVersionId { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Platform of a new experiment.
    /// </summary>
    public string Platform { get; set; } = "IOS";

    /// <summary>
    /// Traffic proportion in percent.
    /// </summary>
    public int? TrafficProportion { get; set; }

    /// <summary>
    /// Treatments (or the single page of a custom product page) with what they change.
    /// </summary>
    public List<AsoTreatmentChange> Treatments { get; set; } = [];

    /// <summary>
    /// Submit: add the item to the open review submission without submitting it, so it goes to App Review with the app
    /// version.
    /// </summary>
    public bool WithVersion { get; set; }

    /// <summary>
    /// Apply winner: the editable App Store version the winner's media is written to.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Apply winner: media sets of the version.
    /// </summary>
    public List<MediaSetChange> VersionMedia { get; set; } = [];
}

/// <summary>
/// A treatment (or custom product page) to write.
/// </summary>
public sealed class AsoTreatmentChange
{
    /// <summary>
    /// Variant key in storeasy.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Alternate icon name (experiments).
    /// </summary>
    public string AppIconName { get; set; }

    /// <summary>
    /// The store's id, null to create.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Localizations.
    /// </summary>
    public List<AsoLocaleChange> Locales { get; set; } = [];
}

/// <summary>
/// One localization of a treatment or custom product page to write.
/// </summary>
public sealed class AsoLocaleChange
{
    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Promotional text (custom product pages); null keeps it.
    /// </summary>
    public string PromotionalText { get; set; }

    /// <summary>
    /// Media sets; a set left out is not touched.
    /// </summary>
    public List<MediaSetChange> Media { get; set; } = [];
}

/// <summary>
/// Outcome of an applied experiment plan.
/// </summary>
public sealed class AsoApplyResult
{
    /// <summary>
    /// Short outcome lines.
    /// </summary>
    public List<string> Lines { get; set; } = [];

    /// <summary>
    /// The store's id of the experiment or page.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Custom product page version written.
    /// </summary>
    public string ExternalVersionId { get; set; }

    /// <summary>
    /// Treatment ids by variant key.
    /// </summary>
    public Dictionary<string, string> TreatmentIds { get; set; } = [];

    /// <summary>
    /// Store state after the write, when known.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Page address, when known.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// A live read of the experiments of a store record, or the message key of its failure.
/// </summary>
/// <param name="Read">What the store returned.</param>
/// <param name="ErrorKey">Message key of the failure.</param>
/// <param name="ErrorDetail">Redacted store message.</param>
public sealed record AsoStoreReadResult(StoreAsoRead Read, string ErrorKey = null, string ErrorDetail = null)
{
    /// <summary>
    /// Whether the read succeeded.
    /// </summary>
    public bool IsSuccess => this.ErrorKey == null;
}

/// <summary>
/// Opens a store record's connection for experiment reads (the secret is opened for one call and dropped).
/// </summary>
public interface IAsoExperimentStore
{
    /// <summary>
    /// Reads the record's experiments and custom product pages live; failures come back as a message key.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AsoStoreReadResult> ReadAsync(StoreAppContext app, CancellationToken cancellationToken);
}
