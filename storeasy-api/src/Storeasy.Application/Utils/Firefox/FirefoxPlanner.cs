using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Content;
using Storeasy.Domain.Intake;
using System.Globalization;
using System.Net.Mail;

namespace Storeasy.Application.Utils.Firefox;

/// <summary>
/// A file a Firefox plan sends, as the plan command found it.
/// </summary>
/// <param name="File">Storage reference.</param>
/// <param name="Width">Image width, when the file is an image whose header was read.</param>
/// <param name="Height">Image height.</param>
/// <param name="Format">Image format (<c>png</c>, <c>jpeg</c>), when read.</param>
public sealed record FirefoxPlanFile(FirefoxPlannedFile File, int? Width = null, int? Height = null, string Format = null);

/// <summary>
/// An intake package a Firefox plan uploads.
/// </summary>
/// <param name="SessionId">Intake session.</param>
/// <param name="File">Storage reference.</param>
/// <param name="Package">What intake read from manifest.json.</param>
public sealed record FirefoxPlanPackage(long SessionId, FirefoxPlannedFile File, ArtifactPackageInfo Package);

/// <summary>
/// What a Firefox plan is built from.
/// </summary>
public sealed class FirefoxPlanInput
{
    /// <summary>The live read of the add-on.</summary>
    public FirefoxAddonState State { get; set; }

    /// <summary>What to write.</summary>
    public FirefoxChangeKind Kind { get; set; }

    /// <summary>Listing: values to write.</summary>
    public List<FirefoxListingValue> Listing { get; set; } = [];

    /// <summary>Listing: new categories; null keeps them.</summary>
    public List<string> Categories { get; set; }

    /// <summary>Version: the package.</summary>
    public FirefoxPlanPackage Package { get; set; }

    /// <summary>Version: channel.</summary>
    public FirefoxVersionChannel Channel { get; set; } = FirefoxVersionChannel.Listed;

    /// <summary>Version and release notes: release notes per locale.</summary>
    public List<FirefoxLocalizedText> ReleaseNotes { get; set; } = [];

    /// <summary>Version: notes to the reviewer.</summary>
    public string ApprovalNotes { get; set; }

    /// <summary>Version: license slug.</summary>
    public string License { get; set; }

    /// <summary>Version: applications.</summary>
    public List<string> Compatibility { get; set; } = [];

    /// <summary>Version: source code archive.</summary>
    public FirefoxPlanFile Source { get; set; }

    /// <summary>Previews: images to add.</summary>
    public List<FirefoxPlanFile> AddPreviews { get; set; } = [];

    /// <summary>Previews: ids to remove.</summary>
    public List<long> DeletePreviews { get; set; } = [];

    /// <summary>Icon: the new icon.</summary>
    public FirefoxPlanFile Icon { get; set; }

    /// <summary>Release notes: the version (id or version string).</summary>
    public string Version { get; set; }
}

/// <summary>
/// The plan, or the problems that stop it.
/// </summary>
/// <param name="Draft">The plan draft; null when blocked.</param>
/// <param name="Issues">Every problem found (blocking and warnings).</param>
public sealed record FirefoxPlanResult(PlanDraft Draft, List<FirefoxIssue> Issues)
{
    /// <summary>
    /// Whether a problem stops the plan.
    /// </summary>
    public bool IsBlocked => this.Draft == null;
}

/// <summary>
/// Builds Firefox Add-ons change plans from the add-on's live read. Nothing is sent until the plan is approved.
/// </summary>
public static class FirefoxPlanner
{
    /// <summary>Group of listing items.</summary>
    public const string ListingGroup = "Listing";

    /// <summary>Group of version items.</summary>
    public const string VersionGroup = "Version";

    /// <summary>Group of the release notes of an existing version.</summary>
    public const string ReleaseNotesGroup = "ReleaseNotes";

    /// <summary>Group of media items.</summary>
    public const string MediaGroup = "Media";

    /// <summary>Most previews an add-on keeps.</summary>
    public const int MaxPreviews = 10;

    /// <summary>Preview size AMO recommends.</summary>
    public const int PreviewWidth = 1280;

    /// <summary>Preview size AMO recommends.</summary>
    public const int PreviewHeight = 800;

    /// <summary>Icon size AMO recommends.</summary>
    public const int IconSize = 128;

    /// <summary>Applications a version can be made compatible with.</summary>
    public static readonly IReadOnlyList<string> Applications = ["firefox", "android"];

    /// <summary>
    /// Builds the plan.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static FirefoxPlanResult Build(FirefoxPlanInput input)
    {
        var issues = new List<FirefoxIssue>();
        var addon = input.State.Addon;
        var addonId = addon.Id.ToString(CultureInfo.InvariantCulture);
        var change = new FirefoxPlannedChange { Kind = input.Kind, AddonId = addonId };
        var items = new List<PlanItem>();
        var calls = new List<PlanCall>();
        var baseKeys = new List<string> { FirefoxContent.StatusKey };
        string title;
        string version = null;
        var name = addon.NameIn() ?? addon.Slug ?? addonId;
        var basePath = $"/addons/addon/{addonId}";

        if (addon.Status == FirefoxAddonStatus.Disabled)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxAddonDisabled, input.Kind != FirefoxChangeKind.Listing));

        switch (input.Kind)
        {
            case FirefoxChangeKind.Listing:
            {
                foreach (var value in input.Listing)
                    PlanListingValue(addon, value, change, items, issues, baseKeys);

                if (input.Categories != null)
                {
                    var next = input.Categories.Select(c => c?.Trim().ToLowerInvariant()).Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                    var current = addon.Categories.Order(StringComparer.Ordinal).ToList();

                    if (next.Count == 0)
                        issues.Add(new FirefoxIssue(MessageKey.FirefoxCategoriesEmpty, true));

                    foreach (var unknown in next.Where(c => !FirefoxFields.Categories.Contains(c)))
                        issues.Add(new FirefoxIssue(MessageKey.FirefoxCategoryUnknown, false, unknown));

                    if (!next.SequenceEqual(current))
                    {
                        change.Categories = next;
                        baseKeys.Add(FirefoxContent.CategoriesKey);
                        items.Add(new PlanItem { Group = ListingGroup, Field = "Categories", Before = string.Join(", ", current), After = string.Join(", ", next), Action = ChangeAction.Update });
                    }
                }

                if (items.Count > 0)
                    calls.Add(new PlanCall { Method = "PATCH", Path = $"{basePath}/", Target = string.Join(", ", items.Select(i => i.Locale == null ? i.Field : $"{i.Field} ({i.Locale})").Distinct()) });

                title = $"Edit the listing of {name}";
                break;
            }
            case FirefoxChangeKind.Version:
            {
                PlanVersion(input, addon, change, items, calls, issues, baseKeys, basePath);
                version = change.PackageVersion;
                title = $"Submit {name} {version} ({input.Channel.ToString().ToLowerInvariant()})";
                break;
            }
            case FirefoxChangeKind.Previews:
            {
                if (string.Equals(addon.Type, "statictheme", StringComparison.Ordinal))
                    issues.Add(new FirefoxIssue(MessageKey.FirefoxThemePreviews, true));

                foreach (var id in input.DeletePreviews.Distinct())
                {
                    var preview = addon.Previews.FirstOrDefault(p => p.Id == id);

                    if (preview == null)
                    {
                        issues.Add(new FirefoxIssue(MessageKey.FirefoxPreviewNotFound, true, id));
                        continue;
                    }

                    change.DeletePreviews.Add(id);
                    items.Add(new PlanItem { Group = MediaGroup, Field = "Preview", Before = preview.ImageUrl ?? id.ToString(CultureInfo.InvariantCulture), Action = ChangeAction.Delete });
                    calls.Add(new PlanCall { Method = "DELETE", Path = $"{basePath}/previews/{id}/", Target = id.ToString(CultureInfo.InvariantCulture) });
                }

                foreach (var file in input.AddPreviews)
                {
                    CheckImage(file, issues, square: false);

                    if (file.Width is int width && file.Height is int height && (width != PreviewWidth || height != PreviewHeight))
                        issues.Add(new FirefoxIssue(MessageKey.FirefoxPreviewSize, false, file.File.FileName, width, height, PreviewWidth, PreviewHeight));

                    change.AddPreviews.Add(file.File);
                    items.Add(new PlanItem { Group = MediaGroup, Field = "Preview", After = file.File.FileName, Action = ChangeAction.Create });
                    calls.Add(new PlanCall { Method = "POST", Path = $"{basePath}/previews/", Target = file.File.FileName });
                }

                if (addon.Previews.Count - change.DeletePreviews.Count + change.AddPreviews.Count > MaxPreviews)
                    issues.Add(new FirefoxIssue(MessageKey.FirefoxTooManyPreviews, false, MaxPreviews));

                baseKeys.Add(FirefoxContent.PreviewsKey);
                title = $"Change the previews of {name}";
                break;
            }
            case FirefoxChangeKind.Icon:
            {
                if (input.Icon == null)
                {
                    issues.Add(new FirefoxIssue(MessageKey.FirefoxNoChanges, true));
                }
                else
                {
                    CheckImage(input.Icon, issues, square: true);

                    if (input.Icon.Width is int width && input.Icon.Height is int height && width == height && width != IconSize)
                        issues.Add(new FirefoxIssue(MessageKey.FirefoxIconSize, false, width, height, IconSize));

                    change.Icon = input.Icon.File;
                    items.Add(new PlanItem { Group = MediaGroup, Field = "Icon", Before = addon.IconUrl, After = input.Icon.File.FileName, Action = ChangeAction.Update });
                    calls.Add(new PlanCall { Method = "PATCH", Path = $"{basePath}/", Target = $"icon {input.Icon.File.FileName}" });
                }

                baseKeys.Add(FirefoxContent.IconKey);
                title = $"Replace the icon of {name}";
                break;
            }
            case FirefoxChangeKind.ReleaseNotes:
            {
                var target = FindVersion(input.State, input.Version);

                if (target == null)
                {
                    issues.Add(new FirefoxIssue(MessageKey.FirefoxVersionNotFound, true, input.Version ?? "-"));
                    title = $"Release notes of {name}";
                    break;
                }

                version = target.Version;
                change.VersionId = target.Id;
                PlanReleaseNotes(input.ReleaseNotes, target.ReleaseNotes, ReleaseNotesGroup, change, items, issues);

                if (change.Texts.Count > 0)
                    calls.Add(new PlanCall { Method = "PATCH", Path = $"{basePath}/versions/{target.Id}/", Target = "release_notes" });

                baseKeys.Add(FirefoxContent.NotesPrefix + target.Id.ToString(CultureInfo.InvariantCulture));
                title = $"Release notes of {name} {version}";
                break;
            }
            default:
                title = name;
                break;
        }

        if (items.Count == 0 && !issues.Any(i => i.Blocking))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxNoChanges, true));

        if (issues.Any(i => i.Blocking))
            return new FirefoxPlanResult(null, issues);

        var draft = new PlanDraft
        {
            Kind = ChangePlanKind.FirefoxChange,
            Title = $"Firefox Add-ons: {title}",
            Items = items,
            CustomChanges = change,
            VersionId = version == null ? null : change.VersionId?.ToString(CultureInfo.InvariantCulture) ?? version,
            VersionString = version,
            BaseKeys = [.. baseKeys.Distinct(StringComparer.Ordinal)],
            RequiredPermission = input.Kind == FirefoxChangeKind.Version ? OrgPermission.SubmitForReview : null,
            ConfirmText = version == null ? addon.Guid ?? addonId : $"{addon.Guid ?? addonId} {version}",
            Calls = calls,
        };

        return new FirefoxPlanResult(draft, issues);
    }

    /// <summary>
    /// The version a reference names: a numeric id or a version string.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="reference"></param>
    /// <returns></returns>
    public static AmoVersionRead FindVersion(FirefoxAddonState state, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return state.Latest;

        var trimmed = reference.Trim();

        return state.Versions.FirstOrDefault(v => string.Equals(v.Version, trimmed, StringComparison.Ordinal))
               ?? (long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? state.Versions.FirstOrDefault(v => v.Id == id) : null);
    }

    private static void PlanListingValue(AmoAddonRead addon, FirefoxListingValue value, FirefoxPlannedChange change, List<PlanItem> items, List<FirefoxIssue> issues, List<string> baseKeys)
    {
        var row = FirefoxFields.Of(value.Field);
        var locale = value.Locale?.Trim();

        if (row == null)
        {
            issues.Add(new FirefoxIssue(MessageKey.FirefoxFieldUnknown, true, value.Field ?? "-"));
            return;
        }

        var (field, _, maxLength) = row.Value;

        if (string.IsNullOrEmpty(locale) || locale.Length > 20 || !locale.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            issues.Add(new FirefoxIssue(MessageKey.FirefoxLocaleInvalid, true, locale ?? "-"));
            return;
        }

        var next = string.IsNullOrWhiteSpace(value.Value) ? null : value.Value.Trim();
        var current = FirefoxContent.ValuesOf(addon, field).GetValueOrDefault(locale);
        current = string.IsNullOrEmpty(current) ? null : current;

        if (next == current)
            return;

        if (next == null && (field is FirefoxFields.Name or FirefoxFields.Summary) && string.Equals(locale, addon.DefaultLocale, StringComparison.Ordinal))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxDefaultLocaleRequired, true, field, locale));

        if (next != null && next.Length > maxLength)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxTextTooLong, true, $"{field} ({locale})", next.Length, maxLength));

        if (next != null && FirefoxFields.IsUrl(field) && !(Uri.TryCreate(next, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxUrlInvalid, true, field, next));

        if (next != null && field == FirefoxFields.SupportEmail && !MailAddress.TryCreate(next, out _))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxEmailInvalid, true, next));

        change.Texts.Add(new FirefoxPlannedText { Field = field, Locale = locale, Value = next });
        baseKeys.Add(ContentKey.Of(ContentGroups.Listing, locale, field));
        items.Add(new PlanItem { Group = ListingGroup, Locale = locale, Field = field, Before = current, After = next, Action = current == null ? ChangeAction.Create : next == null ? ChangeAction.Delete : ChangeAction.Update });
    }

    private static void PlanVersion(FirefoxPlanInput input, AmoAddonRead addon, FirefoxPlannedChange change, List<PlanItem> items, List<PlanCall> calls, List<FirefoxIssue> issues, List<string> baseKeys, string basePath)
    {
        var package = input.Package;

        if (input.Channel is not (FirefoxVersionChannel.Listed or FirefoxVersionChannel.Unlisted))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxChannelInvalid, true));

        if (package?.Package == null || package.Package.Kind != PackageKind.Xpi)
        {
            issues.Add(new FirefoxIssue(MessageKey.FirefoxPackageNotXpi, true, package?.SessionId.ToString(CultureInfo.InvariantCulture) ?? "-"));
            return;
        }

        var info = package.Package;
        var version = info.VersionName;
        var latest = input.State.Latest;

        // The intake checks, then the checks that depend on the live add-on.
        issues.AddRange(FirefoxManifest.IssuesOf(info).Where(i => i.Key is not (MessageKey.FirefoxVersionNotHigher or MessageKey.FirefoxGeckoIdMissing)));

        if (string.IsNullOrEmpty(info.Identifier))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxGeckoIdMissing, true));
        else if (addon.Guid != null && !string.Equals(info.Identifier, addon.Guid, StringComparison.Ordinal))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxGeckoIdMismatch, true, info.Identifier, addon.Guid));

        if (latest != null && FirefoxVersions.IsValid(version) && FirefoxVersions.Compare(version, latest.Version) <= 0)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxVersionNotHigher, true, version, latest.Version, $"{latest.Channel.ToString().ToLowerInvariant()} {latest.FileStatus}"));

        if (input.State.Versions.Any(v => string.Equals(v.Version, version, StringComparison.Ordinal)))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxVersionExists, true, version));

        if (input.Channel == FirefoxVersionChannel.Listed)
        {
            var waiting = input.State.Versions.FirstOrDefault(v => v.Channel == FirefoxVersionChannel.Listed && v.FileStatus == FirefoxFileStatus.Unreviewed && !v.IsDisabled);

            if (waiting != null)
                issues.Add(new FirefoxIssue(MessageKey.FirefoxListedAwaitingReview, false, waiting.Version));

            if (input.State.LatestOf(FirefoxVersionChannel.Listed) == null && string.IsNullOrWhiteSpace(input.License))
                issues.Add(new FirefoxIssue(MessageKey.FirefoxLicenseRequired, true));
        }

        var needsSource = FirefoxManifest.IssuesOf(info).Any(i => i.Key == MessageKey.FirefoxSourceRequired);

        if (needsSource && input.Source == null)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxSourceMissing, false));

        if (string.IsNullOrWhiteSpace(input.ApprovalNotes) && input.Source != null && input.Channel == FirefoxVersionChannel.Listed)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxBuildInstructions, false));

        if (input.ApprovalNotes?.Length > FirefoxFields.ApprovalNotesMaxLength)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxTextTooLong, true, "approval notes", input.ApprovalNotes.Length, FirefoxFields.ApprovalNotesMaxLength));

        foreach (var application in input.Compatibility.Where(a => !Applications.Contains(a)))
            issues.Add(new FirefoxIssue(MessageKey.FirefoxApplicationInvalid, true, application));

        PlanReleaseNotes(input.ReleaseNotes, null, VersionGroup, change, items, issues);

        change.SessionId = package.SessionId;
        change.Package = package.File;
        change.PackageVersion = version;
        change.Channel = input.Channel;
        change.ApprovalNotes = string.IsNullOrWhiteSpace(input.ApprovalNotes) ? null : input.ApprovalNotes.Trim();
        change.License = string.IsNullOrWhiteSpace(input.License) ? null : input.License.Trim();
        change.Compatibility = [.. input.Compatibility.Distinct(StringComparer.Ordinal)];
        change.Source = input.Source?.File;

        baseKeys.Add(FirefoxContent.LatestVersionKey);

        items.Insert(0, new PlanItem { Group = VersionGroup, Field = "Package", Before = latest?.Version, After = $"{package.File.FileName} ({version})", Action = ChangeAction.Create });
        items.Insert(1, new PlanItem { Group = VersionGroup, Field = "Channel", After = input.Channel.ToString().ToLowerInvariant(), Action = ChangeAction.Create });

        if (change.License != null)
            items.Add(new PlanItem { Group = VersionGroup, Field = "License", After = change.License, Action = ChangeAction.Create });

        if (change.ApprovalNotes != null)
            items.Add(new PlanItem { Group = VersionGroup, Field = "ApprovalNotes", After = change.ApprovalNotes, Action = ChangeAction.Create });

        if (change.Source != null)
            items.Add(new PlanItem { Group = VersionGroup, Field = "Source", After = change.Source.FileName, Action = ChangeAction.Create });

        calls.Add(new PlanCall { Method = "POST", Path = "/addons/upload/", Target = $"{package.File.FileName} ({input.Channel.ToString().ToLowerInvariant()})" });
        calls.Add(new PlanCall { Method = "GET", Path = "/addons/upload/{uuid}/", Target = "validation" });
        calls.Add(new PlanCall { Method = "POST", Path = $"{basePath}/versions/", Target = version });
    }

    private static void PlanReleaseNotes(List<FirefoxLocalizedText> notes, Dictionary<string, string> current, string group, FirefoxPlannedChange change, List<PlanItem> items, List<FirefoxIssue> issues)
    {
        foreach (var note in notes ?? [])
        {
            var locale = note.Locale?.Trim();

            if (string.IsNullOrEmpty(locale) || locale.Length > 20 || !locale.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            {
                issues.Add(new FirefoxIssue(MessageKey.FirefoxLocaleInvalid, true, locale ?? "-"));
                continue;
            }

            var next = string.IsNullOrWhiteSpace(note.Value) ? null : note.Value.Trim();
            var before = current?.GetValueOrDefault(locale);
            before = string.IsNullOrEmpty(before) ? null : before;

            if (next == before || (current == null && next == null))
                continue;

            if (next?.Length > FirefoxFields.ReleaseNotesMaxLength)
                issues.Add(new FirefoxIssue(MessageKey.FirefoxTextTooLong, true, $"release notes ({locale})", next.Length, FirefoxFields.ReleaseNotesMaxLength));

            change.Texts.Add(new FirefoxPlannedText { Field = FirefoxFields.ReleaseNotes, Locale = locale, Value = next });
            items.Add(new PlanItem { Group = group, Locale = locale, Field = FirefoxFields.ReleaseNotes, Before = before, After = next, Action = before == null ? ChangeAction.Create : next == null ? ChangeAction.Delete : ChangeAction.Update });
        }
    }

    private static void CheckImage(FirefoxPlanFile file, List<FirefoxIssue> issues, bool square)
    {
        if (file.Format is not ("png" or "jpeg"))
        {
            issues.Add(new FirefoxIssue(MessageKey.FirefoxImageFormat, true, file.File.FileName));
            return;
        }

        if (square && file.Width is int width && file.Height is int height && width != height)
            issues.Add(new FirefoxIssue(MessageKey.FirefoxIconNotSquare, true, width, height));
    }
}
