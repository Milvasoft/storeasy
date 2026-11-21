using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Winget;

namespace Storeasy.Infrastructure.Stores.Winget;

/// <summary>
/// Readiness of a winget package: the latest manifests pass the schema and policy checks storeasy can run, the package has
/// a version in winget-pkgs, and no pull request waits for the author. The checks the validation pipeline runs on the
/// installer itself (install, scan, uninstall) are not repeated here.
/// </summary>
/// <param name="pullRequestRepository"></param>
/// <param name="localizer"></param>
public sealed class WingetReadiness(IStoreasyRepositoryBase<WingetPullRequest> pullRequestRepository, IMilvaLocalizer localizer) : IReadinessContributor
{
    private const string _group = "Winget";
    private const string _fixRoute = "winget";

    private readonly IStoreasyRepositoryBase<WingetPullRequest> _pullRequestRepository = pullRequestRepository;
    private readonly IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReadinessItemDto>> EvaluateAsync(StoreAppContext app, StoreContentSnapshot content, CancellationToken cancellationToken)
    {
        if (app.Store != StoreKind.Winget || content == null)
            return [];

        var items = new List<ReadinessItemDto>();
        var files = WingetContent.FilesOf(content);

        if (files.Count == 0)
        {
            items.Add(Item("winget.version", ReadinessStatus.Warning, MessageKey.ReadinessWingetVersion, _localizer[MessageKey.ReadinessWingetNoVersion]));
        }
        else
        {
            var issues = WingetManifestValidator.Validate(WingetManifestSet.FromFiles(files));
            var blocking = WingetContent.Localize(issues.Where(i => i.Blocking), _localizer);
            var warnings = WingetContent.Localize(issues.Where(i => !i.Blocking), _localizer);

            items.Add(Item("winget.version", ReadinessStatus.Ok, MessageKey.ReadinessWingetVersion, content.VersionString));
            items.Add(Item("winget.manifests",
                           blocking.Count > 0 ? ReadinessStatus.Missing : warnings.Count > 0 ? ReadinessStatus.Warning : ReadinessStatus.Ok,
                           MessageKey.ReadinessWingetManifests,
                           blocking.Count > 0 ? string.Join(" ", blocking.Select(i => i.Message)) : warnings.Count > 0 ? string.Join(" ", warnings.Select(i => i.Message)) : null,
                           "winget.packages.manifest-schema"));
        }

        var open = await _pullRequestRepository.GetAllAsync(p => p.StoreAppId == app.StoreAppId && p.OrganizationId == app.OrganizationId && p.IsOpen,
                                                            projection: p => new WingetPullRequest { Number = p.Number, State = p.State, PackageVersion = p.PackageVersion },
                                                            cancellationToken: cancellationToken);

        var waiting = open.Where(p => WingetReviewStates.NeedsAction(p.State)).OrderBy(p => p.Number).ToList();

        if (waiting.Count > 0)
        {
            items.Add(Item("winget.pull-request", ReadinessStatus.Missing, MessageKey.ReadinessWingetPullRequest,
                           _localizer[MessageKey.ReadinessWingetPullRequestAction, string.Join(", ", waiting.Select(p => $"#{p.Number} {p.PackageVersion} ({_localizer[$"WingetPullRequestState.{p.State}"]})"))],
                           "winget.review.author-feedback"));
        }
        else if (open.Count > 0)
        {
            items.Add(Item("winget.pull-request", ReadinessStatus.Warning, MessageKey.ReadinessWingetPullRequest,
                           _localizer[MessageKey.ReadinessWingetPullRequestOpen, string.Join(", ", open.OrderBy(p => p.Number).Select(p => $"#{p.Number} {p.PackageVersion} ({_localizer[$"WingetPullRequestState.{p.State}"]})"))]));
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
