using Storeasy.Domain.Content;
using System.Globalization;
using System.Text.Json;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// Plans of releases: App Store release type and phased release, Play track releases (with staged rollout fraction,
/// release notes and countries), rollout changes, testers, bundle uploads and the App Review submission.
/// </summary>
public static class ReleasePlanner
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// App Store release type (and scheduled date) and whether the version is released in phases.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <param name="releaseType"></param>
    /// <param name="earliestReleaseDate"></param>
    /// <param name="phasedRelease"></param>
    /// <returns></returns>
    public static PlanDraft AppleReleaseSettings(StoreContentSnapshot snapshot, ReleaseType? releaseType, DateTime? earliestReleaseDate, bool? phasedRelease)
    {
        var desired = new Dictionary<string, string>(StringComparer.Ordinal);

        if (releaseType != null)
        {
            desired[ContentKey.Of(ContentGroups.Version, null, "releaseType")] = releaseType switch
            {
                ReleaseType.Manual => "MANUAL",
                ReleaseType.Scheduled => "SCHEDULED",
                _ => "AFTER_APPROVAL",
            };

            if (releaseType == ReleaseType.Scheduled && earliestReleaseDate != null)
                desired[ContentKey.Of(ContentGroups.Version, null, "earliestReleaseDate")] = earliestReleaseDate.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:00:00+00:00", CultureInfo.InvariantCulture);
        }

        if (phasedRelease != null)
        {
            var current = snapshot.Get(ContentKey.Of(ContentGroups.Phased, null, "state"));

            // Turning phased release on before the release creates it INACTIVE; Apple starts it when the version goes live.
            desired[ContentKey.Of(ContentGroups.Phased, null, "state")] = phasedRelease.Value ? current ?? "INACTIVE" : null;
        }

        return FieldPlanner.Build(ChangePlanKind.ReleaseSettings, $"Release settings of {snapshot.VersionString}", snapshot, desired, g => new PlanCall
        {
            Method = "PATCH",
            Path = g.Key.Group == ContentGroups.Phased ? "/v1/appStoreVersionPhasedReleases" : "/v1/appStoreVersions",
            Target = snapshot.VersionString,
        });
    }

    /// <summary>
    /// A release on a Play track. Version codes must exist (a bundle uploaded before), except when the same plan uploads
    /// the bundle (<paramref name="bundle"/>).
    /// </summary>
    /// <param name="snapshot"></param>
    /// <param name="release"></param>
    /// <param name="bundle"></param>
    /// <returns></returns>
    public static PlanDraft PlayRelease(StoreContentSnapshot snapshot, TrackReleaseChange release, BundleChange bundle)
    {
        var key = ContentKey.Of(ContentGroups.Track, null, release.Track);
        var current = snapshot.Tracks.FirstOrDefault(t => t.Track == release.Track);
        var before = current == null ? null : Describe(current.Releases);
        var codes = release.VersionCodes.Count > 0 ? string.Join(",", release.VersionCodes) : bundle?.FileName ?? "?";
        var after = $"{codes} {release.Status ?? "completed"}{(release.UserFraction is { } f ? $" {f.ToString("P0", CultureInfo.InvariantCulture)}" : string.Empty)}";

        var draft = new PlanDraft
        {
            Kind = bundle == null ? ChangePlanKind.TrackRelease : ChangePlanKind.BundleUpload,
            Title = bundle == null ? $"Release {codes} on {release.Track}" : $"Upload {bundle.FileName} to {release.Track}",
            Track = release.Track,
            VersionString = release.Name ?? (release.VersionCodes.Count > 0 ? release.VersionCodes[^1] : null),
            BaseKeys = [key],
            Items = [new PlanItem { Group = ContentGroups.Track, Field = release.Track, Before = before, After = after, Action = current == null ? ChangeAction.Create : ChangeAction.Update }],
            Changes = new ContentChangeSet { Release = release, Bundle = bundle },
        };

        draft.Items.AddRange(release.ReleaseNotes.Select(n => new PlanItem { Group = ContentGroups.Track, Locale = n.Language, Field = "releaseNotes", After = n.Text, Action = ChangeAction.Update }));

        if (release.Countries != null)
            draft.Items.Add(new PlanItem { Group = ContentGroups.Track, Field = "countries", After = string.Join(",", release.Countries), Action = ChangeAction.Update });

        draft.Calls = [new PlanCall { Method = "POST", Path = "edits.insert", Target = snapshot.ExternalId }];

        if (bundle != null)
            draft.Calls.Add(new PlanCall { Method = "POST", Path = "edits.bundles.upload (resumable)", Target = $"{bundle.FileName} ({bundle.Size} bytes)" });

        draft.Calls.Add(new PlanCall { Method = "PUT", Path = "edits.tracks.update", Target = release.Track });
        draft.Calls.Add(new PlanCall { Method = "POST", Path = "edits.commit", Target = snapshot.ExternalId });

        return draft;
    }

    /// <summary>
    /// A rollout change. Play: the track must have a staged (inProgress or halted) release; the fraction only grows. App
    /// Store: the version must have a phased release. Returns null with <paramref name="errorKey"/> when not possible now.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="snapshot"></param>
    /// <param name="rollout"></param>
    /// <param name="errorKey"></param>
    /// <returns></returns>
    public static PlanDraft Rollout(StoreKind store, StoreContentSnapshot snapshot, RolloutChange rollout, out string errorKey)
    {
        errorKey = null;

        if (store == StoreKind.Apple)
        {
            var state = snapshot.Get(ContentKey.Of(ContentGroups.Phased, null, "state"));
            var target = rollout.Action switch
            {
                RolloutAction.Pause => "PAUSED",
                RolloutAction.Resume => "ACTIVE",
                RolloutAction.Complete => "COMPLETE",
                _ => null,
            };

            if (state == null || target == null || state == "COMPLETE" || state == target)
            {
                errorKey = MessageKey.RolloutInvalid;
                return null;
            }

            return new PlanDraft
            {
                Kind = ChangePlanKind.Rollout,
                Title = $"Phased release of {snapshot.VersionString}: {target}",
                VersionId = snapshot.VersionId,
                VersionString = snapshot.VersionString,
                BaseKeys = [ContentKey.Of(ContentGroups.Phased, null, "state")],
                Items = [new PlanItem { Group = ContentGroups.Phased, Field = "state", Before = state, After = target, Action = ChangeAction.Update }],
                Calls = [new PlanCall { Method = "PATCH", Path = "/v1/appStoreVersionPhasedReleases", Target = snapshot.VersionString }],
                Changes = new ContentChangeSet { VersionId = snapshot.VersionId, Rollout = rollout },
            };
        }

        var track = rollout.Track ?? ChangePlanRules.ProductionTrack;
        var staged = snapshot.Tracks.FirstOrDefault(t => t.Track == track)?.Releases.FirstOrDefault(r => r.Status is "inProgress" or "halted");

        var invalid = staged == null
                      || (rollout.Action == RolloutAction.SetPercentage && (rollout.UserFraction is not (> 0 and < 1) || rollout.UserFraction <= (staged.UserFraction ?? 0)))
                      || (rollout.Action == RolloutAction.Halt && staged.Status == "halted")
                      || (rollout.Action == RolloutAction.Resume && staged.Status != "halted")
                      || rollout.Action == RolloutAction.Pause;

        if (invalid)
        {
            errorKey = MessageKey.RolloutInvalid;
            return null;
        }

        rollout.Track = track;

        var after = rollout.Action switch
        {
            RolloutAction.SetPercentage => $"inProgress {rollout.UserFraction?.ToString("P0", CultureInfo.InvariantCulture)}",
            RolloutAction.Halt => "halted",
            RolloutAction.Resume => $"inProgress {staged.UserFraction?.ToString("P0", CultureInfo.InvariantCulture)}",
            _ => "completed",
        };

        return new PlanDraft
        {
            Kind = ChangePlanKind.Rollout,
            Title = $"Rollout of {string.Join(",", staged.VersionCodes)} on {track}: {after}",
            Track = track,
            VersionString = staged.Name ?? staged.VersionCodes.LastOrDefault(),
            BaseKeys = [ContentKey.Of(ContentGroups.Track, null, track)],
            Items = [new PlanItem { Group = ContentGroups.Track, Field = track, Before = $"{staged.Status} {staged.UserFraction?.ToString("P0", CultureInfo.InvariantCulture)}".Trim(), After = after, Action = ChangeAction.Update }],
            Calls =
            [
                new PlanCall { Method = "POST", Path = "edits.insert", Target = snapshot.ExternalId },
                new PlanCall { Method = "PUT", Path = "edits.tracks.update", Target = track },
                new PlanCall { Method = "POST", Path = "edits.commit", Target = snapshot.ExternalId },
            ],
            Changes = new ContentChangeSet { Rollout = rollout },
        };
    }

    /// <summary>
    /// Tester Google Groups of a Play testing track.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <param name="track"></param>
    /// <param name="groups"></param>
    /// <returns></returns>
    public static PlanDraft Testers(StoreContentSnapshot snapshot, string track, IReadOnlyList<string> groups)
    {
        var current = snapshot.Tracks.FirstOrDefault(t => t.Track == track)?.GoogleGroups ?? [];
        var desired = groups.Select(g => g.Trim()).Where(g => g.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var same = current.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(desired.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

        return new PlanDraft
        {
            Kind = ChangePlanKind.Testers,
            Title = $"Testers of {track}: {desired.Count} groups",
            Track = track,
            BaseKeys = [ContentKey.Of(ContentGroups.Testers, null, track)],
            Items = same ? [] : [new PlanItem { Group = ContentGroups.Testers, Field = track, Before = string.Join(", ", current), After = string.Join(", ", desired), Action = ChangeAction.Update }],
            Calls =
            [
                new PlanCall { Method = "POST", Path = "edits.insert", Target = snapshot.ExternalId },
                new PlanCall { Method = "PUT", Path = "edits.testers.update", Target = track },
                new PlanCall { Method = "POST", Path = "edits.commit", Target = snapshot.ExternalId },
            ],
            Changes = same ? new ContentChangeSet() : new ContentChangeSet { Testers = new TestersChange { Track = track, GoogleGroups = desired } },
        };
    }

    /// <summary>
    /// Submission of the version for App Review.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <returns></returns>
    public static PlanDraft Submit(StoreContentSnapshot snapshot) => new()
    {
        Kind = ChangePlanKind.SubmitForReview,
        Title = $"Submit {snapshot.VersionString} for review",
        VersionId = snapshot.VersionId,
        VersionString = snapshot.VersionString,
        BaseKeys = [ContentKey.Of(ContentGroups.Build, null, "build")],
        Items = [new PlanItem { Group = ContentGroups.Version, Field = "state", Before = snapshot.VersionState, After = "WAITING_FOR_REVIEW", Action = ChangeAction.Update }],
        Calls =
        [
            new PlanCall { Method = "POST", Path = "/v1/reviewSubmissions", Target = snapshot.ExternalId },
            new PlanCall { Method = "POST", Path = "/v1/reviewSubmissionItems", Target = snapshot.VersionString },
            new PlanCall { Method = "PATCH", Path = "/v1/reviewSubmissions (submitted)", Target = snapshot.VersionString },
        ],
        Changes = new ContentChangeSet { VersionId = snapshot.VersionId, SubmitForReview = true },
    };

    private static string Describe(IEnumerable<TrackReleaseState> releases)
        => string.Join("; ", releases.Select(r => $"{string.Join(",", r.VersionCodes)} {r.Status}{(r.UserFraction is { } f ? $" {f.ToString("P0", CultureInfo.InvariantCulture)}" : string.Empty)}"));

    /// <summary>
    /// JSON of a track's releases (as <see cref="ContentSnapshots.Flatten"/> keeps it).
    /// </summary>
    /// <param name="releases"></param>
    /// <returns></returns>
    public static string Serialize(IEnumerable<TrackReleaseState> releases) => JsonSerializer.Serialize(releases, _json);
}
