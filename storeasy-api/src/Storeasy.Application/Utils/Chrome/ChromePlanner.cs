using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Intake;
using System.Globalization;

namespace Storeasy.Application.Utils.Chrome;

/// <summary>
/// A package a Chrome plan uploads: an extension ZIP intake received and still stores.
/// </summary>
/// <param name="SessionId">Intake session.</param>
/// <param name="FileName">File name.</param>
/// <param name="ObjectKey">Storage key.</param>
/// <param name="Size">Size.</param>
/// <param name="Sha256">SHA-256.</param>
/// <param name="Package">What intake read from manifest.json.</param>
public sealed record ChromePlanPackage(long SessionId, string FileName, string ObjectKey, long Size, string Sha256, ArtifactPackageInfo Package);

/// <summary>
/// What a Chrome plan is built from.
/// </summary>
/// <param name="ItemId">Extension id.</param>
/// <param name="Status">The item's status, read live.</param>
/// <param name="Kind">What to write.</param>
/// <param name="Package">Package to upload first (publish only), or null.</param>
/// <param name="Staged">Publish: stage instead of publishing after approval.</param>
/// <param name="DeployPercentage">Publish: initial percentage; deploy percentage: the new one.</param>
/// <param name="SkipReview">Publish: ask to skip the review.</param>
/// <param name="TargetVersion">Rollback: version to publish again.</param>
/// <param name="NewVersion">Rollback: its new version number.</param>
/// <param name="Reason">Rollback: reason.</param>
/// <param name="WeeklyUsers">Seven day users, when the last sync read them.</param>
public sealed record ChromePlanInput(string ItemId,
                                     ChromeItemStatusRead Status,
                                     ChromeChangeKind Kind,
                                     ChromePlanPackage Package,
                                     bool Staged,
                                     int? DeployPercentage,
                                     bool SkipReview,
                                     string TargetVersion,
                                     string NewVersion,
                                     string Reason,
                                     long? WeeklyUsers);

/// <summary>
/// The plan, or the problems that stop it.
/// </summary>
/// <param name="Draft">The plan draft; null when blocked.</param>
/// <param name="Issues">Every problem found (blocking and warnings).</param>
public sealed record ChromePlanResult(PlanDraft Draft, List<ChromeIssue> Issues)
{
    /// <summary>
    /// Whether a problem stops the plan.
    /// </summary>
    public bool IsBlocked => this.Draft == null;
}

/// <summary>
/// Builds Chrome Web Store change plans from the item's live status. Every plan is one API write (with the package upload
/// before a publish); nothing is sent until the plan is approved.
/// </summary>
public static class ChromePlanner
{
    /// <summary>
    /// Group of package items.
    /// </summary>
    public const string PackageGroup = "Package";

    /// <summary>
    /// Group of release items.
    /// </summary>
    public const string ReleaseGroup = "Release";

    /// <summary>
    /// Fewest seven day users of an item the store lets roll out to a percentage.
    /// </summary>
    public const long PartialRolloutMinUsers = 10000;

    /// <summary>
    /// Builds the plan.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static ChromePlanResult Build(ChromePlanInput input)
    {
        var issues = new List<ChromeIssue>();
        var published = input.Status?.Published;
        var submitted = input.Status?.Submitted;
        var items = new List<PlanItem>();
        var change = new ChromePlannedChange { Kind = input.Kind, ItemId = input.ItemId };
        var calls = new List<PlanCall>();
        string title;
        string version = null;

        if (input.Status?.TakenDown == true)
            issues.Add(new ChromeIssue(MessageKey.ChromeTakenDown, false));

        switch (input.Kind)
        {
            case ChromeChangeKind.Publish:
            {
                if (submitted?.State == ChromeItemState.PendingReview)
                    issues.Add(new ChromeIssue(MessageKey.ChromeSubmissionPending, true, submitted.Version ?? "-"));

                if (input.Package != null)
                {
                    var package = input.Package.Package;

                    version = package?.VersionName;

                    if (package == null || package.Kind != PackageKind.ExtensionZip)
                    {
                        issues.Add(new ChromeIssue(MessageKey.ChromePackageNotExtension, true, input.Package.SessionId));
                    }
                    else
                    {
                        // The intake checks, except the version comparison, which is made again against the live status.
                        foreach (var (blocking, key, _, args) in ChromeManifest.IssuesOf(package).Where(i => i.Key != MessageKey.ChromeVersionNotHigher))
                            issues.Add(new ChromeIssue(key, blocking, args));

                        foreach (var (other, what) in new[] { (published?.Version, "published"), (submitted?.State is ChromeItemState.Staged ? submitted.Version : null, "staged") })
                        {
                            if (other != null && ChromeVersions.IsValid(version) && ChromeVersions.Compare(version, other) <= 0)
                                issues.Add(new ChromeIssue(MessageKey.ChromeVersionNotHigher, true, version, other, what));
                        }
                    }

                    change.SessionId = input.Package.SessionId;
                    change.ObjectKey = input.Package.ObjectKey;
                    change.Size = input.Package.Size;
                    change.Sha256 = input.Package.Sha256;

                    items.Add(new PlanItem { Group = PackageGroup, Field = "package", Before = published?.Version, After = $"{input.Package.FileName} ({version})", Action = ChangeAction.Update });
                    calls.Add(new PlanCall { Method = "POST", Path = $"/upload/v2/publishers/{{publisherId}}/items/{input.ItemId}:upload", Target = input.Package.FileName });
                }
                else
                {
                    issues.Add(new ChromeIssue(MessageKey.ChromeDraftSubmitted, false));
                }

                if (input.DeployPercentage is int percentage)
                {
                    if (percentage is < 0 or > 100)
                        issues.Add(new ChromeIssue(MessageKey.ChromePercentageInvalid, true, percentage));
                    else if (percentage < 100 && input.WeeklyUsers is long users && users <= PartialRolloutMinUsers)
                        issues.Add(new ChromeIssue(MessageKey.ChromePercentageUsers, false, users));

                    items.Add(new PlanItem { Group = ReleaseGroup, Field = "deployPercentage", Before = published?.DeployPercentage?.ToString(CultureInfo.InvariantCulture), After = percentage.ToString(CultureInfo.InvariantCulture), Action = ChangeAction.Update });
                }

                if (input.SkipReview)
                    issues.Add(new ChromeIssue(MessageKey.ChromeSkipReviewHint, false));

                change.PackageVersion = version;
                change.Staged = input.Staged;
                change.DeployPercentage = input.DeployPercentage;
                change.SkipReview = input.SkipReview;

                items.Add(new PlanItem { Group = ReleaseGroup, Field = "publishType", After = input.Staged ? "STAGED_PUBLISH" : "DEFAULT_PUBLISH", Action = ChangeAction.Update });

                if (input.SkipReview)
                    items.Add(new PlanItem { Group = ReleaseGroup, Field = "skipReview", After = "true", Action = ChangeAction.Update });

                calls.Add(new PlanCall { Method = "POST", Path = $"/v2/publishers/{{publisherId}}/items/{input.ItemId}:publish", Target = input.Staged ? "STAGED_PUBLISH" : "DEFAULT_PUBLISH" });
                title = version == null ? $"Submit {input.ItemId}" : $"Submit {input.ItemId} {version}";
                break;
            }
            case ChromeChangeKind.PublishStaged:
            {
                if (submitted?.State != ChromeItemState.Staged)
                    issues.Add(new ChromeIssue(MessageKey.ChromeNotStaged, true));

                version = submitted?.Version;
                change.PackageVersion = version;
                items.Add(new PlanItem { Group = ReleaseGroup, Field = "state", Before = ChromeItemState.Staged.ToString(), After = ChromeItemState.Published.ToString(), Action = ChangeAction.Update });
                calls.Add(new PlanCall { Method = "POST", Path = $"/v2/publishers/{{publisherId}}/items/{input.ItemId}:publish", Target = "staged revision" });
                title = $"Publish staged {input.ItemId} {version}".TrimEnd();
                break;
            }
            case ChromeChangeKind.DeployPercentage:
            {
                var current = published?.DeployPercentage ?? 0;

                if (published?.State is not (ChromeItemState.Published or ChromeItemState.PublishedToTesters))
                    issues.Add(new ChromeIssue(MessageKey.ChromeNotPublished, true));

                if (input.DeployPercentage is not int next || next is < 1 or > 100)
                    issues.Add(new ChromeIssue(MessageKey.ChromePercentageInvalid, true, input.DeployPercentage?.ToString(CultureInfo.InvariantCulture) ?? "-"));
                else if (next <= current)
                    issues.Add(new ChromeIssue(MessageKey.ChromePercentageNotHigher, true, current));

                if (input.WeeklyUsers is long users && users <= PartialRolloutMinUsers)
                    issues.Add(new ChromeIssue(MessageKey.ChromePercentageUsers, false, users));

                version = published?.Version;
                change.PackageVersion = version;
                change.DeployPercentage = input.DeployPercentage;
                items.Add(new PlanItem { Group = ReleaseGroup, Field = "publishedDeployPercentage", Before = current.ToString(CultureInfo.InvariantCulture), After = input.DeployPercentage?.ToString(CultureInfo.InvariantCulture), Action = ChangeAction.Update });
                calls.Add(new PlanCall { Method = "POST", Path = $"/v2/publishers/{{publisherId}}/items/{input.ItemId}:setPublishedDeployPercentage", Target = $"{input.DeployPercentage}%" });
                title = $"Roll out {input.ItemId} {version} to {input.DeployPercentage}%";
                break;
            }
            case ChromeChangeKind.CancelSubmission:
            {
                if (submitted?.State is not (ChromeItemState.PendingReview or ChromeItemState.Staged))
                    issues.Add(new ChromeIssue(MessageKey.ChromeNothingToCancel, true));

                version = submitted?.Version;
                change.PackageVersion = version;
                items.Add(new PlanItem { Group = ReleaseGroup, Field = "submission", Before = $"{submitted?.State} {version}".Trim(), After = ChromeItemState.Cancelled.ToString(), Action = ChangeAction.Delete });
                calls.Add(new PlanCall { Method = "POST", Path = $"/v2/publishers/{{publisherId}}/items/{input.ItemId}:cancelSubmission", Target = version ?? input.ItemId });
                title = $"Cancel the submission of {input.ItemId} {version}".TrimEnd();
                break;
            }
            case ChromeChangeKind.Rollback:
            {
                if (published?.State is not (ChromeItemState.Published or ChromeItemState.PublishedToTesters) || published.Version == null)
                    issues.Add(new ChromeIssue(MessageKey.ChromeNotPublished, true));

                var newVersion = input.NewVersion?.Trim();
                var target = string.IsNullOrWhiteSpace(input.TargetVersion) ? null : input.TargetVersion.Trim();

                if (!ChromeVersions.IsValid(newVersion))
                    issues.Add(new ChromeIssue(MessageKey.ChromeVersionInvalid, true, newVersion ?? "-"));
                else if (published?.Version != null && ChromeVersions.Compare(newVersion, published.Version) <= 0)
                    issues.Add(new ChromeIssue(MessageKey.ChromeVersionNotHigher, true, newVersion, published.Version, "published"));

                if (target != null && !ChromeVersions.IsValid(target))
                    issues.Add(new ChromeIssue(MessageKey.ChromeVersionInvalid, true, target));

                if (string.IsNullOrWhiteSpace(input.Reason))
                    issues.Add(new ChromeIssue(MessageKey.ChromeRollbackReason, true));

                version = newVersion;
                change.PackageVersion = newVersion;
                change.TargetVersion = target;
                change.NewVersion = newVersion;
                change.Reason = input.Reason?.Trim();
                items.Add(new PlanItem { Group = PackageGroup, Field = "rollback", Before = published?.Version, After = $"{target ?? "last published"} as {newVersion}", Action = ChangeAction.Update });
                calls.Add(new PlanCall { Method = "POST", Path = $"/v2/publishers/{{publisherId}}/items/{input.ItemId}:rollback", Target = $"{target ?? "last published"} as {newVersion}" });
                title = $"Roll back {input.ItemId} to {target ?? "the last published package"} as {newVersion}";
                break;
            }
            default:
                issues.Add(new ChromeIssue(MessageKey.ChromeNoChanges, true));
                title = input.ItemId;
                break;
        }

        if (issues.Any(i => i.Blocking))
            return new ChromePlanResult(null, issues);

        var draft = new PlanDraft
        {
            Kind = ChangePlanKind.ChromeChange,
            Title = $"Chrome Web Store: {title}",
            Items = items,
            CustomChanges = change,
            VersionId = version,
            VersionString = version,
            BaseKeys = [.. ChromeContent.BaseKeys],
            RequiredPermission = OrgPermission.SubmitForReview,
            ConfirmText = version == null ? input.ItemId : $"{input.ItemId} {version}",
            Calls = calls,
        };

        return new ChromePlanResult(draft, issues);
    }

}
