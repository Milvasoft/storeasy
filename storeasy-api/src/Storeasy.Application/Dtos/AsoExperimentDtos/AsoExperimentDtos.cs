namespace Storeasy.Application.Dtos.AsoExperimentDtos;

/// <summary>
/// The experiments tab of an app: every experiment and custom page record, and what each store record supports.
/// </summary>
public sealed class AsoExperimentOverviewDto
{
    /// <summary>
    /// Records, newest first.
    /// </summary>
    public List<AsoExperimentSummaryDto> Experiments { get; set; } = [];

    /// <summary>
    /// What each store record of the app supports.
    /// </summary>
    public List<AsoStoreCapabilityDto> Capabilities { get; set; } = [];
}

/// <summary>
/// What a store record supports.
/// </summary>
public sealed class AsoStoreCapabilityDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Supported kinds and how; empty when the store supports none (Microsoft Store).
    /// </summary>
    public List<AsoKindSupportDto> Kinds { get; set; } = [];
}

/// <summary>
/// How a kind is supported.
/// </summary>
public sealed class AsoKindSupportDto
{
    /// <summary>
    /// Kind.
    /// </summary>
    public AsoExperimentKind Kind { get; set; }

    /// <summary>
    /// Through the API or as an assisted step.
    /// </summary>
    public AsoStoreSupport Support { get; set; }
}

/// <summary>
/// A record in the list.
/// </summary>
public class AsoExperimentSummaryDto
{
    /// <summary>
    /// Id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Kind.
    /// </summary>
    public AsoExperimentKind Kind { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public AsoExperimentStatus Status { get; set; }

    /// <summary>
    /// The store's own state.
    /// </summary>
    public string StoreState { get; set; }

    /// <summary>
    /// Traffic proportion in percent.
    /// </summary>
    public int? TrafficProportion { get; set; }

    /// <summary>
    /// Number of variants.
    /// </summary>
    public int VariantCount { get; set; }

    /// <summary>
    /// Start.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Winning variant.
    /// </summary>
    public string WinnerVariantKey { get; set; }

    /// <summary>
    /// Page address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Last change in storeasy.
    /// </summary>
    public DateTime UpdatedDate { get; set; }
}

/// <summary>
/// A record with its variants, links, timeline and what can be done now.
/// </summary>
public sealed class AsoExperimentDto : AsoExperimentSummaryDto
{
    /// <summary>
    /// The store's id.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Variants.
    /// </summary>
    public List<AsoExperimentVariant> Variants { get; set; } = [];

    /// <summary>
    /// Result in the member's words.
    /// </summary>
    public string ResultSummary { get; set; }

    /// <summary>
    /// Link to the results.
    /// </summary>
    public string ResultUrl { get; set; }

    /// <summary>
    /// Free note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Whether App Review must approve it (App Store).
    /// </summary>
    public bool? ReviewRequired { get; set; }

    /// <summary>
    /// Done checklist items (Play).
    /// </summary>
    public List<int> ChecklistDone { get; set; } = [];

    /// <summary>
    /// The assisted step of a Play record (checklist in the request's language); null for App Store records.
    /// </summary>
    public AsoAssistedDto Assisted { get; set; }

    /// <summary>
    /// What happened, oldest first.
    /// </summary>
    public List<AsoTimelineEntryDto> Timeline { get; set; } = [];

    /// <summary>
    /// The store console page.
    /// </summary>
    public string ConsoleUrl { get; set; }

    /// <summary>
    /// Member who created it.
    /// </summary>
    public string CreatedByUserName { get; set; }

    /// <summary>
    /// Last store read.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Writes that can be planned now.
    /// </summary>
    public List<AsoExperimentOperation> Operations { get; set; } = [];
}

/// <summary>
/// The guided Play Console part of a Play record.
/// </summary>
public sealed class AsoAssistedDto
{
    /// <summary>
    /// Play Console page.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Checklist items in order (item n is number n in the done list).
    /// </summary>
    public List<string> Steps { get; set; } = [];

    /// <summary>
    /// Known traps.
    /// </summary>
    public List<string> Pitfalls { get; set; } = [];
}

/// <summary>
/// One event of a record.
/// </summary>
public sealed class AsoTimelineEntryDto
{
    /// <summary>
    /// When.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// created, plan, submitted, started, ended, result or synced.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// One line.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Change plan, for plan events.
    /// </summary>
    public long? PlanId { get; set; }

    /// <summary>
    /// State of the plan.
    /// </summary>
    public ChangePlanStatus? PlanStatus { get; set; }
}

/// <summary>
/// Everything a member needs to set the variants up in a store console by hand: download links of the files and the
/// localized texts.
/// </summary>
public sealed class AsoExperimentExportDto
{
    /// <summary>
    /// Files with short lived download addresses.
    /// </summary>
    public List<AsoExportFileDto> Files { get; set; } = [];

    /// <summary>
    /// Texts by variant, locale and field.
    /// </summary>
    public List<AsoExportTextDto> Texts { get; set; } = [];
}

/// <summary>
/// A file of a variant.
/// </summary>
public sealed class AsoExportFileDto
{
    /// <summary>
    /// Variant.
    /// </summary>
    public string VariantKey { get; set; }

    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Store class key.
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Asset.
    /// </summary>
    public long AssetId { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Short lived download address.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// A text of a variant.
/// </summary>
public sealed class AsoExportTextDto
{
    /// <summary>
    /// Variant.
    /// </summary>
    public string VariantKey { get; set; }

    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// promotionalText, shortDescription or fullDescription.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Text.
    /// </summary>
    public string Value { get; set; }
}
