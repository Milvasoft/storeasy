using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Content;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Stores;
using System.Text.Json;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IChangePlanService"/>. Approval is a single conditional UPDATE (pending and not expired), so two
/// members approving at once queue one job; the store is read again before it and a changed fingerprint refuses the
/// approval with <see cref="MessageKey.PlanConflict"/>, writing nothing.
/// </summary>
/// <param name="planRepository"></param>
/// <param name="jobRepository"></param>
/// <param name="contentService"></param>
/// <param name="storeJobQueue"></param>
/// <param name="auditTrail"></param>
/// <param name="activityPublisher"></param>
/// <param name="currentUser"></param>
/// <param name="organizationAccess"></param>
/// <param name="rulesCatalog"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
/// <param name="extensions"></param>
public sealed class ChangePlanService(IStoreasyRepositoryBase<ChangePlan> planRepository,
                                     IStoreasyRepositoryBase<StoreJob> jobRepository,
                                     IStoreContentService contentService,
                                     IStoreJobQueue storeJobQueue,
                                     IAuditTrail auditTrail,
                                     IActivityPublisher activityPublisher,
                                     ICurrentUser currentUser,
                                     IOrganizationAccess organizationAccess,
                                     IRulesCatalog rulesCatalog,
                                     StoreApiOptions options,
                                     TimeProvider timeProvider,
                                     IEnumerable<IChangePlanExtension> extensions) : IChangePlanService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly IStoreasyRepositoryBase<ChangePlan> _planRepository = planRepository;
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = jobRepository;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly IActivityPublisher _activityPublisher = activityPublisher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IOrganizationAccess _organizationAccess = organizationAccess;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly StoreApiOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IEnumerable<IChangePlanExtension> _extensions = extensions;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> SaveAsync(StoreAppContext app, StoreContentSnapshot snapshot, PlanDraft draft, bool dryRun, string confirm, CancellationToken cancellationToken)
    {
        if (draft.IsEmpty)
            return Response<ChangePlanDto>.Error(null, MessageKey.PlanNoChanges);

        var now = Now();
        var versionString = draft.VersionString ?? snapshot?.VersionString;
        var permission = draft.RequiredPermission ?? ChangePlanRules.RequiredPermission(draft.Kind, app.Store, draft.Track);

        var plan = new ChangePlan
        {
            OrganizationId = app.OrganizationId,
            AppId = app.AppId,
            StoreAppId = app.StoreAppId,
            Store = app.Store,
            Kind = draft.Kind,
            Status = ChangePlanStatus.Pending,
            Title = Trim(draft.Title, ChangePlan.TitleMaxLength),
            Source = _auditTrail.Channel,
            CreatedByUserId = _currentUser.Id,
            CreatedByUserName = _currentUser.UserName,
            RequiredPermission = permission,
            ConfirmText = draft.ConfirmText != null ? Trim(draft.ConfirmText, ChangePlan.TitleMaxLength) : ChangePlanRules.NeedsConfirmation(draft.Kind, draft.Track) ? Trim(ChangePlanRules.ConfirmationText(app.AppName, versionString), ChangePlan.TitleMaxLength) : null,
            VersionId = draft.VersionId ?? snapshot?.VersionId,
            VersionString = Trim(versionString, 100),
            Items = draft.Items,
            Warnings = draft.Warnings,
            Calls = draft.Calls,
            Files = draft.Files,
            Changes = JsonSerializer.Serialize(draft.CustomChanges ?? draft.Changes, _json),
            BaseKeys = [.. draft.BaseKeys.Distinct()],
            BaseHash = snapshot == null ? null : ContentDiff.Hash(ContentSnapshots.Flatten(snapshot), draft.BaseKeys),
            CatalogVersion = _rulesCatalog.Current.Version,
            ExpiryDate = now.AddMinutes(Math.Max(1, _options.PlanLifetimeMinutes)),
        };

        await _planRepository.AddAsync(plan, cancellationToken);

        await _activityPublisher.PublishAsync(new ActivityEventDto
        {
            Type = ActivityEventTypes.PlanCreated,
            OrganizationId = plan.OrganizationId,
            PlanId = plan.Id,
            Store = plan.Store,
            AppId = plan.AppId,
            Message = plan.Title,
            Date = now,
        }, cancellationToken);

        if (!dryRun)
            return await ApproveAsync(app.OrganizationId, plan.Id, confirm, cancellationToken);

        return Response<ChangePlanDto>.Success(await GetAsync(app.OrganizationId, plan.Id, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> ApproveAsync(int organizationId, long planId, string confirm, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetFirstOrDefaultAsync(p => p.Id == planId && p.OrganizationId == organizationId,
                                                                projection: p => new ChangePlan
                                                                {
                                                                    Id = p.Id,
                                                                    OrganizationId = p.OrganizationId,
                                                                    StoreAppId = p.StoreAppId,
                                                                    AppId = p.AppId,
                                                                    Store = p.Store,
                                                                    Kind = p.Kind,
                                                                    Title = p.Title,
                                                                    Status = p.Status,
                                                                    ExpiryDate = p.ExpiryDate,
                                                                    RequiredPermission = p.RequiredPermission,
                                                                    ConfirmText = p.ConfirmText,
                                                                    VersionId = p.VersionId,
                                                                    BaseKeys = p.BaseKeys,
                                                                    BaseHash = p.BaseHash,
                                                                },
                                                                cancellationToken: cancellationToken);

        if (plan == null)
            return Response<ChangePlanDto>.Error(null, MessageKey.ChangePlanNotFound);

        var now = Now();

        if (plan.Status != ChangePlanStatus.Pending)
            return Response<ChangePlanDto>.Error(null, MessageKey.PlanNotPending);

        if (plan.ExpiryDate <= now)
            return Response<ChangePlanDto>.Error(null, MessageKey.PlanExpired);

        var role = _organizationAccess.CurrentRole ?? await _organizationAccess.GetRoleAsync(organizationId, _currentUser.GetRequiredId(), cancellationToken);

        if (role == null || !OrgRoleRules.Allows(role.Value, plan.RequiredPermission))
            return Response<ChangePlanDto>.Error(null, MessageKey.OrganizationForbidden);

        if (!ChangePlanRules.ConfirmationMatches(plan.ConfirmText, confirm))
            return Response<ChangePlanDto>.Error(null, MessageKey.PlanConfirmMismatch);

        if (plan.BaseHash != null && plan.BaseKeys?.Count > 0)
        {
            var app = await _contentService.GetStoreAppAsync(organizationId, plan.StoreAppId, cancellationToken);

            if (app == null)
                return Response<ChangePlanDto>.Error(null, MessageKey.StoreAppNotFound);

            var extension = _extensions.FirstOrDefault(e => e.Handles(plan.Kind));
            var read = extension == null
                ? await _contentService.ReadAsync(app, ContentSnapshots.PartsOf(plan.Kind), plan.VersionId, cancellationToken)
                : await extension.ReadBaseAsync(app, plan.BaseKeys, cancellationToken);

            if (!read.IsSuccess)
                return Response<ChangePlanDto>.Error(null, read.ErrorKey);

            if (ContentDiff.Hash(ContentSnapshots.Flatten(read.Snapshot), plan.BaseKeys) != plan.BaseHash)
                return Response<ChangePlanDto>.Error(null, MessageKey.PlanConflict);
        }

        var approved = await _planRepository.ExecuteUpdateAsync(p => p.Id == plan.Id && p.Status == ChangePlanStatus.Pending && p.ExpiryDate > now,
                                                                new SetPropertyBuilder<ChangePlan>().SetPropertyValue(p => p.Status, ChangePlanStatus.Approved)
                                                                                                    .SetPropertyValue(p => p.DecidedByUserId, _currentUser.Id)
                                                                                                    .SetPropertyValue(p => p.DecidedByUserName, _currentUser.UserName)
                                                                                                    .SetPropertyValue(p => p.DecisionChannel, (ActionChannel?)_auditTrail.Channel)
                                                                                                    .SetPropertyValue(p => p.DecisionDate, (DateTime?)now),
                                                                cancellationToken);

        if (approved == 0)
            return Response<ChangePlanDto>.Error(null, MessageKey.PlanNotPending);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(organizationId, StoreJobKind.ApplyChangePlan, new ApplyChangePlanPayload(plan.Id), plan.Store, plan.AppId)], cancellationToken);

        await _planRepository.ExecuteUpdateAsync(plan.Id, new SetPropertyBuilder<ChangePlan>().SetPropertyValue(p => p.StoreJobId, (long?)jobIds[0]), cancellationToken);

        await _auditTrail.WriteAsync(organizationId,
                                     AuditAction.PlanApproved,
                                     plan.Id.ToString(),
                                     $"Plan {plan.Id} approved: {plan.Title}",
                                     plan.Store,
                                     new { PlanId = plan.Id, plan.Kind, JobId = jobIds[0] },
                                     cancellationToken);

        await _activityPublisher.PublishAsync(new ActivityEventDto
        {
            Type = ActivityEventTypes.PlanDecided,
            OrganizationId = organizationId,
            PlanId = plan.Id,
            JobId = jobIds[0],
            Store = plan.Store,
            AppId = plan.AppId,
            Message = ChangePlanStatus.Approved.ToString(),
            Date = now,
        }, cancellationToken);

        return Response<ChangePlanDto>.Success(await GetAsync(organizationId, plan.Id, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<ChangePlanDto> GetAsync(int organizationId, long planId, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetFirstOrDefaultAsync(p => p.Id == planId && p.OrganizationId == organizationId,
                                                                projection: ChangePlanDto.Projection,
                                                                cancellationToken: cancellationToken);

        if (plan == null)
            return null;

        if (plan.JobId != null)
        {
            var job = await _jobRepository.GetFirstOrDefaultAsync(j => j.Id == plan.JobId,
                                                                  projection: j => new { j.Status, j.Error },
                                                                  cancellationToken: cancellationToken);

            plan.Status = ChangePlanRules.Effective(plan.Status, plan.ExpiryDate, job?.Status, Now());
            plan.JobError = job?.Status is StoreJobStatus.Failed ? job.Error : null;
        }
        else
        {
            plan.Status = ChangePlanRules.Effective(plan.Status, plan.ExpiryDate, null, Now());
        }

        return plan;
    }

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> DecideAsync(int organizationId, long planId, ChangePlanStatus status, string note, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetFirstOrDefaultAsync(p => p.Id == planId && p.OrganizationId == organizationId,
                                                                projection: p => new ChangePlan { Id = p.Id, Status = p.Status, RequiredPermission = p.RequiredPermission, Store = p.Store, AppId = p.AppId, Title = p.Title },
                                                                cancellationToken: cancellationToken);

        if (plan == null)
            return Response<ChangePlanDto>.Error(null, MessageKey.ChangePlanNotFound);

        if (plan.Status != ChangePlanStatus.Pending)
            return Response<ChangePlanDto>.Error(null, MessageKey.PlanNotPending);

        if (status == ChangePlanStatus.Rejected)
        {
            var role = _organizationAccess.CurrentRole ?? await _organizationAccess.GetRoleAsync(organizationId, _currentUser.GetRequiredId(), cancellationToken);

            if (role == null || !OrgRoleRules.Allows(role.Value, plan.RequiredPermission))
                return Response<ChangePlanDto>.Error(null, MessageKey.OrganizationForbidden);
        }

        var now = Now();

        var decided = await _planRepository.ExecuteUpdateAsync(p => p.Id == plan.Id && p.Status == ChangePlanStatus.Pending,
                                                               new SetPropertyBuilder<ChangePlan>().SetPropertyValue(p => p.Status, status)
                                                                                                   .SetPropertyValue(p => p.DecidedByUserId, _currentUser.Id)
                                                                                                   .SetPropertyValue(p => p.DecidedByUserName, _currentUser.UserName)
                                                                                                   .SetPropertyValue(p => p.DecisionChannel, (ActionChannel?)_auditTrail.Channel)
                                                                                                   .SetPropertyValue(p => p.DecisionNote, Trim(note, ChangePlan.DecisionNoteMaxLength))
                                                                                                   .SetPropertyValue(p => p.DecisionDate, (DateTime?)now),
                                                               cancellationToken);

        if (decided == 0)
            return Response<ChangePlanDto>.Error(null, MessageKey.PlanNotPending);

        await _auditTrail.WriteAsync(organizationId,
                                     status == ChangePlanStatus.Rejected ? AuditAction.PlanRejected : AuditAction.PlanDiscarded,
                                     plan.Id.ToString(),
                                     $"Plan {plan.Id} {status.ToString().ToLowerInvariant()}: {plan.Title}",
                                     plan.Store,
                                     new { PlanId = plan.Id, Reason = note },
                                     cancellationToken);

        await _activityPublisher.PublishAsync(new ActivityEventDto
        {
            Type = ActivityEventTypes.PlanDecided,
            OrganizationId = organizationId,
            PlanId = plan.Id,
            Store = plan.Store,
            AppId = plan.AppId,
            Message = status.ToString(),
            Date = now,
        }, cancellationToken);

        return Response<ChangePlanDto>.Success(await GetAsync(organizationId, plan.Id, cancellationToken));
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string Trim(string value, int max) => value == null || value.Length <= max ? value : value[..max];
}
