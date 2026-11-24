using Storeasy.Domain;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.EasDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Content;
using Storeasy.Application.Utils.Eas;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Eas;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using System.Globalization;

namespace Storeasy.Infrastructure.Eas;

/// <summary>
/// The after build steps and EAS Submit of <see cref="EasService"/>: what a member chose when starting a build (send to the
/// intake, or EAS Submit) runs once the build finished; once the build reached its test channel the review (App Store) or
/// production (Play) plans are prepared and left pending, never approved. EAS Submit can also be started on a finished build.
/// </summary>
public sealed partial class EasService
{
    private static readonly string _claimChainSql = $"""
                                                     UPDATE "{TableNames.EasBuilds}"
                                                     SET "ChainDone" = TRUE
                                                     WHERE "Id" = ANY(@ids) AND NOT "ChainDone"
                                                     RETURNING "Id" AS "Value"
                                                     """;

    private const int _reviewPlanWaitHours = 24;

    #region Defaults and submissions

    /// <inheritdoc/>
    public async Task<Response<EasAppDto>> SaveChainAsync(int organizationId, int appId, EasChainDto chain, CancellationToken cancellationToken)
    {
        chain ??= new EasChainDto();

        var track = chain.AfterBuild == EasAfterBuild.Submit ? chain.SubmitTrack?.Trim() ?? "internal" : null;
        var status = chain.AfterBuild == EasAfterBuild.Submit ? chain.SubmitReleaseStatus?.Trim() ?? "COMPLETED" : null;

        if (EasRules.ChainProblem(chain.AfterBuild, track, status) is string problem)
            return Response<EasAppDto>.Error(null, problem);

        var link = await _linkRepository.GetFirstOrDefaultAsync(l => l.AppId == appId && l.OrganizationId == organizationId, cancellationToken: cancellationToken);

        if (link == null)
            return Response<EasAppDto>.Error(null, MessageKey.EasProjectNotLinked);

        link.AfterBuild = chain.AfterBuild;
        link.PrepareReviewPlan = chain.PrepareReviewPlan;
        link.SubmitTrack = track;
        link.SubmitReleaseStatus = status;
        await _linkRepository.UpdateAsync(link, cancellationToken);
        await _auditTrail.WriteAsync(organizationId, AuditAction.EasChainSaved, appId.ToString(CultureInfo.InvariantCulture), $"After build defaults: {chain.AfterBuild}{(track == null ? string.Empty : $" ({track}, {status})")}, review plan {(chain.PrepareReviewPlan ? "on" : "off")}.", details: new { chain.AfterBuild, chain.PrepareReviewPlan, track, status }, cancellationToken: cancellationToken);

        return await GetAppAsync(organizationId, appId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Response<EasSubmissionDto>> StartSubmissionAsync(int organizationId, long buildId, string track, string releaseStatus, string confirm, CancellationToken cancellationToken)
    {
        var build = await _buildRepository.GetFirstOrDefaultAsync(b => b.Id == buildId && b.OrganizationId == organizationId, cancellationToken: cancellationToken);

        if (build == null)
            return Response<EasSubmissionDto>.Error(null, MessageKey.EasBuildNotFound);

        track = build.Platform == EasPlatform.Android ? track?.Trim() ?? "internal" : null;
        releaseStatus = build.Platform == EasPlatform.Android ? releaseStatus?.Trim() ?? "COMPLETED" : null;

        if (build.Platform == EasPlatform.Android && EasRules.SubmitProblem(track, releaseStatus) is string problem)
            return Response<EasSubmissionDto>.Error(null, problem);

        var appName = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == build.AppId && a.OrganizationId == organizationId, projection: a => a.Name, cancellationToken: cancellationToken);

        if (!EasRules.ConfirmationMatches(EasRules.ConfirmText(appName ?? string.Empty, build.Platform), confirm))
            return Response<EasSubmissionDto>.Error(null, MessageKey.PlanConfirmMismatch);

        if (EasRules.IsProductionSubmit(build.Platform, track) && !await AllowsAsync(organizationId, OrgPermission.ReleaseProduction, cancellationToken))
            return Response<EasSubmissionDto>.Error(null, MessageKey.OrganizationForbidden);

        var token = await OpenAsync(organizationId, cancellationToken);

        if (token == null)
            return Response<EasSubmissionDto>.Error(null, MessageKey.ExpoConnectionMissing);

        var (submission, error) = await SubmitCoreAsync(build, token, track, releaseStatus, false, cancellationToken);

        return submission == null ? Response<EasSubmissionDto>.Error(null, error) : Response<EasSubmissionDto>.Success(EasSubmissionDto.From(submission));
    }

    /// <inheritdoc/>
    public async Task<Response<List<EasSubmissionDto>>> ListSubmissionsAsync(int organizationId, int appId, CancellationToken cancellationToken)
        => Response<List<EasSubmissionDto>>.Success([.. (await _submissionRepository.GetAllAsync(s => s.AppId == appId && s.OrganizationId == organizationId, projection: EasSubmissionDto.Projection, cancellationToken: cancellationToken))
                                                       .OrderByDescending(s => s.StartedDate ?? DateTime.MinValue).ThenByDescending(s => s.Id).Take(50)]);

    /// <inheritdoc/>
    public async Task<Response<EasSubmissionDto>> GetSubmissionAsync(int organizationId, long id, CancellationToken cancellationToken)
    {
        var submission = await _submissionRepository.GetFirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, projection: EasSubmissionDto.Projection, cancellationToken: cancellationToken);

        return submission == null ? Response<EasSubmissionDto>.Error(null, MessageKey.EasSubmissionNotFound) : Response<EasSubmissionDto>.Success(submission);
    }

    /// <summary>
    /// Starts EAS Submit of a finished build: the store identifiers come from the app's store records in storeasy, the
    /// credentials from the project on expo.dev (storeasy never sends its own store keys to Expo). Returns the saved
    /// submission, or the message key of why it could not start.
    /// </summary>
    private async Task<(EasSubmission Submission, string Error)> SubmitCoreAsync(EasBuild build, string token, string track, string releaseStatus, bool fromChain, CancellationToken cancellationToken)
    {
        if (build.Status != EasBuildStatus.Finished || build.BuildId == null)
            return (null, MessageKey.EasBuildNotFinished);

        var store = build.Platform == EasPlatform.Ios ? StoreKind.Apple : StoreKind.Google;
        var record = await _contentService.GetStoreAppOfAppAsync(build.OrganizationId, build.AppId, store, cancellationToken);
        var identifiers = record == null
            ? null
            : await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == record.StoreAppId && s.OrganizationId == build.OrganizationId, projection: s => new StoreApp { ExternalId = s.ExternalId, BundleId = s.BundleId }, cancellationToken: cancellationToken);

        if (identifiers == null)
            return (null, MessageKey.EasStoreRecordMissing);

        var appIdentifier = store == StoreKind.Apple ? identifiers.BundleId : identifiers.ExternalId;
        var link = await _linkRepository.GetFirstOrDefaultAsync(l => l.AppId == build.AppId && l.OrganizationId == build.OrganizationId, projection: l => new EasProjectLink { AccountName = l.AccountName, Slug = l.Slug }, cancellationToken: cancellationToken);

        try
        {
            var credentials = await _expo.GetSubmitCredentialsAsync(token, build.ProjectId, build.Platform, appIdentifier, cancellationToken);
            var keyId = store == StoreKind.Apple ? credentials.AscApiKeyId : credentials.GoogleServiceAccountKeyId;

            if (keyId == null)
                return (null, _localizer[MessageKey.EasSubmitCredentialsMissing, $"{_options.WebsiteUrl.TrimEnd('/')}/accounts/{Uri.EscapeDataString(link?.AccountName ?? string.Empty)}/projects/{Uri.EscapeDataString(link?.Slug ?? string.Empty)}/credentials"]);

            var remote = await _expo.StartSubmissionAsync(token, new ExpoSubmitRequest(build.ProjectId,
                                                                                       build.BuildId,
                                                                                       build.Platform,
                                                                                       store == StoreKind.Apple ? identifiers.ExternalId : null,
                                                                                       credentials.AscApiKeyId,
                                                                                       store == StoreKind.Google ? identifiers.ExternalId : null,
                                                                                       credentials.GoogleServiceAccountKeyId,
                                                                                       track,
                                                                                       releaseStatus),
                                                          cancellationToken);
            var submission = new EasSubmission
            {
                OrganizationId = build.OrganizationId,
                AppId = build.AppId,
                EasBuildId = build.Id,
                SubmissionId = remote.Id,
                Platform = build.Platform,
                Track = track,
                ReleaseStatus = releaseStatus,
                StartedBy = fromChain ? build.StartedBy : Trim(_currentUser.UserName, EasSubmission.ShortMaxLength),
                FromChain = fromChain,
                StartedDate = remote.CreatedDate ?? Now(),
            };

            MergeSubmission(submission, remote);

            await _submissionRepository.AddAsync(submission, cancellationToken);
            await _auditTrail.WriteAsync(build.OrganizationId, AuditAction.EasSubmissionStarted, submission.Id.ToString(CultureInfo.InvariantCulture),
                                         $"EAS Submit of build {build.BuildId} ({build.Platform}{(track == null ? ", TestFlight" : $", {track}, {releaseStatus}")}) started{(fromChain ? " by the build's after build option" : string.Empty)}.",
                                         details: new { build.BuildId, submission.SubmissionId, track, releaseStatus, fromChain },
                                         cancellationToken: cancellationToken);

            return (submission, null);
        }
        catch (StoreCallException ex)
        {
            return (null, ex.Transient ? MessageKey.ExpoUnreachable : _localizer[MessageKey.EasStartRefused, SecretRedactor.RedactAndTrim(ex.Message, 500)]);
        }
    }

    #endregion

    #region Follow

    /// <summary>
    /// Organizations with work waiting after a build: an after build step, a review plan or a running submission.
    /// </summary>
    private async Task<List<int>> PendingChainOrganizationsAsync(CancellationToken cancellationToken)
    {
        var ended = new[] { EasBuildStatus.Finished, EasBuildStatus.Errored, EasBuildStatus.Canceled, EasBuildStatus.NotStarted };
        var builds = await _buildRepository.GetAllAsync(b => ended.Contains(b.Status) && ((b.AfterBuild != EasAfterBuild.None && !b.ChainDone) || !b.ReviewPlanDone),
                                                        projection: b => new EasBuild { OrganizationId = b.OrganizationId },
                                                        cancellationToken: cancellationToken);
        var running = new[] { EasSubmissionStatus.AwaitingBuild, EasSubmissionStatus.InQueue, EasSubmissionStatus.InProgress };
        var submissions = await _submissionRepository.GetAllAsync(s => running.Contains(s.Status), projection: s => new EasSubmission { OrganizationId = s.OrganizationId }, cancellationToken: cancellationToken);

        return [.. builds.Select(b => b.OrganizationId).Concat(submissions.Select(s => s.OrganizationId)).Distinct()];
    }

    /// <summary>
    /// The after build steps of ended builds, the review plans of builds that reached their test channel, and running
    /// submissions. Returns how many changed.
    /// </summary>
    private async Task<int> FollowChainAsync(Dictionary<int, string> tokens, CancellationToken cancellationToken)
    {
        var ended = new[] { EasBuildStatus.Finished, EasBuildStatus.Errored, EasBuildStatus.Canceled, EasBuildStatus.NotStarted };
        var builds = await _buildRepository.GetAllAsync(b => ended.Contains(b.Status) && ((b.AfterBuild != EasAfterBuild.None && !b.ChainDone) || !b.ReviewPlanDone), cancellationToken: cancellationToken);
        var changed = 0;

        _auditTrail.Channel = ActionChannel.System;

        // Claim the steps in the database first: the cron and a manual sync can follow at the same time, and only the run
        // whose update flips ChainDone performs a step (an intake hand off or an EAS Submit must run once).
        var pending = builds.Where(b => b.AfterBuild != EasAfterBuild.None && !b.ChainDone).ToList();
        var claimed = await ClaimChainStepsAsync([.. pending.Select(b => b.Id)], cancellationToken);

        foreach (var build in pending)
        {
            build.ChainDone = true;

            if (!claimed.Contains(build.Id))
                continue;

            changed++;

            if (build.Status != EasBuildStatus.Finished)
            {
                build.ReviewPlanDone = true;
                continue;
            }

            build.ChainError = build.AfterBuild == EasAfterBuild.Intake
                ? await SendToIntakeCoreAsync(build, cancellationToken)
                : tokens.GetValueOrDefault(build.OrganizationId) is string token
                    ? (await SubmitCoreAsync(build, token, build.SubmitTrack, build.SubmitReleaseStatus, true, cancellationToken)).Error
                    : MessageKey.ExpoConnectionMissing;

            if (build.ChainError != null)
                build.ReviewPlanDone = true;
        }

        foreach (var build in builds.Where(b => !b.ReviewPlanDone && (b.AfterBuild == EasAfterBuild.None || b.ChainDone)))
        {
            if (await PrepareReviewPlanAsync(build, cancellationToken))
                changed++;
        }

        await _buildRepository.UpdateAsync(builds, cancellationToken);

        return changed + await FollowSubmissionsAsync(tokens, cancellationToken);
    }

    /// <summary>
    /// Once a build reached its test channel, prepares its plans and leaves them pending: on the App Store attaching the build
    /// to the editable version (with the export compliance answer of an earlier build) and the review submission; on Play the
    /// production release of its version code. Returns whether the build changed.
    /// </summary>
    /// <summary>
    /// Sets <see cref="EasBuild.ChainDone"/> on the given builds that still need their after build step, in one statement,
    /// and returns the ids this run claimed; a build another run claimed first is left out.
    /// </summary>
    private async Task<HashSet<long>> ClaimChainStepsAsync(long[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
            return [];

        var claimed = await _dbContext.Database.SqlQueryRaw<long>(_claimChainSql, new NpgsqlParameter("ids", ids)).ToListAsync(cancellationToken);

        return [.. claimed];
    }

    private async Task<bool> PrepareReviewPlanAsync(EasBuild build, CancellationToken cancellationToken)
    {
        if (build.Status != EasBuildStatus.Finished)
        {
            build.ReviewPlanDone = true;

            return true;
        }

        var store = build.Platform == EasPlatform.Ios ? StoreKind.Apple : StoreKind.Google;
        var record = await _contentService.GetStoreAppOfAppAsync(build.OrganizationId, build.AppId, store, cancellationToken);

        if (record == null)
            return GiveUp(build, MessageKey.EasStoreRecordMissing);

        if (!await ReachedTestChannelAsync(build, record.StoreAppId, cancellationToken))
            return (build.FinishedDate ?? Now()).AddHours(_reviewPlanWaitHours) < Now() && GiveUp(build, "The build did not reach its test channel in time.");

        var read = await _contentService.ReadContentAsync(record, ContentParts.Release, null, store, cancellationToken);

        if (!read.IsSuccess)
            return GiveUp(build, read.ErrorKey);

        var drafts = new List<PlanDraft>();

        if (store == StoreKind.Apple)
        {
            var storeBuild = await _storeBuildRepository.GetFirstOrDefaultAsync(b => b.StoreAppId == record.StoreAppId && b.OrganizationId == build.OrganizationId && b.BuildNumber == build.AppBuildVersion,
                                                                                projection: b => new Build { ExternalId = b.ExternalId, BuildNumber = b.BuildNumber, UsesNonExemptEncryption = b.UsesNonExemptEncryption },
                                                                                cancellationToken: cancellationToken);
            var answer = storeBuild.UsesNonExemptEncryption
                         ?? (await _storeBuildRepository.GetAllAsync(b => b.StoreAppId == record.StoreAppId && b.OrganizationId == build.OrganizationId && b.UsesNonExemptEncryption != null,
                                                                     projection: b => new Build { Id = b.Id, UsesNonExemptEncryption = b.UsesNonExemptEncryption },
                                                                     cancellationToken: cancellationToken))
                            .OrderByDescending(b => b.Id).FirstOrDefault()?.UsesNonExemptEncryption;

            drafts.Add(ReleasePlanner.BuildLink(read.Snapshot, storeBuild.ExternalId, storeBuild.BuildNumber, storeBuild.UsesNonExemptEncryption == null ? answer : null));
            drafts.Add(ReleasePlanner.Submit(read.Snapshot));
        }
        else
        {
            drafts.Add(ReleasePlanner.PlayRelease(read.Snapshot, new TrackReleaseChange
            {
                Track = "production",
                VersionCodes = [build.AppBuildVersion],
                Status = "completed",
                ReleaseNotes = [],
            }, null));
        }

        var planIds = new List<long>();
        var titles = new List<string>();

        foreach (var draft in drafts.Where(d => !d.IsEmpty))
        {
            var saved = await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, true, null, cancellationToken);

            if (saved.IsSuccess)
            {
                planIds.Add(saved.Data.PlanId);
                titles.Add(saved.Data.Title);
            }
        }

        build.ReviewPlanIds = planIds;
        build.ReviewPlanDone = true;

        if (planIds.Count == 0)
            return GiveUp(build, "No plan could be prepared (nothing would change).");

        await _auditTrail.WriteAsync(build.OrganizationId, AuditAction.EasReviewPlanPrepared, build.Id.ToString(CultureInfo.InvariantCulture),
                                     $"EAS build {build.BuildId} reached its test channel; plans {string.Join(", ", planIds)} prepared and waiting for approval.",
                                     details: new { build.BuildId, planIds },
                                     cancellationToken: cancellationToken);
        await _notificationDispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = build.OrganizationId,
            Kind = NotificationType.EasBuild,
            Roles = NotificationAudiences.OwnersAndAdmins,
            TextKey = MessageKey.NotificationEasReviewPlanReady,
            TextArgs = [read.App.AppName, string.Join(", ", titles)],
            Data = new { easBuildId = build.Id, planIds },
            RelatedEntity = NotificationEntity.EasBuild,
            RelatedEntityId = $"{build.Id}:plans",
            ActionLink = $"/activity/plans/{planIds[0]}",
        }, cancellationToken);

        return true;
    }

    /// <summary>
    /// Whether the build is in its test channel: on the App Store a processed (valid) build of its build number; on Play its
    /// intake session delivered, or its EAS Submit finished.
    /// </summary>
    private async Task<bool> ReachedTestChannelAsync(EasBuild build, int storeAppId, CancellationToken cancellationToken)
    {
        if (build.Platform == EasPlatform.Ios)
        {
            var state = await _storeBuildRepository.GetFirstOrDefaultAsync(b => b.StoreAppId == storeAppId && b.OrganizationId == build.OrganizationId && b.BuildNumber == build.AppBuildVersion,
                                                                           projection: b => (BuildProcessingState?)b.ProcessingState,
                                                                           cancellationToken: cancellationToken);

            return state == BuildProcessingState.Valid;
        }

        if (build.ArtifactSessionId is long sessionId)
        {
            var status = await _sessionRepository.GetFirstOrDefaultAsync(s => s.Id == sessionId && s.OrganizationId == build.OrganizationId, projection: s => (ArtifactSessionStatus?)s.Status, cancellationToken: cancellationToken);

            return status == ArtifactSessionStatus.Delivered;
        }

        var submitted = await _submissionRepository.GetFirstOrDefaultAsync(s => s.EasBuildId == build.Id && s.OrganizationId == build.OrganizationId && s.Status == EasSubmissionStatus.Finished, projection: s => (long?)s.Id, cancellationToken: cancellationToken);

        return submitted.HasValue;
    }

    private bool GiveUp(EasBuild build, string reason)
    {
        build.ReviewPlanDone = true;
        build.ChainError = Trim(reason, EasBuild.ErrorMaxLength);

        return true;
    }

    /// <summary>
    /// Reads the running submissions from Expo; reports the ended ones. Returns how many changed.
    /// </summary>
    private async Task<int> FollowSubmissionsAsync(Dictionary<int, string> tokens, CancellationToken cancellationToken)
    {
        var running = new[] { EasSubmissionStatus.AwaitingBuild, EasSubmissionStatus.InQueue, EasSubmissionStatus.InProgress };
        var submissions = await _submissionRepository.GetAllAsync(s => running.Contains(s.Status), cancellationToken: cancellationToken);
        var changed = new List<EasSubmission>();

        foreach (var submission in submissions)
        {
            if (tokens.GetValueOrDefault(submission.OrganizationId) is not string token)
                continue;

            try
            {
                if (await _expo.GetSubmissionAsync(token, submission.SubmissionId, cancellationToken) is ExpoSubmission remote && MergeSubmission(submission, remote))
                    changed.Add(submission);
            }
            catch (StoreCallException)
            {
                // Asked again on the next run.
            }
        }

        var ended = changed.Where(s => !EasRules.IsActive(s.Status) && !s.EndReported).ToList();

        foreach (var submission in ended)
            submission.EndReported = true;

        await _submissionRepository.UpdateAsync(changed, cancellationToken);

        if (ended.Count == 0)
            return changed.Count;

        await _auditTrail.WriteManyAsync([.. ended.Select(s => new AuditEntry(s.OrganizationId,
                                                                               s.Status == EasSubmissionStatus.Finished ? AuditAction.EasSubmissionFinished : AuditAction.EasSubmissionFailed,
                                                                               s.Id.ToString(CultureInfo.InvariantCulture),
                                                                               $"EAS Submit {s.SubmissionId} ({s.Platform}{(s.Track == null ? string.Empty : ", " + s.Track)}): {s.Status}{(s.ErrorMessage == null ? string.Empty : ": " + s.ErrorMessage)}.",
                                                                               Details: new { s.SubmissionId, s.Status, s.ErrorCode },
                                                                               UserName: "system",
                                                                               Channel: ActionChannel.System))],
                                         cancellationToken);

        foreach (var submission in ended)
        {
            await _notificationDispatcher.DispatchAsync(new OrgNotification
            {
                OrganizationId = submission.OrganizationId,
                Kind = NotificationType.EasBuild,
                Roles = NotificationAudiences.Editors,
                TextKey = submission.Status == EasSubmissionStatus.Finished ? MessageKey.NotificationEasSubmissionFinished : MessageKey.NotificationEasSubmissionFailed,
                TextArgs = [submission.Platform.ToString(), submission.Track ?? "TestFlight", submission.ErrorMessage ?? submission.Status.ToString()],
                Data = new { easSubmissionId = submission.Id, submission.SubmissionId, status = submission.Status.ToString() },
                RelatedEntity = NotificationEntity.EasBuild,
                RelatedEntityId = $"submission:{submission.Id}",
                ActionLink = $"/apps/{submission.AppId}/eas",
            }, cancellationToken);
        }

        return changed.Count;
    }

    private bool MergeSubmission(EasSubmission submission, ExpoSubmission remote)
    {
        var before = (submission.Status, submission.ErrorMessage);

        submission.Status = remote.Status;
        submission.LogsUrl = Trim(remote.LogsUrl, 2000);
        submission.FinishedDate = remote.CompletedDate ?? (EasRules.IsActive(remote.Status) ? null : submission.FinishedDate ?? Now());
        submission.ErrorCode = Trim(remote.ErrorCode, EasSubmission.ShortMaxLength);
        submission.ErrorMessage = Trim(remote.ErrorMessage, 1000);

        return before != (submission.Status, submission.ErrorMessage);
    }

    private async Task<bool> AllowsAsync(int organizationId, OrgPermission permission, CancellationToken cancellationToken)
    {
        var access = _organizationAccess.Current ?? await _organizationAccess.GetAccessAsync(organizationId, _currentUser.GetRequiredId(), cancellationToken);

        return access?.Allows(permission) == true;
    }

    #endregion
}
