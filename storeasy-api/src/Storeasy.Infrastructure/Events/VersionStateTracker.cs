using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Events;
using Storeasy.Domain.Feedback;
using System.Globalization;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// Default <see cref="IVersionStateTracker"/>. Reads everything it needs in a few queries, decides in memory, writes in
/// bulk, then notifies: a rejection to the owners and admins (in-app and e-mail by default), a state change to the
/// editors (in-app by default; not for versions the first sync only discovers), and the outgoing webhooks.
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="stateRepository"></param>
/// <param name="caseRepository"></param>
/// <param name="stepRepository"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="orgWebhookPublisher"></param>
/// <param name="auditTrail"></param>
/// <param name="timeProvider"></param>
public sealed class VersionStateTracker(IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                        IStoreasyRepositoryBase<AppVersionState> stateRepository,
                                        IStoreasyRepositoryBase<RejectionCase> caseRepository,
                                        IStoreasyRepositoryBase<AssistedStep> stepRepository,
                                        INotificationDispatcher notificationDispatcher,
                                        IOrgWebhookPublisher orgWebhookPublisher,
                                        IAuditTrail auditTrail,
                                        TimeProvider timeProvider) : IVersionStateTracker
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<AppVersionState> _stateRepository = stateRepository;
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = caseRepository;
    private readonly IStoreasyRepositoryBase<AssistedStep> _stepRepository = stepRepository;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly IOrgWebhookPublisher _orgWebhookPublisher = orgWebhookPublisher;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VersionStateOutcome>> ApplyAsync(int organizationId, IReadOnlyList<VersionStateChange> changes, RejectionSource source, CancellationToken cancellationToken)
    {
        var distinct = changes.Where(c => !string.IsNullOrEmpty(c.ExternalVersionId) && !string.IsNullOrEmpty(c.State))
                              .GroupBy(c => (c.StoreAppId, c.ExternalVersionId))
                              .Select(g => g.Last())
                              .ToList();

        if (distinct.Count == 0)
            return [];

        var storeAppIds = distinct.Select(c => c.StoreAppId).Distinct().ToList();

        var storeApps = (await _storeAppRepository.GetAllAsync(s => storeAppIds.Contains(s.Id) && s.OrganizationId == organizationId,
                                                               projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, Store = s.Store, Name = s.App.Name },
                                                               cancellationToken: cancellationToken)).ToDictionary(s => s.Id);

        var states = (await _stateRepository.GetAllAsync(s => storeAppIds.Contains(s.StoreAppId),
                                                         projection: s => new AppVersionState { Id = s.Id, StoreAppId = s.StoreAppId, ExternalVersionId = s.ExternalVersionId, State = s.State, SubmittedDate = s.SubmittedDate, VersionString = s.VersionString, BuildNumber = s.BuildNumber },
                                                         cancellationToken: cancellationToken)).ToDictionary(s => (s.StoreAppId, s.ExternalVersionId));

        var cases = await _caseRepository.GetAllAsync(c => storeAppIds.Contains(c.StoreAppId),
                                                      projection: c => new RejectionCase { Id = c.Id, StoreAppId = c.StoreAppId, ExternalVersionId = c.ExternalVersionId, VersionString = c.VersionString, State = c.State, OpenedDate = c.OpenedDate, Timeline = c.Timeline },
                                                      cancellationToken: cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var added = new List<AppVersionState>();
        var updated = new List<AppVersionState>();
        var opened = new List<RejectionCase>();
        var closed = new List<RejectionCase>();
        var outcomes = new List<(VersionStateOutcome Outcome, StoreApp StoreApp, VersionStateChange Change)>();

        foreach (var change in distinct.Where(c => storeApps.ContainsKey(c.StoreAppId)))
        {
            var storeApp = storeApps[change.StoreAppId];
            var date = change.Date ?? now;
            states.TryGetValue((change.StoreAppId, change.ExternalVersionId), out var row);

            var previous = row?.State;
            var changed = !string.Equals(previous, change.State, StringComparison.OrdinalIgnoreCase);
            var reachedReview = AppStoreVersionStates.IsSubmitted(change.State) || AppStoreVersionStates.RejectionOf(change.State).HasValue;

            if (row == null)
            {
                row = new AppVersionState { OrganizationId = organizationId, AppId = storeApp.AppId, StoreAppId = storeApp.Id, Store = storeApp.Store, ExternalVersionId = change.ExternalVersionId };
                added.Add(row);
                states[(change.StoreAppId, change.ExternalVersionId)] = row;
            }
            else if (changed || row.VersionString != change.VersionString || (change.BuildNumber != null && row.BuildNumber != change.BuildNumber))
            {
                updated.Add(row);
            }

            row.VersionString = Trim(change.VersionString ?? row.VersionString);
            row.BuildNumber = Trim(change.BuildNumber ?? row.BuildNumber);

            if (changed)
            {
                row.PreviousState = Trim(previous);
                row.State = Trim(change.State);
                row.ChangedDate = date;
            }

            if (reachedReview)
                row.SubmittedDate ??= date;

            int? openedId = null;
            var closedIds = new List<int>();
            var rejection = AppStoreVersionStates.RejectionOf(change.State);

            if (changed && rejection.HasValue && !cases.Any(c => c.StoreAppId == storeApp.Id && c.ExternalVersionId == change.ExternalVersionId && c.State == RejectionState.Open))
            {
                var rejectionCase = RejectionCases.Open(organizationId, storeApp.AppId, storeApp.Id, storeApp.Store, change.ExternalVersionId, Trim(change.VersionString), Trim(change.BuildNumber), rejection.Value, source, now, ActionChannel.System, null);

                rejectionCase.PreviousCaseId = cases.Where(c => c.StoreAppId == storeApp.Id && c.ExternalVersionId == change.ExternalVersionId).OrderByDescending(c => c.OpenedDate).Select(c => (int?)c.Id).FirstOrDefault();
                opened.Add(rejectionCase);
                cases.Add(rejectionCase);
            }

            if (changed && AppStoreVersionStates.IsSubmitted(change.State))
            {
                foreach (var open in cases.Where(c => c.StoreAppId == storeApp.Id && c.State == RejectionState.Open && c.Id != 0).ToList())
                {
                    CloseCase(open, now, $"{change.VersionString}: {change.State}");
                    closed.Add(open);
                    closedIds.Add(open.Id);
                }
            }

            outcomes.Add((new VersionStateOutcome(storeApp.Id, change.ExternalVersionId, previous, change.State, changed, null, closedIds), storeApp, change));
        }

        if (added.Count > 0)
            await _stateRepository.AddRangeAsync(added, cancellationToken);

        if (updated.Count > 0)
            await _stateRepository.UpdateAsync(updated, cancellationToken, s => s.State, s => s.PreviousState, s => s.ChangedDate, s => s.SubmittedDate, s => s.VersionString, s => s.BuildNumber);

        if (opened.Count > 0)
        {
            await _caseRepository.AddRangeAsync(opened, cancellationToken);
            await _stepRepository.OpenReplyStepsAsync(organizationId, [.. opened.Where(c => c.Store == StoreKind.Apple).Select(c => c.AppId)], cancellationToken);
        }

        if (closed.Count > 0)
            await _caseRepository.UpdateAsync(closed, cancellationToken, c => c.State, c => c.ClosedDate, c => c.ClosedVia, c => c.Timeline);

        var result = new List<VersionStateOutcome>();

        foreach (var (outcome, storeApp, change) in outcomes)
        {
            var openedCase = opened.FirstOrDefault(c => c.StoreAppId == storeApp.Id && c.ExternalVersionId == change.ExternalVersionId);

            result.Add(outcome with { OpenedCaseId = openedCase?.Id });

            if (openedCase != null)
                await NotifyRejectionAsync(organizationId, storeApp, openedCase, cancellationToken);
            else if (outcome.Changed && (source == RejectionSource.Webhook || outcome.PreviousState != null))
                await NotifyStateAsync(organizationId, storeApp, change, outcome.PreviousState, cancellationToken);
        }

        foreach (var closedCase in closed)
            await PublishClosedAsync(organizationId, closedCase, RejectionCloseReason.Submission, cancellationToken);

        return result;
    }

    /// <inheritdoc/>
    public async Task<int> CloseForSubmissionAsync(int organizationId, int storeAppId, CancellationToken cancellationToken)
    {
        var open = await _caseRepository.GetAllAsync(c => c.StoreAppId == storeAppId && c.OrganizationId == organizationId && c.State == RejectionState.Open,
                                                     projection: c => new RejectionCase { Id = c.Id, StoreAppId = c.StoreAppId, AppId = c.AppId, VersionString = c.VersionString, State = c.State, Timeline = c.Timeline },
                                                     cancellationToken: cancellationToken);

        if (open.Count == 0)
            return 0;

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        open.ForEach(c => CloseCase(c, now, "submitted again through storeasy"));

        await _caseRepository.UpdateAsync(open, cancellationToken, c => c.State, c => c.ClosedDate, c => c.ClosedVia, c => c.Timeline);

        foreach (var closedCase in open)
            await PublishClosedAsync(organizationId, closedCase, RejectionCloseReason.Submission, cancellationToken);

        return open.Count;
    }

    private static void CloseCase(RejectionCase rejectionCase, DateTime now, string note)
    {
        rejectionCase.State = RejectionState.Closed;
        rejectionCase.ClosedDate = now;
        rejectionCase.ClosedVia = RejectionCloseReason.Submission;

        RejectionCases.AddTimeline(rejectionCase, now, "closed", note, ActionChannel.System);
    }

    private async Task NotifyRejectionAsync(int organizationId, StoreApp storeApp, RejectionCase rejectionCase, CancellationToken cancellationToken)
    {
        await _auditTrail.WriteAsync(organizationId, AuditAction.RejectionOpened, rejectionCase.Id.ToString(CultureInfo.InvariantCulture), $"{storeApp.Name} {rejectionCase.VersionString}: {rejectionCase.Kind}, rejection case opened.", storeApp.Store, cancellationToken: cancellationToken);

        var data = new { caseId = rejectionCase.Id, appId = storeApp.AppId, storeAppId = storeApp.Id, store = storeApp.Store.ToString(), versionString = rejectionCase.VersionString, buildNumber = rejectionCase.BuildNumber, kind = rejectionCase.Kind.ToString(), openedVia = rejectionCase.OpenedVia.ToString() };

        await _notificationDispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = organizationId,
            Kind = NotificationType.Rejection,
            Roles = NotificationAudiences.OwnersAndAdmins,
            TextKey = MessageKey.NotificationRejection,
            TextArgs = [storeApp.Name, rejectionCase.VersionString, rejectionCase.Kind == RejectionKind.MetadataRejected ? "METADATA_REJECTED" : "REJECTED"],
            Data = data,
            AppId = storeApp.AppId,
            RelatedEntity = NotificationEntity.RejectionCase,
            RelatedEntityId = rejectionCase.Id.ToString(CultureInfo.InvariantCulture),
            ActionLink = $"/apps/{storeApp.AppId}/rejections/{rejectionCase.Id}",
            WebhookEventType = OrgWebhookEventTypes.RejectionOpened,
            ActivityType = ActivityEventTypes.RejectionOpened,
        }, cancellationToken);
    }

    private Task NotifyStateAsync(int organizationId, StoreApp storeApp, VersionStateChange change, string previous, CancellationToken cancellationToken)
        => _notificationDispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = organizationId,
            Kind = NotificationType.VersionState,
            Roles = NotificationAudiences.Editors,
            TextKey = MessageKey.NotificationVersionState,
            TextArgs = [storeApp.Name, change.VersionString, previous ?? "-", change.State],
            Data = new { appId = storeApp.AppId, storeAppId = storeApp.Id, versionId = change.ExternalVersionId, versionString = change.VersionString, oldValue = previous, newValue = change.State },
            AppId = storeApp.AppId,
            RelatedEntity = NotificationEntity.AppVersionState,
            RelatedEntityId = change.ExternalVersionId,
            ActionLink = $"/apps/{storeApp.AppId}/release",
            WebhookEventType = OrgWebhookEventTypes.VersionStateChanged,
            ActivityType = ActivityEventTypes.VersionStateChanged,
        }, cancellationToken);

    private async Task PublishClosedAsync(int organizationId, RejectionCase rejectionCase, RejectionCloseReason reason, CancellationToken cancellationToken)
    {
        await _auditTrail.WriteAsync(organizationId, AuditAction.RejectionClosed, rejectionCase.Id.ToString(CultureInfo.InvariantCulture), $"Rejection case of {rejectionCase.VersionString} closed ({reason}).", cancellationToken: cancellationToken);

        await _orgWebhookPublisher.EnqueueAsync(organizationId, OrgWebhookEventTypes.RejectionClosed, new { caseId = rejectionCase.Id, rejectionCase.StoreAppId, rejectionCase.VersionString, closedVia = reason.ToString() }, cancellationToken);
    }

    private static string Trim(string value) => value?.Length > AppVersionState.TextMaxLength ? value[..AppVersionState.TextMaxLength] : value;
}
