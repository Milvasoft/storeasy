using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.EasDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Eas;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Eas;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Registries;
using System.Globalization;
using System.Text;

namespace Storeasy.Infrastructure.Eas;

/// <summary>
/// <see cref="IEasService"/> (docs/eas-builds.md). Builds are started only on a member's request with the typed
/// confirmation: Expo builds from the GitHub repository linked to the project on expo.dev (<c>createGitHubBuild</c>), so no
/// archive is uploaded. The schedule then follows the request and the build; a finished package can be handed to the
/// build intake, whose delivery rule takes it to a test channel as for any other upload.
/// </summary>
/// <param name="connectionRepository"></param>
/// <param name="linkRepository"></param>
/// <param name="buildRepository"></param>
/// <param name="appRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="registryConnectionRepository"></param>
/// <param name="secretProtector"></param>
/// <param name="expo"></param>
/// <param name="gitHub"></param>
/// <param name="storeJobQueue"></param>
/// <param name="intakeService"></param>
/// <param name="auditTrail"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="currentUser"></param>
/// <param name="localizer"></param>
/// <param name="featureFlagService"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
/// <param name="submissionRepository"></param>
/// <param name="storeBuildRepository"></param>
/// <param name="sessionRepository"></param>
/// <param name="contentService"></param>
/// <param name="changePlanService"></param>
/// <param name="organizationAccess"></param>
public sealed partial class EasService(IStoreasyRepositoryBase<ExpoConnection> connectionRepository,
                               IStoreasyRepositoryBase<EasProjectLink> linkRepository,
                               IStoreasyRepositoryBase<EasBuild> buildRepository,
                               IStoreasyRepositoryBase<App> appRepository,
                               IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                               IStoreasyRepositoryBase<RegistryConnection> registryConnectionRepository,
                               ISecretProtector secretProtector,
                               IExpoApi expo,
                               IRegistryGitHub gitHub,
                               IStoreJobQueue storeJobQueue,
                               IArtifactIntakeService intakeService,
                               IAuditTrail auditTrail,
                               INotificationDispatcher notificationDispatcher,
                               ICurrentUser currentUser,
                               IMilvaLocalizer localizer,
                               IFeatureFlagService featureFlagService,
                               EasOptions options,
                               TimeProvider timeProvider,
                               IStoreasyRepositoryBase<EasSubmission> submissionRepository,
                               IStoreasyRepositoryBase<Build> storeBuildRepository,
                               IStoreasyRepositoryBase<ArtifactSession> sessionRepository,
                               IStoreContentService contentService,
                               IChangePlanService changePlanService,
                               IOrganizationAccess organizationAccess) : IEasService
{
    private const int _listedBuilds = 50;

    private readonly IStoreasyRepositoryBase<ExpoConnection> _connectionRepository = connectionRepository;
    private readonly IStoreasyRepositoryBase<EasProjectLink> _linkRepository = linkRepository;
    private readonly IStoreasyRepositoryBase<EasBuild> _buildRepository = buildRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<RegistryConnection> _registryConnectionRepository = registryConnectionRepository;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IExpoApi _expo = expo;
    private readonly IRegistryGitHub _gitHub = gitHub;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;
    private readonly IArtifactIntakeService _intakeService = intakeService;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IMilvaLocalizer _localizer = localizer;
    private readonly IFeatureFlagService _featureFlagService = featureFlagService;
    private readonly EasOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IStoreasyRepositoryBase<EasSubmission> _submissionRepository = submissionRepository;
    private readonly IStoreasyRepositoryBase<Build> _storeBuildRepository = storeBuildRepository;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IChangePlanService _changePlanService = changePlanService;
    private readonly IOrganizationAccess _organizationAccess = organizationAccess;

    #region Connection

    /// <inheritdoc/>
    public async Task<Response<ExpoConnectionDto>> GetConnectionAsync(int organizationId, CancellationToken cancellationToken)
        => Response<ExpoConnectionDto>.Success(await _connectionRepository.GetFirstOrDefaultAsync(c => c.OrganizationId == organizationId, projection: ExpoConnectionDto.Projection, cancellationToken: cancellationToken));

    /// <inheritdoc/>
    public async Task<Response<ExpoConnectionDto>> SaveConnectionAsync(int organizationId, string token, CancellationToken cancellationToken)
    {
        token = token.Trim();

        ExpoActor actor;

        try
        {
            actor = await _expo.GetActorAsync(token, cancellationToken);
        }
        catch (StoreCallException ex)
        {
            return Response<ExpoConnectionDto>.Error(null, ex.Transient ? MessageKey.ExpoUnreachable : MessageKey.ExpoTokenRejected);
        }

        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.OrganizationId == organizationId, cancellationToken: cancellationToken)
                         ?? new ExpoConnection { OrganizationId = organizationId };
        var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(token), EasRules.Context(organizationId));

        connection.EncryptedSecret = sealedSecret.EncryptedSecret;
        connection.EncryptedDataKey = sealedSecret.EncryptedDataKey;
        connection.KeyVersion = sealedSecret.KeyVersion;
        connection.Fingerprint = Trim($"{actor.DisplayName} ({(actor.Kind == "Robot" ? "robot" : "user")})", ExpoConnection.FingerprintMaxLength);
        connection.Status = ConnectionStatus.Verified;
        connection.LastVerifiedDate = Now();
        connection.LastVerifyError = null;

        if (connection.Id == 0)
            await _connectionRepository.AddAsync(connection, cancellationToken);
        else
            await _connectionRepository.UpdateAsync(connection, cancellationToken);

        await _auditTrail.WriteAsync(organizationId, AuditAction.ExpoConnectionSaved, connection.Id.ToString(CultureInfo.InvariantCulture), $"Expo connection saved ({connection.Fingerprint}).", details: new { connection.Fingerprint }, cancellationToken: cancellationToken);

        return Response<ExpoConnectionDto>.Success(ExpoConnectionDto.Projection.Compile()(connection));
    }

    /// <inheritdoc/>
    public async Task<Response<int>> RemoveConnectionAsync(int organizationId, CancellationToken cancellationToken)
    {
        var removed = await _connectionRepository.ExecuteDeleteAsync(c => c.OrganizationId == organizationId, cancellationToken: cancellationToken);

        if (removed == 0)
            return Response<int>.Error(0, MessageKey.ExpoConnectionMissing);

        await _auditTrail.WriteAsync(organizationId, AuditAction.ExpoConnectionRemoved, organizationId.ToString(CultureInfo.InvariantCulture), "Expo connection removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(removed);
    }

    #endregion

    #region Projects

    /// <inheritdoc/>
    public async Task<Response<List<EasProjectDto>>> ListProjectsAsync(int organizationId, int? appId, CancellationToken cancellationToken)
    {
        var token = await OpenAsync(organizationId, cancellationToken);

        if (token == null)
            return Response<List<EasProjectDto>>.Error(null, MessageKey.ExpoConnectionMissing);

        var app = appId is int id ? await _appRepository.GetFirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == organizationId, projection: a => new App { Id = a.Id, Name = a.Name }, cancellationToken: cancellationToken) : null;

        if (appId != null && app == null)
            return Response<List<EasProjectDto>>.Error(null, MessageKey.AppNotFound);

        var identifiers = app == null
            ? []
            : (await _storeAppRepository.GetAllAsync(s => s.AppId == app.Id && s.OrganizationId == organizationId, projection: s => new StoreApp { BundleId = s.BundleId, ExternalId = s.ExternalId }, cancellationToken: cancellationToken))
              .SelectMany(s => new[] { s.BundleId, s.ExternalId }).Where(i => !string.IsNullOrEmpty(i)).ToList();
        var links = (await _linkRepository.GetAllAsync(l => l.OrganizationId == organizationId, projection: l => new EasProjectLink { AppId = l.AppId, ProjectId = l.ProjectId }, cancellationToken: cancellationToken))
                    .GroupBy(l => l.ProjectId).ToDictionary(g => g.Key, g => g.First().AppId);

        try
        {
            var projects = await _expo.ListProjectsAsync(token, cancellationToken);

            return Response<List<EasProjectDto>>.Success([.. projects.Select(p => new EasProjectDto
            {
                Id = p.Id,
                AccountName = p.AccountName,
                Slug = p.Slug,
                Name = p.Name,
                GitHubRepository = p.GitHubRepository,
                AppIdentifier = p.AppIdentifier,
                Match = app == null ? 0 : EasRules.MatchScore(app.Name, identifiers, p.Slug, p.Name, p.AppIdentifier),
                LinkedAppId = links.TryGetValue(p.Id, out var linked) ? linked : null,
            }).OrderByDescending(p => p.Match).ThenBy(p => p.AccountName, StringComparer.Ordinal).ThenBy(p => p.Slug, StringComparer.Ordinal)]);
        }
        catch (StoreCallException ex)
        {
            return Response<List<EasProjectDto>>.Error(null, Refusal(ex));
        }
    }

    /// <inheritdoc/>
    public async Task<Response<EasAppDto>> LinkProjectAsync(int organizationId, int appId, string projectId, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == organizationId, projection: a => new App { Id = a.Id, Name = a.Name }, cancellationToken: cancellationToken);

        if (app == null)
            return Response<EasAppDto>.Error(null, MessageKey.AppNotFound);

        var link = await _linkRepository.GetFirstOrDefaultAsync(l => l.AppId == appId && l.OrganizationId == organizationId, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(projectId))
        {
            if (link != null)
                await _linkRepository.ExecuteDeleteAsync(l => l.Id == link.Id && l.OrganizationId == organizationId, cancellationToken: cancellationToken);

            await _auditTrail.WriteAsync(organizationId, AuditAction.EasProjectLinked, appId.ToString(CultureInfo.InvariantCulture), $"{app.Name} unlinked from its EAS project.", cancellationToken: cancellationToken);

            return await GetAppAsync(organizationId, appId, cancellationToken);
        }

        var token = await OpenAsync(organizationId, cancellationToken);

        if (token == null)
            return Response<EasAppDto>.Error(null, MessageKey.ExpoConnectionMissing);

        ExpoProject project;

        try
        {
            project = await _expo.GetProjectAsync(token, projectId.Trim(), cancellationToken);
        }
        catch (StoreCallException ex)
        {
            return Response<EasAppDto>.Error(null, ex.Transient ? MessageKey.ExpoUnreachable : MessageKey.EasProjectNotFound);
        }

        if (project == null)
            return Response<EasAppDto>.Error(null, MessageKey.EasProjectNotFound);

        link ??= new EasProjectLink { OrganizationId = organizationId, AppId = appId };
        Apply(link, project);

        if (link.Id == 0)
            await _linkRepository.AddAsync(link, cancellationToken);
        else
            await _linkRepository.UpdateAsync(link, cancellationToken);

        await _auditTrail.WriteAsync(organizationId, AuditAction.EasProjectLinked, appId.ToString(CultureInfo.InvariantCulture), $"{app.Name} linked to the EAS project @{project.AccountName}/{project.Slug}.", details: new { project.Id, project.AccountName, project.Slug }, cancellationToken: cancellationToken);
        await _storeJobQueue.EnqueueAsync([new StoreJobRequest(organizationId, StoreJobKind.EasSync, new EasSyncPayload(appId), AppId: appId)], cancellationToken);

        return await GetAppAsync(organizationId, appId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Response<EasAppDto>> GetAppAsync(int organizationId, int appId, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == organizationId, projection: a => new App { Id = a.Id, Name = a.Name }, cancellationToken: cancellationToken);

        if (app == null)
            return Response<EasAppDto>.Error(null, MessageKey.AppNotFound);

        var link = await _linkRepository.GetFirstOrDefaultAsync(l => l.AppId == appId && l.OrganizationId == organizationId, cancellationToken: cancellationToken);
        var connected = await _connectionRepository.GetFirstOrDefaultAsync(c => c.OrganizationId == organizationId, projection: c => (int?)c.Id, cancellationToken: cancellationToken);
        var builds = (await _buildRepository.GetAllAsync(b => b.AppId == appId && b.OrganizationId == organizationId, projection: EasBuildDto.Projection, cancellationToken: cancellationToken))
                     .OrderByDescending(b => b.StartedDate ?? DateTime.MinValue).ThenByDescending(b => b.Id).Take(_listedBuilds).ToList();
        var submissions = (await ListSubmissionsAsync(organizationId, appId, cancellationToken)).Data;

        return Response<EasAppDto>.Success(new EasAppDto
        {
            AppId = app.Id,
            AppName = app.Name,
            Connected = connected.HasValue,
            Link = link == null ? null : new EasProjectLinkDto
            {
                ProjectId = link.ProjectId,
                AccountName = link.AccountName,
                Slug = link.Slug,
                GitHubRepository = link.GitHubRepository,
                DefaultBranch = link.DefaultBranch,
                ProjectUrl = $"{_options.WebsiteUrl.TrimEnd('/')}/accounts/{Uri.EscapeDataString(link.AccountName ?? string.Empty)}/projects/{Uri.EscapeDataString(link.Slug ?? string.Empty)}",
                GitHubSettingsUrl = EasRules.GitHubSettingsUrl(_options.WebsiteUrl, link.AccountName ?? string.Empty, link.Slug ?? string.Empty),
                SyncedDate = link.SyncedDate,
                SyncError = link.SyncError,
                Chain = new EasChainDto { AfterBuild = link.AfterBuild, PrepareReviewPlan = link.PrepareReviewPlan, SubmitTrack = link.SubmitTrack, SubmitReleaseStatus = link.SubmitReleaseStatus },
            },
            Builds = builds,
            Submissions = submissions,
        });
    }

    /// <inheritdoc/>
    public async Task<Response<EasProfilesDto>> GetProfilesAsync(int organizationId, int appId, string gitRef, CancellationToken cancellationToken)
    {
        var link = await _linkRepository.GetFirstOrDefaultAsync(l => l.AppId == appId && l.OrganizationId == organizationId, cancellationToken: cancellationToken);

        if (link == null)
            return Response<EasProfilesDto>.Error(null, MessageKey.EasProjectNotLinked);

        var defaults = new EasProfilesDto { Profiles = [.. EasRules.DefaultProfiles] };

        if (link.GitHubRepository == null)
            return Response<EasProfilesDto>.Success(With(defaults, "The project has no GitHub repository on expo.dev."));

        var token = await OpenGitHubAsync(organizationId, cancellationToken);

        if (token == null)
            return Response<EasProfilesDto>.Success(With(defaults, "Add a GitHub token under Package registries to read eas.json."));

        try
        {
            var file = await _gitHub.GetFileAsync(token, link.GitHubRepository, "eas.json", EasRules.IsGitRef(gitRef) ? gitRef : link.DefaultBranch, cancellationToken);
            var profiles = file == null ? null : EasRules.ProfilesOf(file.Content);

            return Response<EasProfilesDto>.Success(profiles is { Count: > 0 }
                ? new EasProfilesDto { Profiles = profiles, FromRepository = true }
                : With(defaults, "eas.json was not found at the root of the repository."));
        }
        catch (StoreCallException)
        {
            return Response<EasProfilesDto>.Success(With(defaults, "GitHub did not return eas.json."));
        }
    }

    #endregion

    #region Builds

    /// <inheritdoc/>
    public async Task<Response<List<EasBuildDto>>> StartBuildAsync(int organizationId, int appId, EasPlatform platform, string profile, string gitRef, EasChainDto chain, string confirm, CancellationToken cancellationToken)
    {
        profile = profile?.Trim();
        gitRef = gitRef?.Trim();

        if (EasRules.StartProblem(platform, profile, gitRef) is string problem)
            return Response<List<EasBuildDto>>.Error(null, problem);

        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == organizationId, projection: a => new App { Id = a.Id, Name = a.Name }, cancellationToken: cancellationToken);

        if (app == null)
            return Response<List<EasBuildDto>>.Error(null, MessageKey.AppNotFound);

        if (!EasRules.ConfirmationMatches(EasRules.ConfirmText(app.Name, platform), confirm))
            return Response<List<EasBuildDto>>.Error(null, MessageKey.PlanConfirmMismatch);

        var link = await _linkRepository.GetFirstOrDefaultAsync(l => l.AppId == appId && l.OrganizationId == organizationId, cancellationToken: cancellationToken);

        if (link == null)
            return Response<List<EasBuildDto>>.Error(null, MessageKey.EasProjectNotLinked);

        chain ??= new EasChainDto { AfterBuild = link.AfterBuild, PrepareReviewPlan = link.PrepareReviewPlan, SubmitTrack = link.SubmitTrack, SubmitReleaseStatus = link.SubmitReleaseStatus };
        chain.SubmitTrack = chain.AfterBuild == EasAfterBuild.Submit ? chain.SubmitTrack?.Trim() ?? "internal" : null;
        chain.SubmitReleaseStatus = chain.AfterBuild == EasAfterBuild.Submit ? chain.SubmitReleaseStatus?.Trim() ?? "COMPLETED" : null;

        if (EasRules.ChainProblem(chain.AfterBuild, chain.SubmitTrack, chain.SubmitReleaseStatus) is string chainProblem)
            return Response<List<EasBuildDto>>.Error(null, chainProblem);

        if (chain.AfterBuild == EasAfterBuild.Submit && EasRules.IsProductionSubmit(platform, chain.SubmitTrack) && !await AllowsAsync(organizationId, OrgPermission.ReleaseProduction, cancellationToken))
            return Response<List<EasBuildDto>>.Error(null, MessageKey.OrganizationForbidden);

        var token = await OpenAsync(organizationId, cancellationToken);

        if (token == null)
            return Response<List<EasBuildDto>>.Error(null, MessageKey.ExpoConnectionMissing);

        var builds = new List<EasBuild>();

        try
        {
            // The repository may have been connected on expo.dev after the link: read the project again.
            if (link.GitHubRepository == null && await _expo.GetProjectAsync(token, link.ProjectId, cancellationToken) is ExpoProject project)
            {
                Apply(link, project);
                await _linkRepository.UpdateAsync(link, cancellationToken);
            }

            if (link.GitHubRepository == null)
            {
                var url = EasRules.GitHubSettingsUrl(_options.WebsiteUrl, link.AccountName ?? string.Empty, link.Slug ?? string.Empty);

                return Response<List<EasBuildDto>>.Error(null, new ResponseMessage(MessageKey.EasGitHubNotLinked, _localizer[MessageKey.EasGitHubNotLinked, url]));
            }

            foreach (var single in EasRules.Expand(platform))
            {
                var receipt = await _expo.StartGitHubBuildAsync(token, link.ProjectId, single, profile, gitRef, cancellationToken);

                var requested = Requested(organizationId, link, single, profile, gitRef, receipt);

                requested.AfterBuild = chain.AfterBuild;
                requested.PrepareReviewPlan = chain.PrepareReviewPlan;
                requested.SubmitTrack = single == EasPlatform.Android ? chain.SubmitTrack : null;
                requested.SubmitReleaseStatus = single == EasPlatform.Android ? chain.SubmitReleaseStatus : null;
                requested.ReviewPlanDone = !chain.PrepareReviewPlan;
                builds.Add(requested);
            }
        }
        catch (StoreCallException ex) when (builds.Count == 0)
        {
            return Response<List<EasBuildDto>>.Error(null, new ResponseMessage(MessageKey.EasStartRefused, _localizer[MessageKey.EasStartRefused, SecretRedactor.RedactAndTrim(ex.Message, EasBuild.ErrorMaxLength)]));
        }
        catch (StoreCallException)
        {
            // The first platform was asked for; the second one failed and is reported with the result.
        }

        await _buildRepository.AddRangeAsync(builds, cancellationToken);
        await _auditTrail.WriteManyAsync([.. builds.Select(b => new AuditEntry(organizationId, AuditAction.EasBuildStarted, b.Id.ToString(CultureInfo.InvariantCulture),
                                                                                $"EAS build of {app.Name} ({b.Platform}, {b.Profile}, {b.GitRef}) requested from GitHub.",
                                                                                Details: new { b.ProjectId, b.Platform, b.Profile, b.GitRef, b.ReceiptId, b.AfterBuild, b.PrepareReviewPlan, b.SubmitTrack }))],
                                         cancellationToken);

        return Response<List<EasBuildDto>>.Success([.. builds.Select(EasBuildDto.From)]);
    }

    /// <inheritdoc/>
    public async Task<Response<EasBuildDto>> GetBuildAsync(int organizationId, long id, CancellationToken cancellationToken)
    {
        var build = await _buildRepository.GetFirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == organizationId, projection: EasBuildDto.Projection, cancellationToken: cancellationToken);

        return build == null ? Response<EasBuildDto>.Error(null, MessageKey.EasBuildNotFound) : Response<EasBuildDto>.Success(build);
    }

    /// <inheritdoc/>
    public async Task<Response<EasBuildDto>> CancelBuildAsync(int organizationId, long id, CancellationToken cancellationToken)
    {
        var build = await _buildRepository.GetFirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == organizationId, cancellationToken: cancellationToken);

        if (build == null)
            return Response<EasBuildDto>.Error(null, MessageKey.EasBuildNotFound);

        if (build.BuildId == null || !EasRules.IsCancelable(build.Status))
            return Response<EasBuildDto>.Error(null, MessageKey.EasBuildNotCancelable);

        var token = await OpenAsync(organizationId, cancellationToken);

        if (token == null)
            return Response<EasBuildDto>.Error(null, MessageKey.ExpoConnectionMissing);

        try
        {
            build.Status = await _expo.CancelBuildAsync(token, build.BuildId, cancellationToken);
        }
        catch (StoreCallException ex)
        {
            return Response<EasBuildDto>.Error(null, Refusal(ex));
        }

        if (build.Status == EasBuildStatus.Canceled)
        {
            build.FinishedDate ??= Now();
            build.EndReported = true;
        }

        await _buildRepository.UpdateAsync(build, cancellationToken);
        await _auditTrail.WriteAsync(organizationId, AuditAction.EasBuildCanceled, build.Id.ToString(CultureInfo.InvariantCulture), $"EAS build {build.BuildId} ({build.Platform}) canceled.", details: new { build.BuildId, build.Status }, cancellationToken: cancellationToken);

        return Response<EasBuildDto>.Success(EasBuildDto.From(build));
    }

    /// <inheritdoc/>
    public async Task<Response<EasBuildDto>> SendToIntakeAsync(int organizationId, long id, CancellationToken cancellationToken)
    {
        var build = await _buildRepository.GetFirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == organizationId, cancellationToken: cancellationToken);

        if (build == null)
            return Response<EasBuildDto>.Error(null, MessageKey.EasBuildNotFound);

        var error = await SendToIntakeCoreAsync(build, cancellationToken);

        return error == null ? Response<EasBuildDto>.Success(EasBuildDto.From(build)) : Response<EasBuildDto>.Error(null, error);
    }

    /// <summary>
    /// Hands a finished build's package to the intake; returns a message key or text when it cannot.
    /// </summary>
    private async Task<string> SendToIntakeCoreAsync(EasBuild build, CancellationToken cancellationToken)
    {
        var organizationId = build.OrganizationId;

        if (build.Status != EasBuildStatus.Finished || build.ArtifactUrl == null)
            return MessageKey.EasBuildNotFinished;

        var extension = EasRules.PackageExtension(build.ArtifactUrl);

        if (extension == null || build.Distribution == "SIMULATOR")
            return MessageKey.ArtifactTypeNotSupported;

        if (!Uri.TryCreate(build.ArtifactUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !IntakeRules.IsHostAllowed(uri.Host, _options.DownloadDomains))
            return MessageKey.DownloadHostNotAllowed;

        var appName = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == build.AppId && a.OrganizationId == organizationId, projection: a => a.Name, cancellationToken: cancellationToken);
        var fileName = $"{Safe(appName)}-{Safe(build.AppVersion ?? "0")}-{Safe(build.AppBuildVersion ?? build.BuildId)}{extension}";
        var sourceRef = new ArtifactSourceRef
        {
            Provider = "eas",
            RunId = build.BuildId,
            RunUrl = build.PageUrl,
            Commit = build.GitCommitHash,
            Branch = build.GitRef ?? build.Profile,
            Actor = build.StartedBy,
        };
        var result = await _intakeService.AcceptDownloadAsync(organizationId, build.AppId, fileName, build.ArtifactUrl, ArtifactSource.Eas, sourceRef, cancellationToken);

        if (!result.IsSuccess)
        {
            var message = result.Messages?.FirstOrDefault();

            return string.IsNullOrEmpty(message?.Key) ? message?.Message ?? MessageKey.ArtifactTypeNotSupported : message.Key;
        }

        build.ArtifactSessionId = result.Data?.SessionId;
        await _buildRepository.UpdateAsync(build, cancellationToken);
        await _auditTrail.WriteAsync(organizationId, AuditAction.EasBuildSentToIntake, build.Id.ToString(CultureInfo.InvariantCulture), $"EAS build {build.BuildId} sent to the build intake (session {build.ArtifactSessionId}).", details: new { build.BuildId, build.ArtifactSessionId }, cancellationToken: cancellationToken);

        return null;
    }

    /// <inheritdoc/>
    public async Task<Response<long>> QueueSyncAsync(int organizationId, int? appId, CancellationToken cancellationToken)
    {
        var linked = await _linkRepository.GetFirstOrDefaultAsync(l => l.OrganizationId == organizationId && (appId == null || l.AppId == appId), projection: l => (int?)l.Id, cancellationToken: cancellationToken);

        if (!linked.HasValue)
            return Response<long>.Error(0, MessageKey.EasProjectNotLinked);

        var jobs = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(organizationId, StoreJobKind.EasSync, new EasSyncPayload(appId), AppId: appId)], cancellationToken);

        return Response<long>.Success(jobs[0]);
    }

    #endregion

    #region Sync and follow

    /// <inheritdoc/>
    public async Task<EasSyncSummary> SyncAsync(int organizationId, int? appId, CancellationToken cancellationToken)
    {
        var token = await OpenAsync(organizationId, cancellationToken) ?? throw new StoreJobException("The organization has no Expo connection.", transient: false);
        var links = await _linkRepository.GetAllAsync(l => l.OrganizationId == organizationId && (appId == null || l.AppId == appId), cancellationToken: cancellationToken);
        var read = new List<(EasProjectLink Link, List<ExpoBuild> Builds)>();
        var failed = new List<string>();

        foreach (var link in links)
        {
            try
            {
                read.Add((link, await _expo.ListBuildsAsync(token, link.ProjectId, _options.BuildsPerSync, cancellationToken)));
                link.SyncError = null;
            }
            catch (StoreCallException ex)
            {
                link.SyncError = SecretRedactor.RedactAndTrim(ex.Message, 500);
                failed.Add($"@{link.AccountName}/{link.Slug}");
            }

            link.SyncedDate = Now();
        }

        var buildIds = read.SelectMany(r => r.Builds).Select(b => b.Id).Distinct().ToList();
        var known = (await _buildRepository.GetAllAsync(b => b.OrganizationId == organizationId && buildIds.Contains(b.BuildId), cancellationToken: cancellationToken))
                    .ToDictionary(b => b.BuildId);
        var added = new List<EasBuild>();
        var changed = new List<EasBuild>();

        foreach (var (link, builds) in read)
        {
            foreach (var remote in builds)
            {
                if (known.TryGetValue(remote.Id, out var row))
                {
                    if (row.AppId == link.AppId && Merge(row, remote))
                        changed.Add(row);

                    continue;
                }

                var build = new EasBuild { OrganizationId = organizationId, AppId = link.AppId, ProjectId = link.ProjectId, BuildId = remote.Id, StartedBy = remote.Actor };

                Merge(build, remote);

                // A build seen for the first time is reported only when it ends later.
                build.EndReported = !EasRules.IsActive(build.Status);
                known[remote.Id] = build;
                added.Add(build);
            }
        }

        await _linkRepository.UpdateAsync(links, cancellationToken);

        try
        {
            await _buildRepository.AddRangeAsync(added, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another sync of the same project saved these builds first (unique build id); the retry updates them.
            throw new StoreJobException("Another sync saved these builds at the same time; trying again.", transient: true);
        }

        await _buildRepository.UpdateAsync(changed, cancellationToken);
        await ReportEndsAsync(changed, cancellationToken);

        return new EasSyncSummary(read.Count, read.Sum(r => r.Builds.Count), failed);
    }

    /// <inheritdoc/>
    public async Task<int> FollowAsync(CancellationToken cancellationToken)
    {
        if (!await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.EasBuilds, cancellationToken))
            return 0;

        var active = new[] { EasBuildStatus.Requested, EasBuildStatus.New, EasBuildStatus.InQueue, EasBuildStatus.InProgress, EasBuildStatus.PendingCancel };
        var builds = await _buildRepository.GetAllAsync(b => active.Contains(b.Status), cancellationToken: cancellationToken);
        var waiting = await PendingChainOrganizationsAsync(cancellationToken);

        if (builds.Count == 0 && waiting.Count == 0)
            return 0;

        var tokens = await TokensAsync([.. builds.Select(b => b.OrganizationId).Concat(waiting).Distinct()], cancellationToken);
        var resolved = new List<(int OrganizationId, string BuildId)>();
        var changed = new List<EasBuild>();

        foreach (var build in builds)
        {
            if (await FollowOneAsync(build, tokens.GetValueOrDefault(build.OrganizationId), cancellationToken))
            {
                changed.Add(build);

                if (build.ReceiptId != null && build.BuildId != null)
                    resolved.Add((build.OrganizationId, build.BuildId));
            }
        }

        // A sync may have read a requested build before its receipt was resolved: keep the requested row.
        var resolvedIds = resolved.Select(r => r.BuildId).ToList();
        var keep = changed.Select(b => b.Id).ToList();

        if (resolvedIds.Count > 0)
            await _buildRepository.ExecuteDeleteAsync(b => resolvedIds.Contains(b.BuildId) && b.ReceiptId == null && !keep.Contains(b.Id), cancellationToken: cancellationToken);

        await _buildRepository.UpdateAsync(changed, cancellationToken);

        _auditTrail.Channel = ActionChannel.System;
        await ReportEndsAsync(changed, cancellationToken);

        return changed.Count + await FollowChainAsync(tokens, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> QueueLinkedAsync(CancellationToken cancellationToken)
    {
        if (!await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.EasBuilds, cancellationToken))
            return 0;

        var organizations = (await _linkRepository.GetAllAsync(l => true, projection: l => new EasProjectLink { OrganizationId = l.OrganizationId }, cancellationToken: cancellationToken))
                            .Select(l => l.OrganizationId).Distinct().ToList();
        var connected = (await _connectionRepository.GetAllAsync(c => organizations.Contains(c.OrganizationId), projection: c => new ExpoConnection { OrganizationId = c.OrganizationId }, cancellationToken: cancellationToken))
                        .Select(c => c.OrganizationId).ToList();

        if (connected.Count > 0)
            await _storeJobQueue.EnqueueAsync([.. connected.Select(o => new StoreJobRequest(o, StoreJobKind.EasSync, new EasSyncPayload()))], cancellationToken);

        return connected.Count;
    }

    /// <summary>
    /// One step of following a build: the receipt of a requested build, then the build itself. Returns whether it changed.
    /// </summary>
    private async Task<bool> FollowOneAsync(EasBuild build, string token, CancellationToken cancellationToken)
    {
        if (token == null)
            return false;

        try
        {
            if (build.Status == EasBuildStatus.Requested)
            {
                var receipt = await _expo.GetReceiptAsync(token, build.ReceiptId, cancellationToken);

                if (receipt?.State == "FAILURE")
                    return End(build, EasBuildStatus.NotStarted, receipt.ErrorMessage ?? "Expo could not start the build.");

                if (receipt?.State != "SUCCESS" || receipt.ResultId == null)
                    return build.StartedDate is DateTime asked && asked.AddHours(Math.Max(1, _options.RequestTimeoutHours)) < Now() && End(build, EasBuildStatus.NotStarted, "Expo did not create the build in time.");

                build.BuildId = receipt.ResultId;
                build.Status = EasBuildStatus.New;
            }

            var remote = await _expo.GetBuildAsync(token, build.BuildId, cancellationToken);

            return remote == null || Merge(build, remote) || build.Status == EasBuildStatus.New;
        }
        catch (StoreCallException)
        {
            // Expo did not answer now; the next run asks again.
            return false;
        }
    }

    private async Task ReportEndsAsync(List<EasBuild> builds, CancellationToken cancellationToken)
    {
        var ended = builds.Where(b => !b.EndReported && !EasRules.IsActive(b.Status)).ToList();

        if (ended.Count == 0)
            return;

        foreach (var build in ended)
            build.EndReported = true;

        await _buildRepository.UpdateAsync(ended, cancellationToken);
        await _auditTrail.WriteManyAsync([.. ended.Select(b => new AuditEntry(b.OrganizationId, ActionOf(b.Status), b.Id.ToString(CultureInfo.InvariantCulture),
                                                                               $"EAS build {b.BuildId ?? b.ReceiptId} ({b.Platform}, {b.Profile}): {b.Status}{(b.ErrorMessage == null ? string.Empty : ": " + b.ErrorMessage)}.",
                                                                               Details: new { b.BuildId, b.Status, b.AppVersion, b.AppBuildVersion, b.ErrorCode },
                                                                               UserName: "system",
                                                                               Channel: ActionChannel.System))],
                                         cancellationToken);

        foreach (var build in ended)
        {
            await _notificationDispatcher.DispatchAsync(new OrgNotification
            {
                OrganizationId = build.OrganizationId,
                Kind = NotificationType.EasBuild,
                Roles = NotificationAudiences.Editors,
                TextKey = build.Status switch
                {
                    EasBuildStatus.Finished => MessageKey.NotificationEasBuildFinished,
                    EasBuildStatus.Canceled => MessageKey.NotificationEasBuildCanceled,
                    _ => MessageKey.NotificationEasBuildFailed,
                },
                TextArgs = [build.Platform.ToString(), build.Profile ?? string.Empty, build.AppVersion ?? string.Empty, build.ErrorMessage ?? string.Empty],
                Data = new { easBuildId = build.Id, build.BuildId, status = build.Status.ToString(), build.PageUrl },
                RelatedEntity = NotificationEntity.EasBuild,
                RelatedEntityId = build.Id.ToString(CultureInfo.InvariantCulture),
                ActionLink = $"/apps/{build.AppId}/eas",
            }, cancellationToken);
        }
    }

    private static AuditAction ActionOf(EasBuildStatus status) => status switch
    {
        EasBuildStatus.Finished => AuditAction.EasBuildFinished,
        EasBuildStatus.Canceled => AuditAction.EasBuildCanceled,
        _ => AuditAction.EasBuildFailed,
    };

    private bool End(EasBuild build, EasBuildStatus status, string error)
    {
        build.Status = status;
        build.FinishedDate = Now();
        build.ErrorMessage = Trim(error, EasBuild.ErrorMaxLength);

        return true;
    }

    /// <summary>
    /// Copies Expo's view of a build; returns whether anything a member sees changed.
    /// </summary>
    private bool Merge(EasBuild build, ExpoBuild remote)
    {
        var before = (build.Status, build.ArtifactUrl, build.ErrorMessage, build.AppVersion, build.AppBuildVersion, build.GitCommitHash);

        build.Platform = remote.Platform;
        build.Status = remote.Status;
        build.Profile = Trim(remote.Profile, EasBuild.ShortMaxLength) ?? build.Profile;
        build.AppVersion = Trim(remote.AppVersion, EasBuild.ShortMaxLength);
        build.AppBuildVersion = Trim(remote.AppBuildVersion, EasBuild.ShortMaxLength);
        build.GitCommitHash = Trim(remote.GitCommitHash, EasBuild.ShortMaxLength);
        build.Distribution = Trim(remote.Distribution, EasBuild.ShortMaxLength);
        build.StartedBy ??= Trim(remote.Actor, EasBuild.ShortMaxLength);
        build.LogsUrl = Trim(remote.LogsUrl, EasBuild.UrlMaxLength);
        build.ArtifactUrl = Trim(remote.ArtifactUrl, EasBuild.UrlMaxLength);
        build.PageUrl = Trim(EasRules.BuildPageUrl(_options.WebsiteUrl, remote.AccountName, remote.Slug, remote.Id), EasBuild.UrlMaxLength);
        build.StartedDate = remote.CreatedDate ?? build.StartedDate;
        build.FinishedDate = remote.CompletedDate ?? (EasRules.IsActive(remote.Status) ? null : build.FinishedDate ?? Now());
        build.ErrorCode = Trim(remote.ErrorCode, EasBuild.ShortMaxLength);
        build.ErrorMessage = Trim(remote.ErrorMessage, EasBuild.ErrorMaxLength);

        return before != (build.Status, build.ArtifactUrl, build.ErrorMessage, build.AppVersion, build.AppBuildVersion, build.GitCommitHash);
    }

    private EasBuild Requested(int organizationId, EasProjectLink link, EasPlatform platform, string profile, string gitRef, ExpoReceipt receipt)
    {
        var build = new EasBuild
        {
            OrganizationId = organizationId,
            AppId = link.AppId,
            ProjectId = link.ProjectId,
            ReceiptId = receipt.Id,
            Platform = platform,
            Status = EasBuildStatus.Requested,
            Profile = profile,
            GitRef = gitRef,
            StartedBy = Trim(_currentUser.UserName, EasBuild.ShortMaxLength),
            StartedInStoreasy = true,
            StartedDate = Now(),
        };

        if (receipt.State == "FAILURE")
        {
            End(build, EasBuildStatus.NotStarted, receipt.ErrorMessage ?? "Expo could not start the build.");
            build.EndReported = true;
        }
        else if (receipt.State == "SUCCESS" && receipt.ResultId != null)
        {
            build.BuildId = receipt.ResultId;
            build.Status = EasBuildStatus.New;
        }

        return build;
    }

    private static void Apply(EasProjectLink link, ExpoProject project)
    {
        link.ProjectId = project.Id;
        link.AccountName = Trim(project.AccountName, EasProjectLink.ShortMaxLength);
        link.Slug = Trim(project.Slug, EasProjectLink.ShortMaxLength);
        link.GitHubRepository = Trim(project.GitHubRepository, EasProjectLink.ShortMaxLength);
        link.DefaultBranch = Trim(project.DefaultBranch, EasProjectLink.ShortMaxLength);
    }

    #endregion

    #region Helpers

    private async Task<string> OpenAsync(int organizationId, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.OrganizationId == organizationId,
                                                                            projection: c => new ExpoConnection { EncryptedSecret = c.EncryptedSecret, EncryptedDataKey = c.EncryptedDataKey, KeyVersion = c.KeyVersion },
                                                                            cancellationToken: cancellationToken);

        return connection == null ? null : Open(organizationId, connection);
    }

    private async Task<Dictionary<int, string>> TokensAsync(List<int> organizations, CancellationToken cancellationToken)
        => (await _connectionRepository.GetAllAsync(c => organizations.Contains(c.OrganizationId), cancellationToken: cancellationToken))
           .ToDictionary(c => c.OrganizationId, c => Open(c.OrganizationId, c));

    private string Open(int organizationId, ExpoConnection connection)
        => Encoding.UTF8.GetString(_secretProtector.Unprotect(new ProtectedSecret(connection.EncryptedSecret, connection.EncryptedDataKey, connection.KeyVersion), EasRules.Context(organizationId)));

    private async Task<string> OpenGitHubAsync(int organizationId, CancellationToken cancellationToken)
    {
        var connection = await _registryConnectionRepository.GetFirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.Kind == RegistryConnectionKind.GitHub,
                                                                                    projection: c => new RegistryConnection { EncryptedSecret = c.EncryptedSecret, EncryptedDataKey = c.EncryptedDataKey, KeyVersion = c.KeyVersion },
                                                                                    cancellationToken: cancellationToken);

        return connection == null
            ? null
            : Encoding.UTF8.GetString(_secretProtector.Unprotect(new ProtectedSecret(connection.EncryptedSecret, connection.EncryptedDataKey, connection.KeyVersion), RegistrySecrets.Context(organizationId, RegistryConnectionKind.GitHub)));
    }

    private static string Refusal(StoreCallException ex) => ex.Transient ? MessageKey.ExpoUnreachable : MessageKey.ExpoTokenRejected;

    private static EasProfilesDto With(EasProfilesDto profiles, string note)
    {
        profiles.Note = note;

        return profiles;
    }

    private static string Safe(string text) => new([.. (text ?? "build").Select(c => char.IsLetterOrDigit(c) || c is '.' or '_' ? c : '-')]);

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private static string Trim(string value, int max) => value == null || value.Length <= max ? value : value[..max];

    #endregion
}
