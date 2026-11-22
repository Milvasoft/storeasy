using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Chrome;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;

namespace Storeasy.Infrastructure.Stores.Chrome;

/// <summary>
/// Readiness of a Chrome Web Store item: the policy standing (warned, taken down), the submission's review state, and an
/// extension package newer than the published version that passed the manifest checks. The listing, the privacy practices
/// and the distribution settings have no API; the rules catalog's assisted steps cover them.
/// </summary>
/// <param name="sessionRepository"></param>
/// <param name="localizer"></param>
public sealed class ChromeReadiness(IStoreasyRepositoryBase<ArtifactSession> sessionRepository, IMilvaLocalizer localizer) : IReadinessContributor
{
    private const string _group = "Chrome";
    private const string _fixRoute = "chrome";

    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReadinessItemDto>> EvaluateAsync(StoreAppContext app, StoreContentSnapshot content, CancellationToken cancellationToken)
    {
        if (app.Store != StoreKind.Chrome || content == null)
            return [];

        var status = ChromeContent.FromSnapshot(content);
        var items = new List<ReadinessItemDto>();

        items.Add(status.TakenDown
            ? Item("chrome.policy", ReadinessStatus.Missing, MessageKey.ReadinessChromePolicy, _localizer[MessageKey.ReadinessChromeTakenDown], "chrome.policy.enforcement")
            : status.Warned
                ? Item("chrome.policy", ReadinessStatus.Missing, MessageKey.ReadinessChromePolicy, _localizer[MessageKey.ReadinessChromeWarned], "chrome.policy.enforcement")
                : Item("chrome.policy", ReadinessStatus.Ok, MessageKey.ReadinessChromePolicy, null));

        var submitted = status.Submitted;

        switch (submitted?.State)
        {
            case ChromeItemState.PendingReview:
                items.Add(Item("chrome.submission", ReadinessStatus.Warning, MessageKey.ReadinessChromeSubmission, _localizer[MessageKey.ReadinessChromeInReview, submitted.Version ?? "-"], "chrome.review.process"));
                break;
            case ChromeItemState.Rejected:
                items.Add(Item("chrome.submission", ReadinessStatus.Missing, MessageKey.ReadinessChromeSubmission, _localizer[MessageKey.ReadinessChromeRejected, submitted.Version ?? "-"], "chrome.review.process"));
                break;
            case ChromeItemState.Staged:
                items.Add(Item("chrome.submission", ReadinessStatus.Warning, MessageKey.ReadinessChromeSubmission, _localizer[MessageKey.ReadinessChromeStaged, submitted.Version ?? "-"]));
                break;
        }

        var published = status.Published?.Version;
        var sessions = await _sessionRepository.GetAllAsync(s => s.OrganizationId == app.OrganizationId
                                                                 && s.AppId == app.AppId
                                                                 && s.Platform == ArtifactPlatform.Extension
                                                                 && !s.PackageDeleted
                                                                 && s.Package != null,
                                                            projection: s => new ArtifactSession { Id = s.Id, Package = s.Package, StartDate = s.StartDate },
                                                            cancellationToken: cancellationToken);

        var candidate = sessions.Where(s => s.Package?.VersionName != null && (published == null || ChromeVersions.Compare(s.Package.VersionName, published) > 0))
                                .OrderByDescending(s => s.StartDate)
                                .FirstOrDefault();

        if (candidate == null)
        {
            items.Add(Item("chrome.package", ReadinessStatus.Warning, MessageKey.ReadinessChromePackage, _localizer[MessageKey.ReadinessChromeNoPackage, published ?? "-"]));
        }
        else
        {
            var blocking = ChromeManifest.IssuesOf(candidate.Package).Where(i => i.Blocking).ToList();

            items.Add(blocking.Count > 0
                ? Item("chrome.package", ReadinessStatus.Missing, MessageKey.ReadinessChromePackage, string.Join(" ", blocking.Select(b => b.Text)), "chrome.packages.manifest-v3")
                : Item("chrome.package", ReadinessStatus.Ok, MessageKey.ReadinessChromePackage, _localizer[MessageKey.ReadinessChromePackageReady, candidate.Package.VersionName]));
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
