using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Content;

/// <summary>
/// Pure rules of change plans: who may approve which plan (docs/urun-plani.md section 6), which plans need a typed
/// confirmation, and the state a plan shows. No database.
/// </summary>
public static class ChangePlanRules
{
    /// <summary>
    /// Name of the Play production track.
    /// </summary>
    public const string ProductionTrack = "production";

    /// <summary>
    /// Permission needed to approve a plan of <paramref name="kind"/> (writing to <paramref name="track"/> on Play).
    /// Production releases, phased releases and rollouts need <see cref="OrgPermission.ReleaseProduction"/>, test tracks and
    /// testers <see cref="OrgPermission.ReleaseTest"/>, a submission <see cref="OrgPermission.SubmitForReview"/>, everything
    /// else <see cref="OrgPermission.EditContent"/>.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="store"></param>
    /// <param name="track"></param>
    /// <returns></returns>
    public static OrgPermission RequiredPermission(ChangePlanKind kind, StoreKind store, string track) => kind switch
    {
        ChangePlanKind.SubmitForReview => OrgPermission.SubmitForReview,
        ChangePlanKind.TrackRelease or ChangePlanKind.BundleUpload => IsProduction(track) ? OrgPermission.ReleaseProduction : OrgPermission.ReleaseTest,
        ChangePlanKind.Rollout => store == StoreKind.Apple || IsProduction(track) ? OrgPermission.ReleaseProduction : OrgPermission.ReleaseTest,
        ChangePlanKind.Testers => OrgPermission.ReleaseTest,
        _ => OrgPermission.EditContent,
    };

    /// <summary>
    /// Whether a plan needs the approver to type its confirmation text: submissions and releases to Play production.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="track"></param>
    /// <returns></returns>
    public static bool NeedsConfirmation(ChangePlanKind kind, string track)
        => kind == ChangePlanKind.SubmitForReview || (kind is ChangePlanKind.TrackRelease or ChangePlanKind.BundleUpload && IsProduction(track));

    /// <summary>
    /// The text an approver types: the app name and the version, e.g. "Kumbara 1.0".
    /// </summary>
    /// <param name="appName"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public static string ConfirmationText(string appName, string version)
        => string.IsNullOrWhiteSpace(version) ? appName?.Trim() : $"{appName?.Trim()} {version.Trim()}";

    /// <summary>
    /// Whether <paramref name="typed"/> matches <paramref name="expected"/> (surrounding and repeated spaces ignored, case
    /// sensitive). A plan without a confirmation text accepts anything.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="typed"></param>
    /// <returns></returns>
    public static bool ConfirmationMatches(string expected, string typed)
    {
        if (expected == null)
            return true;

        static string Squash(string value) => string.Join(' ', (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return string.Equals(Squash(expected), Squash(typed), StringComparison.Ordinal);
    }

    /// <summary>
    /// The state a plan shows: a pending plan past its expiry is <see cref="ChangePlanStatus.Expired"/>; an approved plan
    /// whose apply job failed or was cancelled is <see cref="ChangePlanStatus.Failed"/>.
    /// </summary>
    /// <param name="stored"></param>
    /// <param name="expiryDate"></param>
    /// <param name="jobStatus"></param>
    /// <param name="now"></param>
    /// <returns></returns>
    public static ChangePlanStatus Effective(ChangePlanStatus stored, DateTime expiryDate, StoreJobStatus? jobStatus, DateTime now) => stored switch
    {
        ChangePlanStatus.Pending when expiryDate <= now => ChangePlanStatus.Expired,
        ChangePlanStatus.Approved when jobStatus is StoreJobStatus.Failed or StoreJobStatus.Cancelled => ChangePlanStatus.Failed,
        _ => stored,
    };

    /// <summary>
    /// Audit action of the writes of a plan of <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static AuditAction AuditActionOf(ChangePlanKind kind) => kind switch
    {
        ChangePlanKind.Listing or ChangePlanKind.Details => AuditAction.ListingApplied,
        ChangePlanKind.Screenshots or ChangePlanKind.Previews => AuditAction.MediaApplied,
        ChangePlanKind.TrackRelease or ChangePlanKind.BundleUpload or ChangePlanKind.ReleaseSettings => AuditAction.ReleaseApplied,
        ChangePlanKind.Rollout => AuditAction.RolloutChanged,
        ChangePlanKind.BuildLink or ChangePlanKind.ExportCompliance => AuditAction.BuildLinked,
        ChangePlanKind.SubmitForReview => AuditAction.SubmittedForReview,
        _ => AuditAction.ContentApplied,
    };

    /// <summary>
    /// Whether <paramref name="track"/> is the Play production track.
    /// </summary>
    /// <param name="track"></param>
    /// <returns></returns>
    public static bool IsProduction(string track) => string.Equals(track, ProductionTrack, StringComparison.OrdinalIgnoreCase);
}
