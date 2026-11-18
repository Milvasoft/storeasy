using System.Text.Json.Serialization;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// The content engine of one store (App Store Connect, Google Play; later Microsoft Store): reads the store's current
/// content into the content model (<see cref="Domain.Content.ContentKey"/>) and writes a <see cref="ContentChangeSet"/>.
/// Writes run only inside the apply job, which reports every store call as a step. Store failures surface as
/// <see cref="StoreCallException"/>; 429 and 5xx answers are retried with backoff inside the implementation.
/// </summary>
public interface IStoreContentGateway
{
    /// <summary>
    /// Store this gateway talks to.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// Reads the parts of the store's content named by <paramref name="parts"/>.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="parts"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreContentSnapshot> ReadAsync(StoreContentTarget target, ContentParts parts, CancellationToken cancellationToken);

    /// <summary>
    /// Lists choices the store offers (categories, price points of a territory, territories).
    /// </summary>
    /// <param name="target"></param>
    /// <param name="kind"></param>
    /// <param name="filter">Territory for price points.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<StoreOption>> ListOptionsAsync(StoreContentTarget target, StoreOptionKind kind, string filter, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <paramref name="changes"/>. The implementation reads what it needs again and writes only what still
    /// differs, so a retried job does not repeat finished work.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="changes"></param>
    /// <param name="context">Step reporting of the apply job.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Short outcome lines (one per written resource).</returns>
    Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, ContentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Kinds of store choices.
/// </summary>
public enum StoreOptionKind : byte
{
    /// <summary>
    /// App categories.
    /// </summary>
    Categories = 1,

    /// <summary>
    /// Price points of one territory.
    /// </summary>
    PricePoints = 2,

    /// <summary>
    /// Territories.
    /// </summary>
    Territories = 3,
}

/// <summary>
/// What a gateway call works on.
/// </summary>
/// <param name="SecretJson">Decrypted connection secret; never logged or stored.</param>
/// <param name="ExternalId">The store's id of the app (App Store Connect app id, Play package name).</param>
/// <param name="VersionId">App Store version to work on; null for the editable one (or the newest).</param>
public sealed record StoreContentTarget(string SecretJson, string ExternalId, string VersionId = null)
{
    /// <summary>
    /// Masks the secret so the record never prints it.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"StoreContentTarget {{ ExternalId = {this.ExternalId}, VersionId = {this.VersionId} }}";
}

/// <summary>
/// A choice the store offers.
/// </summary>
/// <param name="Key">The store's id of the choice.</param>
/// <param name="Title">Display text (the id when the store has no name for it).</param>
/// <param name="Extra">Additional value (price point: customer price).</param>
public sealed record StoreOption(string Key, string Title, string Extra = null);

/// <summary>
/// The store's current content of one app, in the content model plus the structured parts (media, versions, tracks).
/// </summary>
public sealed class StoreContentSnapshot
{
    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the app.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// App Store version the snapshot describes (the editable one unless asked otherwise).
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// User facing version string.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Store state of the version (PREPARE_FOR_SUBMISSION...).
    /// </summary>
    public string VersionState { get; set; }

    /// <summary>
    /// Whether the version's metadata can still be edited.
    /// </summary>
    public bool VersionEditable { get; set; }

    /// <summary>
    /// Whether the app has a version live in the store (then a submission is an update).
    /// </summary>
    public bool? HasLiveVersion { get; set; }

    /// <summary>
    /// App Store platform of the version (IOS, MAC_OS...).
    /// </summary>
    public string Platform { get; set; }

    /// <summary>
    /// Primary (default) locale.
    /// </summary>
    public string PrimaryLocale { get; set; }

    /// <summary>
    /// Locales with a listing.
    /// </summary>
    public List<string> Locales { get; set; } = [];

    /// <summary>
    /// Values by <see cref="Domain.Content.ContentKey"/>.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Media sets (screenshots, previews, graphics).
    /// </summary>
    public List<MediaSetState> Media { get; set; } = [];

    /// <summary>
    /// App Store versions, newest first.
    /// </summary>
    public List<StoreVersionState> Versions { get; set; } = [];

    /// <summary>
    /// Build attached to the version.
    /// </summary>
    public AttachedBuildState Build { get; set; }

    /// <summary>
    /// Play tracks.
    /// </summary>
    public List<TrackState> Tracks { get; set; } = [];

    /// <summary>
    /// Price schedule entries.
    /// </summary>
    public List<PriceEntryState> Prices { get; set; } = [];

    /// <summary>
    /// Whether the app becomes available in new territories automatically.
    /// </summary>
    public bool? AvailableInNewTerritories { get; set; }

    /// <summary>
    /// Whether the store has review details for the version and whether a password is set there.
    /// </summary>
    public bool StoreDemoPasswordSet { get; set; }

    /// <summary>
    /// Value of <paramref name="key"/>, or null.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public string Get(string key) => Values.TryGetValue(key, out var value) ? value : null;
}

/// <summary>
/// One media set in the store.
/// </summary>
public sealed class MediaSetState
{
    /// <summary>
    /// Screenshot or preview.
    /// </summary>
    public MediaKind Kind { get; set; }

    /// <summary>
    /// Locale (language).
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// The store's class key (display type, image type).
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// The store's id of the set, if it has one.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Items in store order.
    /// </summary>
    public List<MediaItemState> Items { get; set; } = [];
}

/// <summary>
/// One item of a media set.
/// </summary>
public sealed class MediaItemState
{
    /// <summary>
    /// The store's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Checksum the store keeps (App Store: source MD5; Play: SHA-256).
    /// </summary>
    public string Checksum { get; set; }

    /// <summary>
    /// Width, when known.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height, when known.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Processing state (COMPLETE, FAILED...).
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Address of the item (Play), if any.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// An App Store version.
/// </summary>
public sealed class StoreVersionState
{
    /// <summary>
    /// Version id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Version string.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Platform.
    /// </summary>
    public string Platform { get; set; }

    /// <summary>
    /// Store state.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Release type (MANUAL, AFTER_APPROVAL, SCHEDULED).
    /// </summary>
    public string ReleaseType { get; set; }

    /// <summary>
    /// Earliest release instant of a scheduled release.
    /// </summary>
    public DateTime? EarliestReleaseDate { get; set; }

    /// <summary>
    /// Phased release state (INACTIVE, ACTIVE, PAUSED, COMPLETE), null without one.
    /// </summary>
    public string PhasedReleaseState { get; set; }

    /// <summary>
    /// Day of the phased release (1-7).
    /// </summary>
    public int? PhasedReleaseDay { get; set; }

    /// <summary>
    /// UTC instant the phased release started.
    /// </summary>
    public DateTime? PhasedReleaseStartDate { get; set; }

    /// <summary>
    /// Attached build id.
    /// </summary>
    public string BuildId { get; set; }

    /// <summary>
    /// Attached build number.
    /// </summary>
    public string BuildNumber { get; set; }
}

/// <summary>
/// The build attached to a version.
/// </summary>
public sealed class AttachedBuildState
{
    /// <summary>
    /// Build id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Build number.
    /// </summary>
    public string BuildNumber { get; set; }

    /// <summary>
    /// Processing state.
    /// </summary>
    public BuildProcessingState ProcessingState { get; set; }

    /// <summary>
    /// Export compliance answer; null while unanswered.
    /// </summary>
    public bool? UsesNonExemptEncryption { get; set; }
}

/// <summary>
/// A Play track.
/// </summary>
public sealed class TrackState
{
    /// <summary>
    /// Track name (internal, alpha, beta, production or a custom closed track).
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Releases of the track.
    /// </summary>
    public List<TrackReleaseState> Releases { get; set; } = [];

    /// <summary>
    /// Google Groups of the testers (testing tracks).
    /// </summary>
    public List<string> GoogleGroups { get; set; }
}

/// <summary>
/// A release on a Play track.
/// </summary>
public sealed class TrackReleaseState
{
    /// <summary>
    /// Release name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Version codes.
    /// </summary>
    public List<string> VersionCodes { get; set; } = [];

    /// <summary>
    /// draft, inProgress, halted or completed.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Fraction of users of a staged rollout (0-1).
    /// </summary>
    public double? UserFraction { get; set; }

    /// <summary>
    /// Release notes per language.
    /// </summary>
    public List<ReleaseNote> ReleaseNotes { get; set; } = [];

    /// <summary>
    /// Country targeting (ISO codes); null when the release targets every country of the track.
    /// </summary>
    public List<string> Countries { get; set; }

    /// <summary>
    /// Whether the countries list also includes the rest of the world.
    /// </summary>
    public bool? IncludeRestOfWorld { get; set; }
}

/// <summary>
/// Release notes in one language.
/// </summary>
/// <param name="Language">BCP 47 language.</param>
/// <param name="Text">Notes.</param>
public sealed record ReleaseNote(string Language, string Text);

/// <summary>
/// One entry of an App Store price schedule.
/// </summary>
public sealed class PriceEntryState
{
    /// <summary>
    /// Territory of the price.
    /// </summary>
    public string Territory { get; set; }

    /// <summary>
    /// Price point id.
    /// </summary>
    public string PricePointId { get; set; }

    /// <summary>
    /// Customer price.
    /// </summary>
    public string CustomerPrice { get; set; }

    /// <summary>
    /// First day, null for "now".
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Last day, null for "open ended".
    /// </summary>
    public DateOnly? EndDate { get; set; }
}

/// <summary>
/// What an apply job writes. Field changes use the content model; the structured changes cover what is not a plain value.
/// Secrets are never part of it: <see cref="Secrets"/> is filled by the job in memory and not serialized.
/// </summary>
public sealed class ContentChangeSet
{
    /// <summary>
    /// App Store version to write to.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Field changes (value null clears the field).
    /// </summary>
    public List<FieldChange> Fields { get; set; } = [];

    /// <summary>
    /// Media sets to make match.
    /// </summary>
    public List<MediaSetChange> Media { get; set; } = [];

    /// <summary>
    /// Play track release to write.
    /// </summary>
    public TrackReleaseChange Release { get; set; }

    /// <summary>
    /// Rollout change (Play user fraction, App Store phased release state).
    /// </summary>
    public RolloutChange Rollout { get; set; }

    /// <summary>
    /// Play testers to write.
    /// </summary>
    public TestersChange Testers { get; set; }

    /// <summary>
    /// Android App Bundle to upload (and optionally put on a track).
    /// </summary>
    public BundleChange Bundle { get; set; }

    /// <summary>
    /// Data safety CSV to send.
    /// </summary>
    public string DataSafetyCsv { get; set; }

    /// <summary>
    /// Whether to submit the version for App Review after the other changes.
    /// </summary>
    public bool SubmitForReview { get; set; }

    /// <summary>
    /// Whether the demo account password comes from the app's saved answers.
    /// </summary>
    public bool DemoPasswordFromAnswers { get; set; }

    /// <summary>
    /// A Microsoft Store change and the fingerprint of the content it was planned on (plans of kind
    /// <see cref="ChangePlanKind.MsChange"/>).
    /// </summary>
    public Dtos.MsStoreDtos.MsPlannedChange Microsoft { get; set; }

    /// <summary>
    /// Secret values resolved by the job, in memory only.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, string> Secrets { get; set; } = [];

    /// <summary>
    /// Whether nothing would be written.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => Fields.Count == 0 && Media.Count == 0 && Release == null && Rollout == null && Testers == null && Bundle == null
                           && DataSafetyCsv == null && !SubmitForReview && Microsoft == null;
}

/// <summary>
/// One field change.
/// </summary>
/// <param name="Group">Content group.</param>
/// <param name="Locale">Locale, null for values that are not localized.</param>
/// <param name="Field">Field.</param>
/// <param name="Value">New value; null clears.</param>
public sealed record FieldChange(string Group, string Locale, string Field, string Value);

/// <summary>
/// The desired content of one media set, in order.
/// </summary>
public sealed class MediaSetChange
{
    /// <summary>
    /// Screenshot or preview.
    /// </summary>
    public MediaKind Kind { get; set; }

    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Store class key.
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Desired items in order.
    /// </summary>
    public List<MediaItemChange> Items { get; set; } = [];

    /// <summary>
    /// Poster frame of a preview (App Store time code, e.g. 00:00:05:00).
    /// </summary>
    public string PreviewFrameTimeCode { get; set; }
}

/// <summary>
/// One desired item of a media set: an item already in the store (by checksum) or an uploaded asset.
/// </summary>
public sealed class MediaItemChange
{
    /// <summary>
    /// Asset to upload; null for an item the store already has.
    /// </summary>
    public long? AssetId { get; set; }

    /// <summary>
    /// Object key of the asset.
    /// </summary>
    public string ObjectKey { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// MIME type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Lower case hex MD5 (App Store checksum).
    /// </summary>
    public string Md5 { get; set; }

    /// <summary>
    /// Lower case hex SHA-256 (Play checksum).
    /// </summary>
    public string Sha256 { get; set; }

    /// <summary>
    /// The store's id when the item is already there.
    /// </summary>
    public string ExistingId { get; set; }
}

/// <summary>
/// A Play track release to write.
/// </summary>
public sealed class TrackReleaseChange
{
    /// <summary>
    /// Track.
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Release name; null lets Play name it.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Version codes; empty means the bundle uploaded in the same change set.
    /// </summary>
    public List<string> VersionCodes { get; set; } = [];

    /// <summary>
    /// draft, inProgress or completed.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Staged rollout fraction (0-1) of an inProgress release.
    /// </summary>
    public double? UserFraction { get; set; }

    /// <summary>
    /// Release notes.
    /// </summary>
    public List<ReleaseNote> ReleaseNotes { get; set; } = [];

    /// <summary>
    /// Countries the release targets; null for every country of the track.
    /// </summary>
    public List<string> Countries { get; set; }

    /// <summary>
    /// Whether the countries list includes the rest of the world.
    /// </summary>
    public bool? IncludeRestOfWorld { get; set; }
}

/// <summary>
/// A rollout change.
/// </summary>
public sealed class RolloutChange
{
    /// <summary>
    /// What to do.
    /// </summary>
    public RolloutAction Action { get; set; }

    /// <summary>
    /// Play track (production unless said otherwise).
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// New Play user fraction (0-1).
    /// </summary>
    public double? UserFraction { get; set; }
}

/// <summary>
/// Testers of a Play testing track.
/// </summary>
public sealed class TestersChange
{
    /// <summary>
    /// Track.
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Google Group addresses.
    /// </summary>
    public List<string> GoogleGroups { get; set; } = [];
}

/// <summary>
/// An Android App Bundle to upload from storage.
/// </summary>
public sealed class BundleChange
{
    /// <summary>
    /// Asset of the bundle.
    /// </summary>
    public long AssetId { get; set; }

    /// <summary>
    /// Object key of the bundle.
    /// </summary>
    public string ObjectKey { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }
}
