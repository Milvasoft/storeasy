using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Registries;
using System.Globalization;
using System.Text;

namespace Storeasy.Infrastructure.Registries;

/// <summary>
/// <see cref="IRegistryPlanService"/> (docs/package-registries.md). Releases are started in the repository's CI on GitHub
/// (a version bump commit, a tag or a workflow dispatch) and followed until the registries show the version; operations
/// (unlist, deprecate, dist-tag, yank) use the stored registry key. Every write comes from a plan a member approved with the
/// typed confirmation and the production release permission; nothing starts on its own.
/// </summary>
/// <param name="packageRepository"></param>
/// <param name="groupRepository"></param>
/// <param name="versionRepository"></param>
/// <param name="planRepository"></param>
/// <param name="connectionRepository"></param>
/// <param name="secretProtector"></param>
/// <param name="gitHub"></param>
/// <param name="writer"></param>
/// <param name="reader"></param>
/// <param name="storeJobQueue"></param>
/// <param name="organizationAccess"></param>
/// <param name="currentUser"></param>
/// <param name="auditTrail"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="options"></param>
/// <param name="featureFlagService"></param>
/// <param name="timeProvider"></param>
public sealed class RegistryPlanService(IStoreasyRepositoryBase<TrackedPackage> packageRepository,
                                        IStoreasyRepositoryBase<PackageGroup> groupRepository,
                                        IStoreasyRepositoryBase<TrackedPackageVersion> versionRepository,
                                        IStoreasyRepositoryBase<RegistryPlan> planRepository,
                                        IStoreasyRepositoryBase<RegistryConnection> connectionRepository,
                                        ISecretProtector secretProtector,
                                        IRegistryGitHub gitHub,
                                        IRegistryWriter writer,
                                        IPackageRegistryReader reader,
                                        IStoreJobQueue storeJobQueue,
                                        IOrganizationAccess organizationAccess,
                                        ICurrentUser currentUser,
                                        IAuditTrail auditTrail,
                                        INotificationDispatcher notificationDispatcher,
                                        RegistryOptions options,
                                        IFeatureFlagService featureFlagService,
                                        TimeProvider timeProvider) : IRegistryPlanService
{
    private const int _maxWorkflows = 40;

    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<PackageGroup> _groupRepository = groupRepository;
    private readonly IStoreasyRepositoryBase<TrackedPackageVersion> _versionRepository = versionRepository;
    private readonly IStoreasyRepositoryBase<RegistryPlan> _planRepository = planRepository;
    private readonly IStoreasyRepositoryBase<RegistryConnection> _connectionRepository = connectionRepository;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IRegistryGitHub _gitHub = gitHub;
    private readonly IRegistryWriter _writer = writer;
    private readonly IPackageRegistryReader _reader = reader;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;
    private readonly IOrganizationAccess _organizationAccess = organizationAccess;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly RegistryOptions _options = options;
    private readonly IFeatureFlagService _featureFlagService = featureFlagService;
    private readonly TimeProvider _timeProvider = timeProvider;

    private sealed record Scope(string Name, ReleaseSettings Settings, List<TrackedPackage> Packages, string CurrentVersion);

    #region Plans

    /// <inheritdoc/>
    public async Task<Response<RegistryPlanDto>> PlanReleaseAsync(int organizationId, int? trackedPackageId, int? packageGroupId, string version, CancellationToken cancellationToken)
    {
        var scope = await ScopeAsync(organizationId, trackedPackageId, packageGroupId, cancellationToken);

        if (scope == null)
            return Response<RegistryPlanDto>.Error(null, trackedPackageId != null ? MessageKey.TrackedPackageNotFound : MessageKey.PackageGroupNotFound);

        if (ReleaseRules.Problems(scope.Settings) is { Count: > 0 } problems)
            return Response<RegistryPlanDto>.Error(null, problems[0]);

        if (scope.Packages.Count == 0 || scope.Packages.Any(p => !ReleaseRules.IsValidVersion(p.Registry, version)))
            return Response<RegistryPlanDto>.Error(null, MessageKey.RegistryVersionInvalid);

        if (scope.CurrentVersion != null && PackageGroups.Compare(version, scope.CurrentVersion) <= 0)
            return Response<RegistryPlanDto>.Error(null, MessageKey.RegistryVersionNotHigher);

        var token = await OpenAsync(organizationId, RegistryConnectionKind.GitHub, cancellationToken);

        if (token == null)
            return Response<RegistryPlanDto>.Error(null, MessageKey.RegistryConnectionMissing);

        var settings = scope.Settings;
        var plan = new RegistryPlan
        {
            OrganizationId = organizationId,
            TrackedPackageId = trackedPackageId,
            PackageGroupId = packageGroupId,
            Kind = RegistryPlanKind.Release,
            Version = version,
            CurrentVersion = scope.CurrentVersion,
            Release = settings,
            PackageIds = [.. scope.Packages.Select(p => p.Id)],
            Title = Trim($"Release {scope.Name} {version}", RegistryPlan.TitleMaxLength),
        };

        try
        {
            plan.BaseSha = await _gitHub.GetBranchShaAsync(token, settings.Repository, settings.Branch, cancellationToken);

            if (settings.Trigger is ReleaseTrigger.VersionBump or ReleaseTrigger.VersionBumpAndTag)
            {
                var error = await ReadFilesAsync(token, plan, cancellationToken);

                if (error != null)
                    return Response<RegistryPlanDto>.Error(null, error);
            }

            if (settings.Trigger is ReleaseTrigger.Tag or ReleaseTrigger.VersionBumpAndTag)
            {
                plan.Tag = ReleaseRules.TagOf(settings.TagPattern, version);

                if (await _gitHub.TagExistsAsync(token, settings.Repository, plan.Tag, cancellationToken))
                    return Response<RegistryPlanDto>.Error(null, MessageKey.RegistryReleaseTagExists);
            }

            if (settings.Trigger == ReleaseTrigger.Dispatch)
                plan.DispatchInputs = ReleaseRules.DispatchInputs(settings, version, null);
        }
        catch (StoreCallException ex)
        {
            return Response<RegistryPlanDto>.Error(null, ex.StatusCode is 401 or 403 or 404 ? MessageKey.RegistryGitHubRefused : MessageKey.RegistryGitHubUnreachable);
        }

        return await SaveAsync(plan, $"{scope.Name} {version}", cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Response<RegistryPlanDto>> PlanOperationAsync(int organizationId, int trackedPackageId, RegistryPlanKind kind, string version, string argument, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == trackedPackageId && p.OrganizationId == organizationId,
                                                                      projection: p => new TrackedPackage { Id = p.Id, Name = p.Name, Registry = p.Registry },
                                                                      cancellationToken: cancellationToken);

        if (package == null)
            return Response<RegistryPlanDto>.Error(null, MessageKey.TrackedPackageNotFound);

        if (!RegistrySecrets.Supports(package.Registry, kind))
            return Response<RegistryPlanDto>.Error(null, MessageKey.RegistryOperationUnsupported);

        var known = await _versionRepository.GetFirstOrDefaultAsync(v => v.TrackedPackageId == package.Id && v.OrganizationId == organizationId && v.Version == version,
                                                                    projection: v => (long?)v.Id,
                                                                    cancellationToken: cancellationToken);

        if (!known.HasValue)
            return Response<RegistryPlanDto>.Error(null, MessageKey.RegistryVersionNotFound);

        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Kind == RegistrySecrets.KindOf(package.Registry),
                                                                            projection: c => (int?)c.Id,
                                                                            cancellationToken: cancellationToken);

        if (!connection.HasValue)
            return Response<RegistryPlanDto>.Error(null, MessageKey.RegistryConnectionMissing);

        var plan = new RegistryPlan
        {
            OrganizationId = organizationId,
            TrackedPackageId = package.Id,
            Kind = kind,
            Version = version,
            Argument = string.IsNullOrEmpty(argument) ? null : Trim(argument, TrackedPackage.MessageMaxLength),
            PackageIds = [package.Id],
            Title = Trim(kind == RegistryPlanKind.DistTag ? $"Point {package.Name} dist-tag {argument} at {version}" : $"{kind} {package.Name} {version}", RegistryPlan.TitleMaxLength),
        };

        return await SaveAsync(plan, $"{package.Name} {version}", cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Response<RegistryPlanDto>> ApproveAsync(int organizationId, long planId, string confirm, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetFirstOrDefaultAsync(p => p.Id == planId && p.OrganizationId == organizationId, cancellationToken: cancellationToken);
        var refusal = await DecisionRefusalAsync(organizationId, plan, cancellationToken);

        if (refusal != null)
            return Response<RegistryPlanDto>.Error(null, refusal);

        if (!ChangePlanRules.ConfirmationMatches(plan.ConfirmText, confirm))
            return Response<RegistryPlanDto>.Error(null, MessageKey.PlanConfirmMismatch);

        if (plan.Files.Count > 0 && await FilesChangedAsync(plan, cancellationToken))
            return Response<RegistryPlanDto>.Error(null, MessageKey.PlanConflict);

        plan.Status = ChangePlanStatus.Approved;
        plan.DecidedByUserName = _currentUser.UserName;
        plan.DecisionDate = _timeProvider.GetUtcNow().UtcDateTime;

        var jobs = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(organizationId, StoreJobKind.RegistryPlanApply, new RegistryPlanApplyPayload(plan.Id), MaxAttempts: 1)], cancellationToken);

        plan.StoreJobId = jobs[0];
        await _planRepository.UpdateAsync(plan, cancellationToken);
        await _auditTrail.WriteAsync(organizationId, AuditAction.RegistryPlanApproved, plan.Id.ToString(CultureInfo.InvariantCulture), $"{plan.Title} approved.", details: new { plan.Kind, plan.Version, plan.Tag }, cancellationToken: cancellationToken);

        return Response<RegistryPlanDto>.Success(RegistryPlanDto.From(plan));
    }

    /// <inheritdoc/>
    public async Task<Response<RegistryPlanDto>> RejectAsync(int organizationId, long planId, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetFirstOrDefaultAsync(p => p.Id == planId && p.OrganizationId == organizationId, cancellationToken: cancellationToken);
        var refusal = await DecisionRefusalAsync(organizationId, plan, cancellationToken);

        if (refusal != null)
            return Response<RegistryPlanDto>.Error(null, refusal);

        plan.Status = ChangePlanStatus.Rejected;
        plan.DecidedByUserName = _currentUser.UserName;
        plan.DecisionDate = _timeProvider.GetUtcNow().UtcDateTime;
        await _planRepository.UpdateAsync(plan, cancellationToken);
        await _auditTrail.WriteAsync(organizationId, AuditAction.RegistryPlanRejected, plan.Id.ToString(CultureInfo.InvariantCulture), $"{plan.Title} rejected.", cancellationToken: cancellationToken);

        return Response<RegistryPlanDto>.Success(RegistryPlanDto.From(plan));
    }

    /// <inheritdoc/>
    public async Task<Response<List<ReleaseSuggestionDto>>> DetectAsync(int organizationId, int? trackedPackageId, int? packageGroupId, string repository, CancellationToken cancellationToken)
    {
        var scope = await ScopeAsync(organizationId, trackedPackageId, packageGroupId, cancellationToken);

        if (scope == null)
            return Response<List<ReleaseSuggestionDto>>.Error(null, trackedPackageId != null ? MessageKey.TrackedPackageNotFound : MessageKey.PackageGroupNotFound);

        var first = scope.Packages.FirstOrDefault();
        repository ??= first?.SourceRepository == null ? null : PackageGroups.RepositoryKey(first.SourceRepository);

        if (string.IsNullOrEmpty(repository) || first == null)
            return Response<List<ReleaseSuggestionDto>>.Error(null, MessageKey.RegistrySetupRepositoryInvalid);

        var token = await OpenAsync(organizationId, RegistryConnectionKind.GitHub, cancellationToken);

        if (token == null)
            return Response<List<ReleaseSuggestionDto>>.Error(null, MessageKey.RegistryConnectionMissing);

        try
        {
            var facts = new List<WorkflowFacts>();

            foreach (var file in (await _gitHub.ListWorkflowFilesAsync(token, repository, null, cancellationToken)).Take(_maxWorkflows))
            {
                var workflow = await _gitHub.GetFileAsync(token, repository, $".github/workflows/{file}", null, cancellationToken);

                if (workflow != null)
                    facts.Add(ReleaseDetection.Analyze(file, workflow.Content));
            }

            var suggestions = ReleaseDetection.Suggest(repository, "master", facts, first.Registry, scope.Packages.Count == 1 ? first.Name : null);

            return Response<List<ReleaseSuggestionDto>>.Success([.. suggestions.Select(s => new ReleaseSuggestionDto { Settings = s.Settings, Reason = s.Reason })]);
        }
        catch (StoreCallException ex)
        {
            return Response<List<ReleaseSuggestionDto>>.Error(null, ex.StatusCode is 401 or 403 or 404 ? MessageKey.RegistryGitHubRefused : MessageKey.RegistryGitHubUnreachable);
        }
    }

    #endregion

    #region Apply

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ApplyAsync(long planId, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetFirstOrDefaultAsync(p => p.Id == planId && p.OrganizationId == context.OrganizationId, cancellationToken: cancellationToken)
                   ?? throw new StoreJobException("The plan was removed.", transient: false);

        if (plan.Status != ChangePlanStatus.Approved)
            throw new StoreJobException($"The plan is {plan.Status}, not approved.", transient: false);

        var step = await context.BeginStepAsync(plan.Kind == RegistryPlanKind.Release ? "registries.release" : "registries.operation", StepDirection.Outbound, plan.Title, cancellationToken: cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        _auditTrail.Channel = ActionChannel.System;

        try
        {
            if (plan.Kind == RegistryPlanKind.Release)
                await StartReleaseAsync(plan, cancellationToken);
            else
                await OperateAsync(plan, cancellationToken);

            plan.Status = ChangePlanStatus.Applied;
            plan.StartedDate = now;
        }
        catch (StoreCallException ex)
        {
            plan.Status = ChangePlanStatus.Failed;
            plan.Error = SecretRedactor.RedactAndTrim(ex.Message, RegistryPlan.ErrorMaxLength);
        }

        await _planRepository.UpdateAsync(plan, cancellationToken);
        await ReportAppliedAsync(plan, cancellationToken);
        await context.EndStepAsync(step, plan.Status == ChangePlanStatus.Applied ? StoreJobStepStatus.Succeeded : StoreJobStepStatus.Failed, plan.Error ?? plan.Title, cancellationToken);

        if (plan.Status == ChangePlanStatus.Failed)
            throw new StoreJobException(plan.Error, transient: false);

        return new StoreJobOutcome(plan.Kind == RegistryPlanKind.Release ? $"Release started: {plan.Title}." : $"Done: {plan.Title}.", new { plan.Id, plan.CommitSha, plan.Tag, plan.RunId, plan.RunUrl });
    }

    private async Task StartReleaseAsync(RegistryPlan plan, CancellationToken cancellationToken)
    {
        var settings = plan.Release;
        var token = await OpenAsync(plan.OrganizationId, RegistryConnectionKind.GitHub, cancellationToken)
                    ?? throw new StoreCallException("The GitHub token was removed.", null, transient: false);
        var sha = plan.BaseSha;

        if (plan.Files.Count > 0)
        {
            var files = new List<(string Path, string Sha, string Content)>();

            foreach (var change in plan.Files)
            {
                var file = await _gitHub.GetFileAsync(token, settings.Repository, change.Path, settings.Branch, cancellationToken);

                if (file == null || file.Sha != change.BaseSha)
                    throw new StoreCallException($"{change.Path} changed after the plan; plan the release again.", 409, transient: false);

                var format = settings.VersionFiles.First(f => f.Path == change.Path).Format;
                var location = ReleaseRules.Locate(file.Content, format) ?? throw new StoreCallException($"The version was not found in {change.Path}.", null, transient: false);

                files.Add((change.Path, file.Sha, ReleaseRules.Replace(file.Content, location, plan.Version)));
            }

            sha = await _gitHub.CommitFilesAsync(token, settings.Repository, settings.Branch, files, $"Release {plan.Version}", cancellationToken);
            plan.CommitSha = sha;
        }

        if (plan.Tag != null)
            await _gitHub.CreateTagAsync(token, settings.Repository, plan.Tag, sha, cancellationToken);

        if (settings.Trigger == ReleaseTrigger.Dispatch)
        {
            var run = await _gitHub.DispatchAsync(token, settings.Repository, settings.Workflow, settings.Branch, plan.DispatchInputs, cancellationToken);

            plan.RunId = run?.Id;
            plan.RunUrl = run?.HtmlUrl;
            plan.RunStatus = run?.Status;
        }

        plan.FollowStatus = ReleaseFollowStatus.Following;
    }

    private async Task OperateAsync(RegistryPlan plan, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == plan.TrackedPackageId && p.OrganizationId == plan.OrganizationId,
                                                                      projection: p => new TrackedPackage { Id = p.Id, Name = p.Name, Registry = p.Registry },
                                                                      cancellationToken: cancellationToken)
                      ?? throw new StoreCallException("The package is no longer tracked.", null, transient: false);
        var kind = RegistrySecrets.KindOf(package.Registry) ?? throw new StoreCallException("The registry has no write API in storeasy.", null, transient: false);
        var secret = await OpenAsync(plan.OrganizationId, kind, cancellationToken) ?? throw new StoreCallException("The registry key was removed.", null, transient: false);

        await _writer.WriteAsync(plan.Kind, package.Registry, package.Name, plan.Version, plan.Argument, secret, cancellationToken);
        await _storeJobQueue.EnqueueAsync([new StoreJobRequest(plan.OrganizationId, StoreJobKind.RegistrySync, new RegistrySyncPayload(package.Id))], cancellationToken);
    }

    private async Task ReportAppliedAsync(RegistryPlan plan, CancellationToken cancellationToken)
    {
        var failed = plan.Status == ChangePlanStatus.Failed;
        var action = failed ? AuditAction.RegistryReleaseFailed : plan.Kind == RegistryPlanKind.Release ? AuditAction.RegistryReleaseStarted : AuditAction.RegistryOperationApplied;

        await _auditTrail.WriteAsync(plan.OrganizationId, action, plan.Id.ToString(CultureInfo.InvariantCulture), failed ? $"{plan.Title} failed: {plan.Error}" : $"{plan.Title}.", details: new { plan.Kind, plan.Version, plan.Tag, plan.CommitSha, plan.RunUrl }, cancellationToken: cancellationToken);
        await NotifyAsync(plan, failed ? MessageKey.NotificationRegistryReleaseFailed : plan.Kind == RegistryPlanKind.Release ? MessageKey.NotificationRegistryReleaseStarted : MessageKey.NotificationRegistryOperationApplied, cancellationToken);
    }

    #endregion

    #region Follow

    /// <inheritdoc/>
    public async Task<int> FollowAsync(CancellationToken cancellationToken)
    {
        if (!await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.Registries, cancellationToken))
            return 0;

        var plans = await _planRepository.GetAllAsync(p => p.FollowStatus == ReleaseFollowStatus.Following, cancellationToken: cancellationToken);

        if (plans.Count == 0)
            return 0;

        var packageIds = plans.SelectMany(p => p.PackageIds).Distinct().ToList();
        var packages = (await _packageRepository.GetAllAsync(p => packageIds.Contains(p.Id), projection: p => new TrackedPackage { Id = p.Id, OrganizationId = p.OrganizationId, Name = p.Name, Registry = p.Registry }, cancellationToken: cancellationToken))
                       .ToDictionary(p => p.Id);
        var tokens = await TokensAsync([.. plans.Select(p => p.OrganizationId).Distinct()], cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = new List<(RegistryPlan Plan, AuditAction Action)>();

        foreach (var plan in plans)
        {
            var action = await FollowOneAsync(plan, tokens.GetValueOrDefault(plan.OrganizationId), [.. plan.PackageIds.Where(packages.ContainsKey).Select(id => packages[id])], now, cancellationToken);

            if (action is AuditAction value)
                changed.Add((plan, value));
        }

        await _planRepository.UpdateAsync(plans, cancellationToken);

        if (changed.Count == 0)
            return 0;

        _auditTrail.Channel = ActionChannel.System;

        await _auditTrail.WriteManyAsync([.. changed.Select(c => new AuditEntry(c.Plan.OrganizationId, c.Action, c.Plan.Id.ToString(CultureInfo.InvariantCulture),
                                                                                 $"{c.Plan.Title}: {c.Plan.FollowStatus} ({c.Plan.RunConclusion ?? c.Plan.RunStatus ?? "no run"}).",
                                                                                 Details: new { c.Plan.RunUrl, c.Plan.RunConclusion, c.Plan.FollowStatus },
                                                                                 UserName: "system",
                                                                                 Channel: ActionChannel.System))],
                                         cancellationToken);

        foreach (var (plan, action) in changed)
        {
            var key = action switch
            {
                AuditAction.RegistryReleasePublished => MessageKey.NotificationRegistryReleasePublished,
                AuditAction.RegistryReleaseFailed => MessageKey.NotificationRegistryReleaseFailed,
                _ => MessageKey.NotificationRegistryReleaseRunFinished,
            };

            await NotifyAsync(plan, key, cancellationToken);
        }

        var organizations = changed.Where(c => c.Action == AuditAction.RegistryReleasePublished).Select(c => c.Plan.OrganizationId).Distinct().ToList();

        if (organizations.Count > 0)
            await _storeJobQueue.EnqueueAsync([.. organizations.Select(o => new StoreJobRequest(o, StoreJobKind.RegistrySync, new RegistrySyncPayload()))], cancellationToken);

        return changed.Count;
    }

    /// <summary>
    /// One step of following a release: the CI run (found by the bump commit or the tagged commit, or named by the
    /// dispatch), then the registries. Returns the audit action of a change worth reporting.
    /// </summary>
    private async Task<AuditAction?> FollowOneAsync(RegistryPlan plan, string token, List<TrackedPackage> packages, DateTime now, CancellationToken cancellationToken)
    {
        var settings = plan.Release;
        var wasCompleted = plan.RunStatus == "completed";

        try
        {
            if (token != null)
            {
                var run = plan.RunId is long runId
                    ? await _gitHub.GetRunAsync(token, settings.Repository, runId, cancellationToken)
                    : (await _gitHub.ListRunsAsync(token, settings.Repository, plan.CommitSha ?? plan.BaseSha, cancellationToken))
                      .FirstOrDefault(r => string.IsNullOrEmpty(settings.Workflow) || r.Path?.EndsWith(settings.Workflow, StringComparison.OrdinalIgnoreCase) == true);

                if (run != null)
                {
                    plan.RunId = run.Id;
                    plan.RunUrl = run.HtmlUrl ?? plan.RunUrl;
                    plan.RunStatus = run.Status;
                    plan.RunConclusion = run.Conclusion;
                }
            }

            if (plan.RunStatus == "completed" && plan.RunConclusion is not ("success" or "skipped" or "neutral"))
                return Finish(plan, ReleaseFollowStatus.Failed, now, $"The CI run ended with {plan.RunConclusion}.");

            var published = 0;

            foreach (var package in packages)
            {
                var read = await _reader.ReadPackageAsync(package.Registry, package.Name, cancellationToken);

                if (read?.Versions.Any(v => v.Version == plan.Version) == true)
                    published++;
            }

            if (packages.Count > 0 && published == packages.Count)
                return Finish(plan, ReleaseFollowStatus.Published, now, null);
        }
        catch (StoreCallException)
        {
            // A registry or GitHub that does not answer now is asked again on the next run.
        }

        if (plan.StartedDate is DateTime started && started.AddHours(Math.Max(1, _options.ReleaseFollowHours)) < now)
            return Finish(plan, ReleaseFollowStatus.TimedOut, now, "The version did not appear within the follow window.");

        return !wasCompleted && plan.RunStatus == "completed" ? AuditAction.RegistryReleaseRunFinished : null;
    }

    private static AuditAction Finish(RegistryPlan plan, ReleaseFollowStatus status, DateTime now, string error)
    {
        plan.FollowStatus = status;
        plan.FinishedDate = now;
        plan.Error = error;

        return status == ReleaseFollowStatus.Published ? AuditAction.RegistryReleasePublished : AuditAction.RegistryReleaseFailed;
    }

    #endregion

    #region Helpers

    private async Task<Scope> ScopeAsync(int organizationId, int? trackedPackageId, int? packageGroupId, CancellationToken cancellationToken)
    {
        if (trackedPackageId is int packageId)
        {
            var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == packageId && p.OrganizationId == organizationId,
                                                                          projection: p => new TrackedPackage { Id = p.Id, Name = p.Name, Registry = p.Registry, LatestVersion = p.LatestVersion, SourceRepository = p.SourceRepository, Release = p.Release, PackageGroup = p.PackageGroup == null ? null : new PackageGroup { Release = p.PackageGroup.Release } },
                                                                          cancellationToken: cancellationToken);

            return package == null ? null : new Scope(package.Name, package.Release ?? package.PackageGroup?.Release, [package], package.LatestVersion);
        }

        var group = await _groupRepository.GetFirstOrDefaultAsync(g => g.Id == packageGroupId && g.OrganizationId == organizationId,
                                                                  projection: g => new PackageGroup
                                                                  {
                                                                      Name = g.Name,
                                                                      Release = g.Release,
                                                                      Packages = g.Packages.Select(p => new TrackedPackage { Id = p.Id, Name = p.Name, Registry = p.Registry, LatestVersion = p.LatestVersion, SourceRepository = p.SourceRepository }).ToList(),
                                                                  },
                                                                  cancellationToken: cancellationToken);

        return group == null ? null : new Scope(group.Name, group.Release, group.Packages, PackageGroups.Highest(group.Packages.Select(p => p.LatestVersion)));
    }

    /// <summary>
    /// Reads every version file of the plan's settings at the branch and records the change; a message key when a file or
    /// its version is missing.
    /// </summary>
    private async Task<string> ReadFilesAsync(string token, RegistryPlan plan, CancellationToken cancellationToken)
    {
        foreach (var setting in plan.Release.VersionFiles)
        {
            var file = await _gitHub.GetFileAsync(token, plan.Release.Repository, setting.Path, plan.Release.Branch, cancellationToken);

            if (file == null)
                return MessageKey.RegistryReleaseFileMissing;

            var location = ReleaseRules.Locate(file.Content, setting.Format);

            if (location == null)
                return MessageKey.RegistryReleaseVersionNotFound;

            var after = ReleaseRules.Replace(file.Content, location, plan.Version);

            plan.Files.Add(new ReleaseFileChange
            {
                Path = setting.Path,
                BaseSha = file.Sha,
                OldVersion = location.Version,
                Line = location.Line,
                Before = ReleaseRules.LineAt(file.Content, location.Index),
                After = ReleaseRules.LineAt(after, location.Index),
            });
        }

        return null;
    }

    private async Task<bool> FilesChangedAsync(RegistryPlan plan, CancellationToken cancellationToken)
    {
        var token = await OpenAsync(plan.OrganizationId, RegistryConnectionKind.GitHub, cancellationToken);

        if (token == null)
            return true;

        try
        {
            foreach (var change in plan.Files)
            {
                var file = await _gitHub.GetFileAsync(token, plan.Release.Repository, change.Path, plan.Release.Branch, cancellationToken);

                if (file?.Sha != change.BaseSha)
                    return true;
            }

            return false;
        }
        catch (StoreCallException)
        {
            return true;
        }
    }

    private async Task<Response<RegistryPlanDto>> SaveAsync(RegistryPlan plan, string confirm, CancellationToken cancellationToken)
    {
        plan.ConfirmText = Trim(confirm, RegistryPlan.ShortMaxLength);
        plan.RequiredPermission = OrgPermission.ReleaseProduction;
        plan.ExpiryDate = _timeProvider.GetUtcNow().UtcDateTime.AddHours(Math.Max(1, _options.PlanLifetimeHours));
        plan.CreatedByUserName = _currentUser.UserName;

        await _planRepository.AddAsync(plan, cancellationToken);
        await _auditTrail.WriteAsync(plan.OrganizationId, AuditAction.RegistryPlanCreated, plan.Id.ToString(CultureInfo.InvariantCulture), $"{plan.Title} planned.", details: new { plan.Kind, plan.Version, plan.Tag, Files = plan.Files.Select(f => f.Path) }, cancellationToken: cancellationToken);

        return Response<RegistryPlanDto>.Success(RegistryPlanDto.From(plan));
    }

    private async Task<string> DecisionRefusalAsync(int organizationId, RegistryPlan plan, CancellationToken cancellationToken)
    {
        if (plan == null)
            return MessageKey.RegistryPlanNotFound;

        if (plan.Status != ChangePlanStatus.Pending)
            return MessageKey.PlanNotPending;

        if (plan.ExpiryDate <= _timeProvider.GetUtcNow().UtcDateTime)
        {
            plan.Status = ChangePlanStatus.Expired;
            await _planRepository.UpdateAsync(plan, cancellationToken);

            return MessageKey.PlanExpired;
        }

        var access = _organizationAccess.Current ?? await _organizationAccess.GetAccessAsync(organizationId, _currentUser.GetRequiredId(), cancellationToken);

        return access?.Allows(plan.RequiredPermission) == true ? null : MessageKey.OrganizationForbidden;
    }

    private async Task<string> OpenAsync(int organizationId, RegistryConnectionKind kind, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Kind == kind,
                                                                            projection: c => new RegistryConnection { EncryptedSecret = c.EncryptedSecret, EncryptedDataKey = c.EncryptedDataKey, KeyVersion = c.KeyVersion },
                                                                            cancellationToken: cancellationToken);

        return connection == null ? null : Open(organizationId, kind, connection);
    }

    private async Task<Dictionary<int, string>> TokensAsync(List<int> organizations, CancellationToken cancellationToken)
    {
        var connections = await _connectionRepository.GetAllAsync(c => organizations.Contains(c.OrganizationId) && c.Kind == RegistryConnectionKind.GitHub, cancellationToken: cancellationToken);

        return connections.ToDictionary(c => c.OrganizationId, c => Open(c.OrganizationId, RegistryConnectionKind.GitHub, c));
    }

    private string Open(int organizationId, RegistryConnectionKind kind, RegistryConnection connection)
        => Encoding.UTF8.GetString(_secretProtector.Unprotect(new ProtectedSecret(connection.EncryptedSecret, connection.EncryptedDataKey, connection.KeyVersion), RegistrySecrets.Context(organizationId, kind)));

    private async Task NotifyAsync(RegistryPlan plan, string key, CancellationToken cancellationToken)
        => await _notificationDispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = plan.OrganizationId,
            Kind = NotificationType.PackageRegistry,
            Roles = NotificationAudiences.Editors,
            TextKey = key,
            TextArgs = [plan.Title, plan.RunUrl ?? string.Empty, plan.Error ?? string.Empty],
            Data = new { registryPlanId = plan.Id, plan.Version, followStatus = plan.FollowStatus?.ToString(), plan.RunUrl },
            RelatedEntity = NotificationEntity.TrackedPackage,
            RelatedEntityId = $"plan:{plan.Id}:{key}",
            ActionLink = plan.TrackedPackageId is int packageId ? $"/registries/{packageId}" : "/registries/groups",
        }, cancellationToken);

    private static string Trim(string value, int max) => value == null || value.Length <= max ? value : value[..max];

    #endregion
}
