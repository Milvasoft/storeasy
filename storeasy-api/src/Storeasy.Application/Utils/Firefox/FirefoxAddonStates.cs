using Storeasy.Domain.Firefox;

namespace Storeasy.Application.Utils.Firefox;

/// <summary>
/// A change storeasy saw between two reads of an add-on.
/// </summary>
/// <param name="Subject"><c>addon</c> or <c>version</c>.</param>
/// <param name="Previous">State before.</param>
/// <param name="Current">State now.</param>
/// <param name="Version">Version (for a version change; the listed version for the add-on).</param>
/// <param name="Channel">Channel of the version.</param>
/// <param name="Action">Audit action of the change.</param>
public sealed record FirefoxStateTransition(string Subject, string Previous, string Current, string Version, FirefoxVersionChannel Channel, AuditAction Action);

/// <summary>
/// Copies a live read into a <see cref="FirefoxAddon"/> and names what changed, so the sync and the plan's write record the
/// history the same way.
/// </summary>
public static class FirefoxAddonStates
{
    /// <summary>Subject of the add-on status.</summary>
    public const string Addon = "addon";

    /// <summary>Subject of a version's file status.</summary>
    public const string Version = "version";

    /// <summary>
    /// Copies <paramref name="state"/> into <paramref name="row"/>; returns the changes (none on the first read, which only
    /// fills the history).
    /// </summary>
    /// <param name="row"></param>
    /// <param name="state"></param>
    /// <param name="now"></param>
    /// <returns></returns>
    public static List<FirefoxStateTransition> Merge(FirefoxAddon row, FirefoxAddonState state, DateTime now)
    {
        var first = row.SyncedDate == null;
        var changes = new List<FirefoxStateTransition>();
        var history = row.StatusHistory ?? [];
        var addon = state.Addon;
        var latest = state.Latest;
        var latestVersion = Trim(latest?.Version, FirefoxAddon.VersionMaxLength);
        var latestStatus = latest?.FileStatus ?? FirefoxFileStatus.None;
        var latestChannel = latest?.Channel ?? FirefoxVersionChannel.None;

        if (addon.Status != row.Status)
        {
            history.Add(new FirefoxStateChange { Subject = Addon, State = addon.Status.ToString(), Version = addon.CurrentVersion?.Version, Date = now });

            if (!first)
                changes.Add(new FirefoxStateTransition(Addon, row.Status.ToString(), addon.Status.ToString(), addon.CurrentVersion?.Version ?? row.ListedVersion, FirefoxVersionChannel.Listed, AuditAction.FirefoxAddonStatusChanged));
        }

        if (latestVersion != row.LatestVersion || latestStatus != row.LatestFileStatus)
        {
            history.Add(new FirefoxStateChange { Subject = Version, State = latestStatus.ToString(), Version = latestVersion, Channel = latestChannel, Date = now });

            // A new version showing up is told by the plan that uploaded it, or by its first review result; a status change
            // of the same version is a review result.
            var sameVersion = latestVersion == row.LatestVersion;

            if (!first && (sameVersion || latestStatus != FirefoxFileStatus.Unreviewed))
            {
                var action = latestStatus switch
                {
                    FirefoxFileStatus.Public => AuditAction.FirefoxApproved,
                    FirefoxFileStatus.Disabled => AuditAction.FirefoxRejected,
                    _ => AuditAction.FirefoxReviewStateChanged,
                };

                changes.Add(new FirefoxStateTransition(Version, sameVersion ? row.LatestFileStatus.ToString() : FirefoxFileStatus.None.ToString(), latestStatus.ToString(), latestVersion, latestChannel, action));
            }
        }

        row.AddonId = addon.Id;
        row.Guid = Trim(addon.Guid, FirefoxAddon.GuidMaxLength);
        row.Slug = Trim(addon.Slug, FirefoxAddon.SlugMaxLength);
        row.Status = addon.Status;
        row.IsDisabled = addon.IsDisabled;
        row.DefaultLocale = Trim(addon.DefaultLocale, FirefoxAddon.LocaleMaxLength);
        row.ListedVersion = Trim(addon.CurrentVersion?.Version, FirefoxAddon.VersionMaxLength);
        row.LatestVersion = latestVersion;
        row.LatestVersionId = latest?.Id;
        row.LatestChannel = latestChannel;
        row.LatestFileStatus = latestStatus;
        row.Rating = addon.RatingCount > 0 ? addon.Rating : null;
        row.RatingCount = addon.RatingCount;
        row.AverageDailyUsers = addon.AverageDailyUsers;
        row.WeeklyDownloads = addon.WeeklyDownloads;
        row.IsPending = state.Versions.Any(v => v.FileStatus == FirefoxFileStatus.Unreviewed && !v.IsDisabled) || addon.Status == FirefoxAddonStatus.Nominated;
        row.StatusHistory = [.. history.TakeLast(100)];
        row.SyncedDate = now;
        row.SyncError = null;

        return changes;
    }

    private static string Trim(string value, int length) => value?.Length > length ? value[..length] : value;
}
