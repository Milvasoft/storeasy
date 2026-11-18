using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using System.Text;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// The <see cref="StoreJobKind.ApplyChangePlan"/> job: writes an approved plan to its store through the store's
/// <see cref="IStoreContentGateway"/>, every call a step on the live activity view. The demo account password is opened
/// from the app's sealed saved answer here, in memory, and never stored with the plan. On success the plan becomes
/// Applied and one audit entry per changed field is written in the name of the member who approved it. A failure is
/// retried by the runner when transient; the gateways write only what still differs, so a retry does not repeat work. An
/// applied submission for review closes the store record's open rejection cases (<see cref="IVersionStateTracker.CloseForSubmissionAsync"/>).
/// </summary>
/// <param name="planRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="answerRepository"></param>
/// <param name="gateways"></param>
/// <param name="secretProtector"></param>
/// <param name="auditTrail"></param>
/// <param name="activityPublisher"></param>
/// <param name="timeProvider"></param>
/// <param name="extensions"></param>
/// <param name="versionStateTracker"></param>
public sealed class ApplyChangePlanJobHandler(IStoreasyRepositoryBase<ChangePlan> planRepository,
                                             IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                             IStoreasyRepositoryBase<SavedAnswer> answerRepository,
                                             IEnumerable<IStoreContentGateway> gateways,
                                             ISecretProtector secretProtector,
                                             IAuditTrail auditTrail,
                                             IActivityPublisher activityPublisher,
                                             TimeProvider timeProvider,
                                             IEnumerable<IChangePlanExtension> extensions,
                                             IVersionStateTracker versionStateTracker) : IStoreJobHandler
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IStoreasyRepositoryBase<ChangePlan> _planRepository = planRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = answerRepository;
    private readonly IEnumerable<IStoreContentGateway> _gateways = gateways;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly IActivityPublisher _activityPublisher = activityPublisher;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IEnumerable<IChangePlanExtension> _extensions = extensions;
    private readonly IVersionStateTracker _versionStateTracker = versionStateTracker;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.ApplyChangePlan;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<ApplyChangePlanPayload>() ?? throw new StoreJobException("The job has no plan.", transient: false);

        var plan = await _planRepository.GetFirstOrDefaultAsync(p => p.Id == payload.PlanId && p.OrganizationId == context.OrganizationId,
                                                                projection: p => new ChangePlan
                                                                {
                                                                    Id = p.Id,
                                                                    AppId = p.AppId,
                                                                    StoreAppId = p.StoreAppId,
                                                                    Store = p.Store,
                                                                    Kind = p.Kind,
                                                                    Status = p.Status,
                                                                    Title = p.Title,
                                                                    VersionId = p.VersionId,
                                                                    Items = p.Items,
                                                                    Changes = p.Changes,
                                                                    DecidedByUserId = p.DecidedByUserId,
                                                                    DecidedByUserName = p.DecidedByUserName,
                                                                    DecisionChannel = p.DecisionChannel,
                                                                },
                                                                cancellationToken: cancellationToken) ?? throw new StoreJobException("The plan was not found.", transient: false);

        if (plan.Status == ChangePlanStatus.Applied)
            return new StoreJobOutcome($"Plan {plan.Id} was already applied.");

        if (plan.Status != ChangePlanStatus.Approved)
            throw new StoreJobException($"Plan {plan.Id} is {plan.Status}, not approved.", transient: false);

        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == plan.StoreAppId && s.OrganizationId == context.OrganizationId,
                                                                      projection: s => new { s.ExternalId, s.StoreConnectionId },
                                                                      cancellationToken: cancellationToken) ?? throw new StoreJobException("The store record was removed.", transient: false);

        if (record.StoreConnectionId == null)
            throw new StoreJobException("The store record has no connection.", transient: false);

        var connection = await context.OpenConnectionAsync(record.StoreConnectionId.Value, cancellationToken) ?? throw new StoreJobException("The store connection was removed.", transient: false);
        var extension = _extensions.FirstOrDefault(e => e.Handles(plan.Kind));
        IReadOnlyList<string> outcome;

        if (extension != null)
        {
            outcome = await extension.ApplyAsync(new ChangePlanApplyRequest(context.OrganizationId, plan.AppId, plan.StoreAppId, plan.Id, plan.Kind, connection.Store, new StoreContentTarget(connection.SecretJson, record.ExternalId, plan.VersionId), plan.Changes),
                                                 context,
                                                 cancellationToken);
        }
        else
        {
            outcome = await ApplyContentAsync(context, plan, record.ExternalId, connection, cancellationToken);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _planRepository.ExecuteUpdateAsync(plan.Id,
                                                 new SetPropertyBuilder<ChangePlan>().SetPropertyValue(p => p.Status, ChangePlanStatus.Applied)
                                                                                     .SetPropertyValue(p => p.AppliedDate, (DateTime?)now),
                                                 cancellationToken);

        // The version went to review again through storeasy: its open rejection cases are answered.
        if (plan.Kind == ChangePlanKind.SubmitForReview)
            await _versionStateTracker.CloseForSubmissionAsync(context.OrganizationId, plan.StoreAppId, cancellationToken);

        var items = plan.Items?.Count > 0 ? plan.Items : [new Domain.JsonModels.PlanItem { Field = plan.Kind.ToString(), Action = ChangeAction.Update }];

        await _auditTrail.WriteManyAsync([.. items.Select(item => new AuditEntry(context.OrganizationId,
                                                                                   extension?.AuditActionOf(plan.Kind, item) ?? ChangePlanRules.AuditActionOf(plan.Kind),
                                                                                   plan.StoreAppId.ToString(),
                                                                                   $"Plan {plan.Id}: {item.Action} {item.Group} {item.Locale} {item.Field}".Replace("  ", " "),
                                                                                   plan.Store,
                                                                                   new { PlanId = plan.Id, context.JobId, item.Group, item.Locale, item.Field, item.Before, item.After },
                                                                                   plan.DecidedByUserId,
                                                                                   plan.DecidedByUserName ?? "system",
                                                                                   plan.DecisionChannel))],
                                         cancellationToken);

        await _activityPublisher.PublishAsync(new ActivityEventDto
        {
            Type = ActivityEventTypes.PlanApplied,
            OrganizationId = context.OrganizationId,
            PlanId = plan.Id,
            JobId = context.JobId,
            Store = plan.Store,
            AppId = plan.AppId,
            Message = plan.Title,
            Date = now,
        }, cancellationToken);

        return new StoreJobOutcome(SecretRedactor.RedactAndTrim($"Plan {plan.Id} applied: {string.Join(" ", outcome)}", 500), new { PlanId = plan.Id, Steps = outcome });
    }

    private async Task<IReadOnlyList<string>> ApplyContentAsync(IStoreJobContext context, ChangePlan plan, string externalId, OpenedStoreConnection connection, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == connection.Store) ?? throw new StoreJobException($"{connection.Store} is not supported yet.", transient: false);
        var changes = JsonSerializer.Deserialize<ContentChangeSet>(plan.Changes, _json) ?? new ContentChangeSet();

        if (changes.DemoPasswordFromAnswers)
            await OpenDemoPasswordAsync(context.OrganizationId, plan.AppId, changes, cancellationToken);

        try
        {
            return await gateway.ApplyAsync(new StoreContentTarget(connection.SecretJson, externalId, plan.VersionId), changes, context, cancellationToken);
        }
        finally
        {
            changes.Secrets.Clear();
        }
    }

    private async Task OpenDemoPasswordAsync(int organizationId, int appId, ContentChangeSet changes, CancellationToken cancellationToken)
    {
        var answer = await _answerRepository.GetFirstOrDefaultAsync(a => a.AppId == appId && a.OrganizationId == organizationId && a.Kind == SavedAnswerKind.ReviewLogin,
                                                                    projection: a => new { a.EncryptedSecret, a.EncryptedDataKey, a.KeyVersion },
                                                                    cancellationToken: cancellationToken);

        if (answer?.EncryptedSecret == null || answer.KeyVersion == null)
            throw new StoreJobException("The demo account password is not saved in the app's answers.", transient: false);

        var secret = _secretProtector.Unprotect(new ProtectedSecret(answer.EncryptedSecret, answer.EncryptedDataKey, answer.KeyVersion.Value), SavedAnswerSecrets.Context(organizationId, appId));

        try
        {
            changes.Secrets[AppleContentGateway.DemoPasswordSecret] = Encoding.UTF8.GetString(secret);
        }
        finally
        {
            Array.Clear(secret);
        }
    }
}
