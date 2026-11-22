using System.Globalization;

namespace Storeasy.Application.Utils.Chrome;

/// <summary>
/// The content snapshot of a Chrome Web Store item: what <c>fetchStatus</c> reports, as snapshot values. The API has no
/// read of the store listing, so the snapshot has no listing values; the plan's conflict check compares these values.
/// </summary>
public static class ChromeContent
{
    /// <summary>State of the published revision.</summary>
    public const string PublishedStateKey = "chrome.published.state";

    /// <summary>Version of the published revision.</summary>
    public const string PublishedVersionKey = "chrome.published.version";

    /// <summary>Deploy percentage of the published revision.</summary>
    public const string PublishedPercentageKey = "chrome.published.percentage";

    /// <summary>State of the submitted revision.</summary>
    public const string SubmittedStateKey = "chrome.submitted.state";

    /// <summary>Version of the submitted revision.</summary>
    public const string SubmittedVersionKey = "chrome.submitted.version";

    /// <summary>Deploy percentage of the submitted revision.</summary>
    public const string SubmittedPercentageKey = "chrome.submitted.percentage";

    /// <summary>State of the last upload.</summary>
    public const string UploadStateKey = "chrome.upload.state";

    /// <summary>Policy flags (<c>warned</c>, <c>takenDown</c>).</summary>
    public const string FlagsKey = "chrome.flags";

    /// <summary>
    /// Keys every Chrome plan is checked against when it is approved.
    /// </summary>
    public static readonly IReadOnlyList<string> BaseKeys = [PublishedStateKey, PublishedVersionKey, PublishedPercentageKey, SubmittedStateKey, SubmittedVersionKey];

    /// <summary>
    /// The snapshot of a status read.
    /// </summary>
    /// <param name="itemId"></param>
    /// <param name="status"></param>
    /// <returns></returns>
    public static StoreContentSnapshot ToSnapshot(string itemId, ChromeItemStatusRead status)
    {
        var published = status.Published;
        var submitted = status.Submitted;
        var snapshot = new StoreContentSnapshot
        {
            Store = StoreKind.Chrome,
            ExternalId = itemId,
            VersionId = published?.Version,
            VersionString = published?.Version,
            VersionState = published?.State.ToString(),
            VersionEditable = false,
            HasLiveVersion = published?.State is ChromeItemState.Published or ChromeItemState.PublishedToTesters,
            Platform = "chrome",
        };

        if (published != null)
            snapshot.Versions.Add(new StoreVersionState { Id = "published", VersionString = published.Version, Platform = "chrome", State = published.State.ToString() });

        if (submitted != null)
            snapshot.Versions.Add(new StoreVersionState { Id = "submitted", VersionString = submitted.Version, Platform = "chrome", State = submitted.State.ToString() });

        snapshot.Values[PublishedStateKey] = (published?.State ?? ChromeItemState.None).ToString();
        snapshot.Values[PublishedVersionKey] = published?.Version ?? string.Empty;
        snapshot.Values[PublishedPercentageKey] = published?.DeployPercentage?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        snapshot.Values[SubmittedStateKey] = (submitted?.State ?? ChromeItemState.None).ToString();
        snapshot.Values[SubmittedVersionKey] = submitted?.Version ?? string.Empty;
        snapshot.Values[SubmittedPercentageKey] = submitted?.DeployPercentage?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        snapshot.Values[UploadStateKey] = status.LastUploadState.ToString();
        snapshot.Values[FlagsKey] = string.Join(',', new[] { status.Warned ? "warned" : null, status.TakenDown ? "takenDown" : null }.Where(f => f != null));

        return snapshot;
    }

    /// <summary>
    /// The status a snapshot was made from.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <returns></returns>
    public static ChromeItemStatusRead FromSnapshot(StoreContentSnapshot snapshot)
    {
        var flags = (snapshot.Get(FlagsKey) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);

        return new ChromeItemStatusRead(snapshot.ExternalId,
                                        Revision(snapshot, PublishedStateKey, PublishedVersionKey, PublishedPercentageKey),
                                        Revision(snapshot, SubmittedStateKey, SubmittedVersionKey, SubmittedPercentageKey),
                                        Enum.TryParse<ChromeUploadState>(snapshot.Get(UploadStateKey), out var upload) ? upload : ChromeUploadState.None,
                                        flags.Contains("warned"),
                                        flags.Contains("takenDown"));
    }

    private static ChromeRevisionRead Revision(StoreContentSnapshot snapshot, string stateKey, string versionKey, string percentageKey)
    {
        var state = Enum.TryParse<ChromeItemState>(snapshot.Get(stateKey), out var parsed) ? parsed : ChromeItemState.None;
        var version = snapshot.Get(versionKey);

        if (state == ChromeItemState.None && string.IsNullOrEmpty(version))
            return null;

        return new ChromeRevisionRead(state,
                                      string.IsNullOrEmpty(version) ? null : version,
                                      int.TryParse(snapshot.Get(percentageKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var percentage) ? percentage : null);
    }
}
