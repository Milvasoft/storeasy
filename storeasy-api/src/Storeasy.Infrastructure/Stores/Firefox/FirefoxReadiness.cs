using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;

namespace Storeasy.Infrastructure.Stores.Firefox;

/// <summary>
/// Readiness of a Firefox add-on: its status on AMO (disabled by Mozilla, disabled by the developer, awaiting review), the
/// review state of the newest version, the listing texts AMO needs in the default locale, previews, and an .xpi package newer
/// than every version that passed the manifest checks. Policies with no API (data collection consent text, source code
/// review) are the rules catalog's assisted steps.
/// </summary>
/// <param name="sessionRepository"></param>
/// <param name="localizer"></param>
public sealed class FirefoxReadiness(IStoreasyRepositoryBase<ArtifactSession> sessionRepository, IMilvaLocalizer localizer) : IReadinessContributor
{
    private const string _group = "Firefox";
    private const string _fixRoute = "firefox";

    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReadinessItemDto>> EvaluateAsync(StoreAppContext app, StoreContentSnapshot content, CancellationToken cancellationToken)
    {
        if (app.Store != StoreKind.Firefox || FirefoxContent.FromSnapshot(content) is not { } state)
            return [];

        var addon = state.Addon;
        var items = new List<ReadinessItemDto>
        {
            addon.Status switch
            {
                FirefoxAddonStatus.Disabled or FirefoxAddonStatus.Rejected or FirefoxAddonStatus.Deleted => Item("firefox.status", ReadinessStatus.Missing, MessageKey.ReadinessFirefoxStatus, _localizer[MessageKey.ReadinessFirefoxDisabled, addon.Status], "firefox.policy.enforcement"),
                _ when addon.IsDisabled => Item("firefox.status", ReadinessStatus.Warning, MessageKey.ReadinessFirefoxStatus, _localizer[MessageKey.ReadinessFirefoxDisabled, "developer"]),
                FirefoxAddonStatus.Nominated => Item("firefox.status", ReadinessStatus.Warning, MessageKey.ReadinessFirefoxStatus, _localizer[MessageKey.ReadinessFirefoxInReview, addon.CurrentVersion?.Version ?? "-"], "firefox.review.process"),
                _ => Item("firefox.status", ReadinessStatus.Ok, MessageKey.ReadinessFirefoxStatus, _localizer[MessageKey.ReadinessFirefoxStatusOk, addon.Status]),
            },
        };

        var latest = state.Latest;

        items.Add(latest?.FileStatus switch
        {
            null => Item("firefox.version", ReadinessStatus.Warning, MessageKey.ReadinessFirefoxVersion, _localizer[MessageKey.ReadinessFirefoxNoVersion]),
            FirefoxFileStatus.Unreviewed => Item("firefox.version", ReadinessStatus.Warning, MessageKey.ReadinessFirefoxVersion, _localizer[MessageKey.ReadinessFirefoxInReview, latest.Version], "firefox.review.process"),
            FirefoxFileStatus.Disabled => Item("firefox.version", ReadinessStatus.Missing, MessageKey.ReadinessFirefoxVersion, _localizer[MessageKey.ReadinessFirefoxRejected, latest.Version], "firefox.review.process"),
            _ => Item("firefox.version", ReadinessStatus.Ok, MessageKey.ReadinessFirefoxVersion, _localizer[MessageKey.ReadinessFirefoxApproved, latest.Version, latest.Channel.ToString().ToLowerInvariant()]),
        });

        // A listed add-on needs a name and a summary in its default locale; a description and previews are expected.
        if (state.LatestOf(FirefoxVersionChannel.Listed) != null || addon.Status == FirefoxAddonStatus.Public)
        {
            var locale = addon.DefaultLocale ?? "en-US";
            var missing = new[] { (FirefoxFields.Name, addon.Name), (FirefoxFields.Summary, addon.Summary), (FirefoxFields.Description, addon.Description) }
                          .Where(f => string.IsNullOrWhiteSpace(f.Item2.GetValueOrDefault(locale)))
                          .Select(f => f.Item1)
                          .ToList();

            items.Add(missing.Count > 0
                ? Item("firefox.listing", missing.Contains(FirefoxFields.Description) && missing.Count == 1 ? ReadinessStatus.Warning : ReadinessStatus.Missing, MessageKey.ReadinessFirefoxListing, _localizer[MessageKey.ReadinessFirefoxListingMissing, string.Join(", ", missing), locale], "firefox.listing-limits.required-texts")
                : Item("firefox.listing", ReadinessStatus.Ok, MessageKey.ReadinessFirefoxListing, _localizer[MessageKey.ReadinessFirefoxListingOk, locale]));

            if (!string.Equals(addon.Type, "statictheme", StringComparison.Ordinal))
            {
                items.Add(addon.Previews.Count == 0
                    ? Item("firefox.previews", ReadinessStatus.Warning, MessageKey.ReadinessFirefoxPreviews, _localizer[MessageKey.ReadinessFirefoxNoPreviews], "firefox.screenshots.previews")
                    : Item("firefox.previews", ReadinessStatus.Ok, MessageKey.ReadinessFirefoxPreviews, _localizer[MessageKey.ReadinessFirefoxPreviewsOk, addon.Previews.Count]));
            }
        }

        var sessions = await _sessionRepository.GetAllAsync(s => s.OrganizationId == app.OrganizationId
                                                                 && s.AppId == app.AppId
                                                                 && s.Platform == ArtifactPlatform.Extension
                                                                 && !s.PackageDeleted
                                                                 && s.Package != null,
                                                            projection: s => new ArtifactSession { Id = s.Id, Package = s.Package, StartDate = s.StartDate },
                                                            cancellationToken: cancellationToken);

        var candidate = sessions.Where(s => s.Package?.Kind == PackageKind.Xpi && s.Package.VersionName != null && (latest == null || FirefoxVersions.Compare(s.Package.VersionName, latest.Version) > 0))
                                .OrderByDescending(s => s.StartDate)
                                .FirstOrDefault();

        if (candidate == null)
        {
            items.Add(Item("firefox.package", ReadinessStatus.Warning, MessageKey.ReadinessFirefoxPackage, _localizer[MessageKey.ReadinessFirefoxNoPackage, latest?.Version ?? "-"]));
        }
        else
        {
            var blocking = FirefoxManifest.IssuesOf(candidate.Package).Where(i => i.Blocking).ToList();

            items.Add(blocking.Count > 0
                ? Item("firefox.package", ReadinessStatus.Missing, MessageKey.ReadinessFirefoxPackage, string.Join(" ", blocking.Select(b => b.English)), "firefox.packages.manifest")
                : Item("firefox.package", ReadinessStatus.Ok, MessageKey.ReadinessFirefoxPackage, _localizer[MessageKey.ReadinessFirefoxPackageReady, candidate.Package.VersionName]));
        }

        return items;
    }

    private ReadinessItemDto Item(string key, ReadinessStatus status, string titleKey, string message, string ruleId = null) => new()
    {
        Key = key,
        Group = _group,
        Status = status,
        RuleId = ruleId,
        Title = _localizer[titleKey],
        Message = message,
        FixRoute = _fixRoute,
    };
}
