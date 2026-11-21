using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Winget;
using System.Globalization;

namespace Storeasy.Infrastructure.Stores.Winget;

/// <summary>
/// <see cref="StoreJobKind.WingetSync"/>: reads the winget packages of a connection from winget-pkgs (versions, the latest
/// version's names, locales and schema version; a version folder becomes a build), finds the fork owner's pull requests
/// for each package, and refreshes the review state of the open ones from their labels, check runs and bot comments. A
/// state change is audited and notified like a store version state change; a merge is the publication. The schedule runs
/// it every 15 minutes for connections with an open pull request, refreshing only those.
/// </summary>
/// <param name="client"></param>
/// <param name="storeAppRepository"></param>
/// <param name="packageRepository"></param>
/// <param name="pullRequestRepository"></param>
/// <param name="buildRepository"></param>
/// <param name="auditTrail"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="timeProvider"></param>
public sealed class WingetSyncJobHandler(IWingetClient client,
                                         IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                         IStoreasyRepositoryBase<WingetPackage> packageRepository,
                                         IStoreasyRepositoryBase<WingetPullRequest> pullRequestRepository,
                                         IStoreasyRepositoryBase<Build> buildRepository,
                                         IAuditTrail auditTrail,
                                         INotificationDispatcher notificationDispatcher,
                                         TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IWingetClient _client = client;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<WingetPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<WingetPullRequest> _pullRequestRepository = pullRequestRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.WingetSync;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<WingetSyncPayload>() ?? throw new StoreJobException("The job has no connection.", transient: false);
        var connection = await context.OpenConnectionAsync(payload.ConnectionId, cancellationToken) ?? throw new StoreJobException("The store connection was removed.", transient: false);

        if (connection.Store != StoreKind.Winget)
            throw new StoreJobException($"{connection.Store} is not a winget connection.", transient: false);

        var records = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == context.OrganizationId
                                                                 && s.Store == StoreKind.Winget
                                                                 && s.StoreConnectionId == connection.Id
                                                                 && !s.App.IsDeleted
                                                                 && (payload.StoreAppId == null || s.Id == payload.StoreAppId),
                                                            projection: s => new StoreApp { Id = s.Id, OrganizationId = s.OrganizationId, AppId = s.AppId, ExternalId = s.ExternalId, Name = s.Name, App = new App { Name = s.App.Name } },
                                                            cancellationToken: cancellationToken);

        var recordIds = records.Select(r => r.Id).ToList();
        var packages = (await _packageRepository.GetAllAsync(p => recordIds.Contains(p.StoreAppId), cancellationToken: cancellationToken)).ToDictionary(p => p.StoreAppId);
        var pulls = await _pullRequestRepository.GetAllAsync(p => recordIds.Contains(p.StoreAppId) && p.OrganizationId == context.OrganizationId, cancellationToken: cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var read = 0;
        var failed = new List<string>();

        if (!payload.OpenOnly)
        {
            var step = await context.BeginStepAsync("winget.packages.read", StepDirection.Inbound, connection.Name, records.Count, cancellationToken);

            // A record linked without the add command (apps_link_store_record) gets its row here.
            var missing = records.Where(r => !packages.ContainsKey(r.Id))
                                 .Select(r => new WingetPackage { OrganizationId = context.OrganizationId, StoreAppId = r.Id, PackageIdentifier = r.ExternalId })
                                 .ToList();

            if (missing.Count > 0)
            {
                await _packageRepository.AddRangeAsync(missing, cancellationToken);

                foreach (var package in missing)
                    packages[package.StoreAppId] = package;
            }

            var discovered = new List<WingetPullRequest>();

            foreach (var record in records)
            {
                if (await ReadPackageAsync(connection.SecretJson, record, packages[record.Id], now, cancellationToken))
                    read++;
                else
                    failed.Add(record.ExternalId);

                discovered.AddRange(await DiscoverAsync(connection.SecretJson, context.OrganizationId, record, pulls, now, cancellationToken));
                await context.ReportProgressAsync(step, read + failed.Count, cancellationToken);
            }

            if (discovered.Count > 0)
            {
                await _pullRequestRepository.AddRangeAsync(discovered, cancellationToken);
                pulls.AddRange(discovered);
            }

            await context.EndStepAsync(step, failed.Count == 0 ? StoreJobStepStatus.Succeeded : StoreJobStepStatus.Failed, failed.Count == 0 ? $"{read} packages." : $"{read} packages; not read: {string.Join(", ", failed)}.", cancellationToken);
        }

        var open = pulls.Where(p => p.IsOpen).ToList();
        var pullStep = await context.BeginStepAsync("winget.pulls.read", StepDirection.Inbound, total: open.Count, cancellationToken: cancellationToken);
        var changed = 0;
        var done = 0;
        var refreshed = new List<WingetPullRequest>();

        foreach (var pull in open)
        {
            var record = records.FirstOrDefault(r => r.Id == pull.StoreAppId);

            if (record == null)
                continue;

            var github = await _client.GetPullRequestAsync(connection.SecretJson, pull.Number, cancellationToken);
            var previous = pull.State;

            Merge(pull, github, now);
            refreshed.Add(pull);

            if (pull.State != previous)
            {
                changed++;
                await ReportStateAsync(context.OrganizationId, record, pull, previous, cancellationToken);

                // A merge adds the version folder: read the package again.
                if (pull.State == WingetPullRequestState.Merged && packages.TryGetValue(record.Id, out var package))
                    await ReadPackageAsync(connection.SecretJson, record, package, now, cancellationToken);
            }

            await context.ReportProgressAsync(pullStep, ++done, cancellationToken);
        }

        if (refreshed.Count > 0)
            await _pullRequestRepository.UpdateAsync(refreshed, cancellationToken);

        await context.EndStepAsync(pullStep, StoreJobStepStatus.Succeeded, $"{open.Count} open pull requests, {changed} changed.", cancellationToken);

        return new StoreJobOutcome($"winget: {read} packages read, {open.Count} open pull requests, {changed} state changes.",
                                   new { Packages = read, Failed = failed, OpenPullRequests = open.Count, Changed = changed });
    }

    /// <summary>
    /// Copies what GitHub reports into the row and derives the review state.
    /// </summary>
    /// <param name="pull"></param>
    /// <param name="github"></param>
    /// <param name="now"></param>
    public static void Merge(WingetPullRequest pull, WingetPullRequestRead github, DateTime now)
    {
        var state = WingetReviewStates.Of(github.Open, github.Merged, github.Draft, github.Labels, github.Checks);

        pull.Url = github.Url ?? pull.Url;
        pull.Title = github.Title?.Length > WingetPullRequest.TitleMaxLength ? github.Title[..WingetPullRequest.TitleMaxLength] : github.Title ?? pull.Title;
        pull.HeadBranch = github.HeadBranch ?? pull.HeadBranch;
        pull.HeadSha = github.HeadSha ?? pull.HeadSha;
        pull.Labels = github.Labels;
        pull.Checks = github.Checks.Count > 0 || github.HeadSha != null ? github.Checks : pull.Checks;
        pull.Comments = github.Comments.Count > 0 ? github.Comments : pull.Comments;
        pull.IsOpen = github.Open;
        pull.OpenedDate = github.CreatedDate ?? pull.OpenedDate;
        pull.MergedDate = github.MergedDate;
        pull.ClosedDate = github.ClosedDate;
        pull.SyncedDate = now;
        pull.PackageVersion ??= VersionFromTitle(github.Title);

        if (pull.State != state || pull.StatusHistory.Count == 0)
            pull.StatusHistory = [.. pull.StatusHistory ?? [], new WingetStateChange { State = state, Date = now }];

        pull.State = state;
    }

    /// <summary>
    /// The version of a winget-pkgs pull request title ("New version: Id version 1.2.3", "Id 1.2.3"): the last word that
    /// starts with a digit.
    /// </summary>
    /// <param name="title"></param>
    /// <returns></returns>
    public static string VersionFromTitle(string title)
    {
        var word = (title ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(w => w.Trim('(', ')', ','))
                                          .LastOrDefault(w => w.Length > 0 && char.IsAsciiDigit(w[0]));

        return word?.Length <= WingetPackage.VersionMaxLength ? word.TrimEnd('.') : null;
    }

    private async Task<bool> ReadPackageAsync(string secretJson, StoreApp record, WingetPackage package, DateTime now, CancellationToken cancellationToken)
    {
        try
        {
            var versions = await _client.ListVersionsAsync(secretJson, record.ExternalId, cancellationToken);
            var set = versions.Count == 0 ? null : WingetManifestSet.FromFiles(await _client.ReadManifestFilesAsync(secretJson, record.ExternalId, versions[0], cancellationToken));

            package.IsPublished = versions.Count > 0;
            package.Versions = [.. versions];
            package.LatestVersion = versions.FirstOrDefault();
            package.Publisher = Trim(set?.DefaultLocale.Text("Publisher"), WingetPackage.NameMaxLength);
            package.PackageName = Trim(set?.DefaultLocale.Text("PackageName"), WingetPackage.NameMaxLength);
            package.DefaultLocale = Trim(set?.DefaultLocaleTag, WingetPackage.LocaleMaxLength);
            package.Locales = set == null ? [] : [.. set.LocaleTags];
            package.ManifestVersion = Trim(set?.ManifestVersion, 20);
            package.SyncedDate = now;
            package.SyncError = null;

            await _packageRepository.UpdateAsync(package, cancellationToken);
            await SaveBuildsAsync(record, versions, now, cancellationToken);

            var name = package.PackageName;

            if (!string.IsNullOrWhiteSpace(name) && record.Name != name)
            {
                record.Name = Trim(name, App.NameMaxLength);
                record.SyncedDate = now;
                await _storeAppRepository.UpdateAsync(record, cancellationToken, s => s.Name, s => s.SyncedDate);
            }

            return true;
        }
        catch (StoreCallException ex) when (!ex.Transient)
        {
            package.SyncError = SecretRedactor.RedactAndTrim(ex.Message, WingetPackage.SyncErrorMaxLength);
            await _packageRepository.UpdateAsync(package, cancellationToken, p => p.SyncError);

            return false;
        }
    }

    private async Task SaveBuildsAsync(StoreApp record, IReadOnlyList<string> versions, DateTime now, CancellationToken cancellationToken)
    {
        var known = (await _buildRepository.GetAllAsync(b => b.StoreAppId == record.Id, projection: b => new Build { ExternalId = b.ExternalId }, cancellationToken: cancellationToken)).Select(b => b.ExternalId).ToHashSet(StringComparer.Ordinal);
        var added = versions.Where(v => !known.Contains(v) && v.Length <= Build.TextMaxLength).Select(v => new Build
        {
            OrganizationId = record.OrganizationId,
            StoreAppId = record.Id,
            Store = StoreKind.Winget,
            ExternalId = v,
            Version = v,
            BuildNumber = v,
            ProcessingState = BuildProcessingState.Valid,
            SyncedDate = now,
        }).ToList();

        if (added.Count > 0)
            await _buildRepository.AddRangeAsync(added, cancellationToken);
    }

    private async Task<List<WingetPullRequest>> DiscoverAsync(string secretJson, int organizationId, StoreApp record, List<WingetPullRequest> pulls, DateTime now, CancellationToken cancellationToken)
    {
        IReadOnlyList<WingetPullRequestRead> found;

        try
        {
            found = await _client.FindPullRequestsAsync(secretJson, record.ExternalId, cancellationToken);
        }
        catch (StoreCallException ex) when (!ex.Transient)
        {
            // Search is a convenience: storeasy's own pull requests are known without it.
            return [];
        }

        return [.. found.Where(f => f.Number > 0 && pulls.All(p => p.StoreAppId != record.Id || p.Number != f.Number)).Select(item =>
        {
            var state = WingetReviewStates.Of(item.Open, item.Merged, item.Draft, item.Labels, []);

            return new WingetPullRequest
            {
                OrganizationId = organizationId,
                StoreAppId = record.Id,
                Number = item.Number,
                Url = item.Url,
                Title = Trim(item.Title, WingetPullRequest.TitleMaxLength),
                PackageVersion = VersionFromTitle(item.Title),
                State = state,
                IsOpen = item.Open,
                Labels = item.Labels,
                OpenedDate = item.CreatedDate,
                MergedDate = item.MergedDate,
                ClosedDate = item.ClosedDate,
                SyncedDate = now,
                StatusHistory = [new WingetStateChange { State = state, Date = now }],
            };
        })];
    }

    private async Task ReportStateAsync(int organizationId, StoreApp record, WingetPullRequest pull, WingetPullRequestState previous, CancellationToken cancellationToken)
    {
        var action = pull.State switch
        {
            WingetPullRequestState.Merged => AuditAction.WingetPublished,
            WingetPullRequestState.Closed => AuditAction.WingetPullRequestClosed,
            _ => AuditAction.WingetReviewStateChanged,
        };

        var number = pull.Number.ToString(CultureInfo.InvariantCulture);

        _auditTrail.Channel = ActionChannel.System;

        await _auditTrail.WriteAsync(organizationId, action, record.Id.ToString(CultureInfo.InvariantCulture), $"{record.ExternalId} {pull.PackageVersion} (#{number}): {previous} -> {pull.State}.", StoreKind.Winget,
                                     new { pull.Number, pull.PackageVersion, Previous = previous, pull.State, pull.Labels }, cancellationToken);

        await _notificationDispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = organizationId,
            Kind = NotificationType.VersionState,
            Roles = NotificationAudiences.Editors,
            TextKey = MessageKey.NotificationWingetReviewState,
            TextArgs = [record.App?.Name ?? record.Name, pull.PackageVersion ?? "-", number, previous.ToString(), pull.State.ToString()],
            Data = new { appId = record.AppId, storeAppId = record.Id, store = StoreKind.Winget.ToString(), pullRequest = pull.Number, versionString = pull.PackageVersion, oldValue = previous.ToString(), newValue = pull.State.ToString(), url = pull.Url },
            AppId = record.AppId,
            RelatedEntity = NotificationEntity.AppVersionState,
            RelatedEntityId = number,
            ActionLink = $"/apps/{record.AppId}/winget",
            WebhookEventType = OrgWebhookEventTypes.VersionStateChanged,
            ActivityType = ActivityEventTypes.VersionStateChanged,
        }, cancellationToken);
    }

    private static string Trim(string value, int max) => value?.Length > max ? value[..max] : value;
}
