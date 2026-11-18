using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Infrastructure.Intake.Packages;
using System.Globalization;
using System.Security.Cryptography;

namespace Storeasy.Infrastructure.Intake;

/// <summary>
/// The <see cref="StoreJobKind.ProcessArtifact"/> job (docs/urun-plani.md 11.8, design 13.14): downloads a pulled package,
/// verifies the SHA-256, reads the package and matches it with the app's store record, runs the catalog checks, picks the
/// target (the session's override or the app's delivery rule) and delivers through the existing store engines: Google
/// Play with an edit (bundle upload and a testing track release), App Store Connect with a build upload, waiting for
/// processing and adding the build to an internal TestFlight group. Production and review submission are never part of
/// it: such targets are refused and anything unclear is <c>Held</c> for a member. The job is phase driven by the
/// session's status, so a retry continues where the previous attempt stopped; waiting for Apple's processing is a
/// transient failure retried with backoff until the payload's attempts run out.
/// </summary>
/// <param name="sessionRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="buildRepository"></param>
/// <param name="ruleRepository"></param>
/// <param name="pullSourceRepository"></param>
/// <param name="assetRepository"></param>
/// <param name="settingRepository"></param>
/// <param name="fileStorage"></param>
/// <param name="packageReader"></param>
/// <param name="downloader"></param>
/// <param name="gateways"></param>
/// <param name="appleUploader"></param>
/// <param name="testFlight"></param>
/// <param name="testFlightGateway"></param>
/// <param name="rulesCatalog"></param>
/// <param name="auditTrail"></param>
/// <param name="activityPublisher"></param>
/// <param name="timeProvider"></param>
public sealed class ProcessArtifactJobHandler(IStoreasyRepositoryBase<ArtifactSession> sessionRepository,
                                              IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                              IStoreasyRepositoryBase<Build> buildRepository,
                                              IStoreasyRepositoryBase<DeliveryRule> ruleRepository,
                                              IStoreasyRepositoryBase<ArtifactPullSource> pullSourceRepository,
                                              IStoreasyRepositoryBase<Asset> assetRepository,
                                              IStoreasyRepositoryBase<IntakeSetting> settingRepository,
                                              IFileStorage fileStorage,
                                              PackageReader packageReader,
                                              ArtifactDownloader downloader,
                                              IEnumerable<IStoreContentGateway> gateways,
                                              IAppleBuildUploader appleUploader,
                                              AppleTestFlightClient testFlight,
                                              ITestFlightGateway testFlightGateway,
                                              IRulesCatalog rulesCatalog,
                                              IAuditTrail auditTrail,
                                              IActivityPublisher activityPublisher,
                                              TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IStoreasyRepositoryBase<DeliveryRule> _ruleRepository = ruleRepository;
    private readonly IStoreasyRepositoryBase<ArtifactPullSource> _pullSourceRepository = pullSourceRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = assetRepository;
    private readonly IStoreasyRepositoryBase<IntakeSetting> _settingRepository = settingRepository;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly PackageReader _packageReader = packageReader;
    private readonly ArtifactDownloader _downloader = downloader;
    private readonly IEnumerable<IStoreContentGateway> _gateways = gateways;
    private readonly IAppleBuildUploader _appleUploader = appleUploader;
    private readonly AppleTestFlightClient _testFlight = testFlight;
    private readonly ITestFlightGateway _testFlightGateway = testFlightGateway;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly IActivityPublisher _activityPublisher = activityPublisher;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.ProcessArtifact;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<ProcessArtifactPayload>() ?? throw new StoreJobException("The job has no session.", transient: false);

        var session = await _sessionRepository.GetFirstOrDefaultAsync(s => s.Id == payload.SessionId && s.OrganizationId == context.OrganizationId, cancellationToken: cancellationToken)
                      ?? throw new StoreJobException($"Session {payload.SessionId} was not found.", transient: false);

        EnsureSteps(session);

        if (IntakeRules.IsFinal(session.Status))
            return new StoreJobOutcome($"Session {session.Id} is already {session.Status}.");

        try
        {
            if (session.Status == ArtifactSessionStatus.Uploading && session.DownloadUrl != null)
                await DownloadAsync(session, context, cancellationToken);

            if (session.Status == ArtifactSessionStatus.Parsing)
                await ParseAsync(session, cancellationToken);

            if (session.Status == ArtifactSessionStatus.Checking)
                await CheckAsync(session, payload.Force, cancellationToken);

            if (session.Status == ArtifactSessionStatus.Delivering)
                await DeliverAsync(session, context, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var transient = ex is StoreJobException { Transient: true } or HttpRequestException or IOException;
            var message = SecretRedactor.RedactAndTrim(ex.Message, ArtifactSession.TextMaxLength);

            if (transient && context.Attempt < payload.MaxAttempts)
            {
                session.StatusDescription = message;
                await SaveAsync(session, cancellationToken);
                throw;
            }

            var step = session.Steps.FirstOrDefault(s => s.Status == ArtifactStepStatus.Running);

            if (step != null)
                End(step, ArtifactStepStatus.Failed, message);

            await FinishAsync(session, ArtifactSessionStatus.Failed, $"Delivery failed: {message}", cancellationToken);

            throw new StoreJobException(message, transient: false, ex);
        }

        return new StoreJobOutcome(session.CiSummary ?? session.StatusDescription, new { SessionId = session.Id, session.Status });
    }

    private async Task DownloadAsync(ArtifactSession session, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var step = Start(session, ArtifactStepKind.Upload, "Downloading");
        var source = await _pullSourceRepository.GetFirstOrDefaultAsync(p => p.Id == session.PullSourceId && p.OrganizationId == session.OrganizationId,
                                                                        projection: p => new ArtifactPullSource { Id = p.Id, AllowedDomains = p.AllowedDomains },
                                                                        cancellationToken: cancellationToken);

        if (source == null)
        {
            End(step, ArtifactStepStatus.Failed, "The pull source was removed.");
            await RejectAsync(session, "The pull source was removed.", cancellationToken);
            return;
        }

        var setting = await _settingRepository.GetIntakeSettingAsync(session.OrganizationId, cancellationToken);
        var jobStep = await context.BeginStepAsync("artifact.download", StepDirection.Inbound, session.FileName, session.Size > 0 ? session.Size : null, cancellationToken);
        (long Size, string Sha256) downloaded;

        try
        {
            downloaded = await _downloader.DownloadAsync(session.DownloadUrl, source.AllowedDomains, session.ObjectKey, setting.MaxArtifactBytes, bytes => context.ReportProgressAsync(jobStep, bytes, cancellationToken), cancellationToken);
        }
        catch (ArtifactDownloadException ex)
        {
            await context.EndStepAsync(jobStep, StoreJobStepStatus.Failed, ex.Message, cancellationToken);
            End(step, ArtifactStepStatus.Failed, ex.Message);
            session.DownloadUrl = null;
            session.PackageDeleted = true;
            await RejectAsync(session, ex.Message, cancellationToken);
            return;
        }

        await context.EndStepAsync(jobStep, StoreJobStepStatus.Succeeded, $"{downloaded.Size} bytes", cancellationToken);

        session.DownloadUrl = null;

        if (session.Sha256 != null && session.Sha256 != downloaded.Sha256)
        {
            End(step, ArtifactStepStatus.Failed, "SHA-256 does not match the announced one.");
            await _fileStorage.DeleteAsync(session.ObjectKey, cancellationToken);
            session.PackageDeleted = true;
            await RejectAsync(session, "The downloaded file does not match the announced SHA-256.", cancellationToken);
            return;
        }

        session.Size = downloaded.Size;
        session.Sha256 = downloaded.Sha256;
        step.Bytes = downloaded.Size;
        step.Total = downloaded.Size;
        End(step, ArtifactStepStatus.Done, $"Downloaded {downloaded.Size} bytes");

        var original = await _sessionRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == session.OrganizationId
                                                                             && s.AppId == session.AppId
                                                                             && s.Sha256 == session.Sha256
                                                                             && s.Id != session.Id
                                                                             && s.Status != ArtifactSessionStatus.Duplicate
                                                                             && s.Status != ArtifactSessionStatus.Uploading
                                                                             && !(s.Status == ArtifactSessionStatus.Rejected && s.PackageDeleted),
                                                                        projection: s => new ArtifactSession { Id = s.Id },
                                                                        cancellationToken: cancellationToken);

        if (original != null)
        {
            await _fileStorage.DeleteAsync(session.ObjectKey, cancellationToken);
            session.PackageDeleted = true;
            session.OriginalSessionId = original.Id;
            await FinishAsync(session, ArtifactSessionStatus.Duplicate, $"The package was already received (session {original.Id}).", cancellationToken);
            return;
        }

        session.Status = ArtifactSessionStatus.Parsing;
        session.StatusDescription = "Reading the package";
        await SaveAsync(session, cancellationToken);
    }

    private async Task ParseAsync(ArtifactSession session, CancellationToken cancellationToken)
    {
        var step = Start(session, ArtifactStepKind.Parse, "Reading the package");
        var kind = IntakeRules.Classify(session.FileName)?.Kind ?? PackageKind.Msix;

        if (session.Source is not (ArtifactSource.Eas or ArtifactSource.Webhook))
        {
            var sha256 = await HashAsync(session.ObjectKey, cancellationToken);

            if (sha256 != session.Sha256)
            {
                End(step, ArtifactStepStatus.Failed, "SHA-256 does not match the declared one.");
                await _fileStorage.DeleteAsync(session.ObjectKey, cancellationToken);
                session.PackageDeleted = true;
                await RejectAsync(session, "The uploaded file does not match the declared SHA-256.", cancellationToken);
                return;
            }
        }

        ArtifactPackageInfo package;

        try
        {
            package = await _packageReader.ReadAsync(session.ObjectKey, session.Size, kind, session.FileName, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or ArgumentException or System.Xml.XmlException)
        {
            End(step, ArtifactStepStatus.Failed, ex.Message);
            await RejectAsync(session, $"The file is not a valid {kind.ToString().ToUpperInvariant()}: {ex.Message}", cancellationToken);
            return;
        }

        session.Package = package;

        var store = IntakeRules.StoreOf(session.Platform);
        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.AppId == session.AppId && s.OrganizationId == session.OrganizationId && s.Store == store,
                                                                      projection: s => new StoreApp { Id = s.Id, BundleId = s.BundleId, ExternalId = s.ExternalId },
                                                                      cancellationToken: cancellationToken);

        var expected = record == null ? null : store == StoreKind.Google ? record.ExternalId ?? record.BundleId : record.BundleId ?? record.ExternalId;

        if (store == StoreKind.Microsoft && expected != null && package.Identifier != null && expected.StartsWith(package.Identifier + "_", StringComparison.Ordinal))
            expected = package.Identifier;
        var summary = $"{package.Identifier} {package.VersionName} ({package.VersionCode})";

        if (record == null)
        {
            End(step, ArtifactStepStatus.Failed, summary);
            await RejectAsync(session, $"The app has no {store} record to match {package.Identifier}.", cancellationToken);
            return;
        }

        if (!string.Equals(expected, package.Identifier, StringComparison.Ordinal))
        {
            End(step, ArtifactStepStatus.Failed, $"{package.Identifier} != {expected}");
            await RejectAsync(session, $"Package {package.Identifier} does not match the app ({expected}).", cancellationToken);
            return;
        }

        session.StoreAppId = record.Id;
        End(step, ArtifactStepStatus.Done, summary);
        session.Status = ArtifactSessionStatus.Checking;
        session.StatusDescription = "Checking against the rules catalog";
        await SaveAsync(session, cancellationToken);
        await PublishAsync(session, cancellationToken);
    }

    private async Task CheckAsync(ArtifactSession session, bool force, CancellationToken cancellationToken)
    {
        var step = Start(session, ArtifactStepKind.Check, "Checking");
        var package = session.Package;

        var builds = await _buildRepository.GetAllAsync(b => b.StoreAppId == session.StoreAppId && b.OrganizationId == session.OrganizationId,
                                                        projection: b => new Build { Version = b.Version, BuildNumber = b.BuildNumber },
                                                        cancellationToken: cancellationToken);

        var delivered = await _sessionRepository.GetAllAsync(s => s.AppId == session.AppId && s.Platform == session.Platform && s.Status == ArtifactSessionStatus.Delivered && s.Id != session.Id,
                                                             projection: s => new ArtifactSession { Package = s.Package },
                                                             cancellationToken: cancellationToken);

        var knownCodes = builds.Select(b => b.BuildNumber)
                               .Concat(delivered.Select(d => d.Package?.VersionCode))
                               .Select(v => long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) ? (long?)code : null)
                               .Where(v => v != null)
                               .Select(v => v.Value)
                               .ToList();

        var knownBuilds = builds.Select(b => (b.Version, b.BuildNumber)).Concat(delivered.Where(d => d.Package != null).Select(d => (d.Package.VersionName, d.Package.VersionCode))).ToList();

        var checks = force && session.Checks?.Count > 0
            ? session.Checks
            : ArtifactChecks.Run(package, new ArtifactChecks.Context(_rulesCatalog.Current, DateOnly.FromDateTime(Now()), knownCodes, knownBuilds));

        session.Checks = checks;

        var blocking = checks.Where(c => c.Status == ReadinessStatus.Missing).ToList();
        var warnings = checks.Count(c => c.Status == ReadinessStatus.Warning);

        if (blocking.Count > 0)
        {
            End(step, ArtifactStepStatus.Failed, $"{checks.Count - blocking.Count} / {checks.Count} passed");
            await RejectAsync(session, $"{blocking[0].Title}: {blocking[0].Message}", cancellationToken);
            return;
        }

        End(step, ArtifactStepStatus.Done, warnings > 0 ? $"{checks.Count - warnings} / {checks.Count} passed, {warnings} warning(s)" : $"{checks.Count} / {checks.Count} passed");

        var ruleStep = Start(session, ArtifactStepKind.Rule, "Choosing the target");
        var applied = await ResolveTargetAsync(session, cancellationToken);

        session.AppliedRule = applied;

        var holdReason = applied == null ? $"No delivery rule for {session.Platform}: deliver it by hand."
                         : package.Kind == PackageKind.Apk ? "Google Play takes App Bundles: APK packages are not delivered automatically."
                         : warnings > 0 && applied.OnWarning == DeliveryWarningAction.Hold && !force ? "The checks passed with warnings: a member must approve the delivery."
                         : session.Delivery?.HoldForConfirmation == true && !force ? "Waiting for confirmation: the uploader asked to see the checks before the delivery."
                         : null;

        if (holdReason != null)
        {
            End(ruleStep, ArtifactStepStatus.Done, applied == null ? "No rule" : Describe(applied));
            session.HoldReason = holdReason;
            await FinishAsync(session, ArtifactSessionStatus.Held, $"Held: {holdReason}", cancellationToken);
            return;
        }

        End(ruleStep, ArtifactStepStatus.Done, Describe(applied));
        session.Status = ArtifactSessionStatus.Delivering;
        session.StatusDescription = $"Delivering to {Describe(applied)}";
        await SaveAsync(session, cancellationToken);
        await PublishAsync(session, cancellationToken);
    }

    private async Task<ArtifactAppliedRule> ResolveTargetAsync(ArtifactSession session, CancellationToken cancellationToken)
    {
        var rule = await _ruleRepository.GetFirstOrDefaultAsync(r => r.AppId == session.AppId && r.OrganizationId == session.OrganizationId && r.Platform == session.Platform,
                                                                projection: r => new DeliveryRule
                                                                {
                                                                    Enabled = r.Enabled,
                                                                    Target = r.Target,
                                                                    TargetId = r.TargetId,
                                                                    TargetName = r.TargetName,
                                                                    OnWarning = r.OnWarning,
                                                                    RolloutFraction = r.RolloutFraction,
                                                                    UpdatedByUserName = r.UpdatedByUserName,
                                                                    UpdatedDate = r.UpdatedDate,
                                                                },
                                                                cancellationToken: cancellationToken);

        var overrideTarget = session.Delivery?.Track ?? session.Delivery?.Group;

        if (overrideTarget != null)
        {
            var target = session.Platform == ArtifactPlatform.Android ? DeliveryTarget.PlayTrack : DeliveryTarget.TestFlightGroup;

            if (!IntakeRules.IsTargetAllowed(session.Platform, target, overrideTarget))
                return null;

            return new ArtifactAppliedRule
            {
                Platform = session.Platform,
                Target = target,
                TargetId = overrideTarget.Trim(),
                TargetName = overrideTarget.Trim(),
                Notes = session.Delivery?.Notes,
                OnWarning = rule?.OnWarning ?? DeliveryWarningAction.Hold,
                Override = true,
            };
        }

        if (rule == null || !rule.Enabled || !IntakeRules.IsTargetAllowed(session.Platform, rule.Target, rule.TargetId))
            return null;

        return new ArtifactAppliedRule
        {
            Platform = session.Platform,
            Target = rule.Target,
            TargetId = rule.TargetId,
            TargetName = rule.TargetName ?? rule.TargetId,
            RolloutFraction = rule.RolloutFraction,
            Notes = session.Delivery?.Notes,
            OnWarning = rule.OnWarning,
            RuleUpdatedByUserName = rule.UpdatedByUserName,
            RuleUpdatedDate = rule.UpdatedDate,
        };
    }

    private async Task DeliverAsync(ArtifactSession session, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var step = Start(session, ArtifactStepKind.Deliver, session.StatusDescription);
        var store = IntakeRules.StoreOf(session.Platform);

        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == session.StoreAppId && s.OrganizationId == session.OrganizationId,
                                                                      projection: s => new StoreApp { Id = s.Id, ExternalId = s.ExternalId, StoreConnectionId = s.StoreConnectionId },
                                                                      cancellationToken: cancellationToken)
                     ?? throw new StoreJobException("The store record was removed.", transient: false);

        if (record.StoreConnectionId == null)
            throw new StoreJobException("The store record has no connection.", transient: false);

        var connection = await context.OpenConnectionAsync(record.StoreConnectionId.Value, cancellationToken) ?? throw new StoreJobException("The store connection was removed.", transient: false);

        var result = store switch
        {
            StoreKind.Google => await DeliverToPlayAsync(session, record, connection, context, cancellationToken),
            StoreKind.Microsoft => await DeliverToFlightAsync(session, record, connection, context, cancellationToken),
            _ => await DeliverToTestFlightAsync(session, record, connection, context, cancellationToken),
        };

        if (result == null)
        {
            await SaveAsync(session, cancellationToken);
            throw new StoreJobException(session.StatusDescription, transient: true);
        }

        End(step, ArtifactStepStatus.Done, result);
        await FinishAsync(session, ArtifactSessionStatus.Delivered, $"Delivered {session.Package.DisplayName ?? session.Package.Identifier} {session.Package.VersionName} ({session.Package.VersionCode}) to {Describe(session.AppliedRule)}", cancellationToken);
    }

    private async Task<string> DeliverToPlayAsync(ArtifactSession session, StoreApp record, OpenedStoreConnection connection, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == StoreKind.Google) ?? throw new StoreJobException("Google Play is not supported.", transient: false);
        var rule = session.AppliedRule;

        var changes = new ContentChangeSet
        {
            Bundle = new BundleChange { ObjectKey = session.ObjectKey, Size = session.Size, FileName = session.FileName },
            Release = rule.Target == DeliveryTarget.PlayTrack
                ? new TrackReleaseChange
                {
                    Track = rule.TargetId,
                    Status = rule.RolloutFraction is > 0 and < 1 ? "inProgress" : "completed",
                    UserFraction = rule.RolloutFraction is > 0 and < 1 ? rule.RolloutFraction : null,
                    ReleaseNotes = string.IsNullOrWhiteSpace(rule.Notes) ? [] : [new ReleaseNote("en-US", rule.Notes.Length > 500 ? rule.Notes[..500] : rule.Notes)],
                }
                : null,
        };

        var outcome = await gateway.ApplyAsync(new StoreContentTarget(connection.SecretJson, record.ExternalId), changes, context, cancellationToken);

        session.StoreBuildId = session.Package.VersionCode;

        return string.Join(" ", outcome);
    }

    private async Task<string> DeliverToFlightAsync(ArtifactSession session, StoreApp record, OpenedStoreConnection connection, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == StoreKind.Microsoft) ?? throw new StoreJobException("Microsoft Store is not supported.", transient: false);
        var rule = session.AppliedRule;
        var flightId = rule.Target == DeliveryTarget.MsFlight ? rule.TargetId : null;

        var asset = await _assetRepository.GetFirstOrDefaultAsync(a => a.ObjectKey == session.ObjectKey && a.OrganizationId == session.OrganizationId,
                                                                  projection: a => new Asset { Id = a.Id },
                                                                  cancellationToken: cancellationToken);

        if (asset == null)
        {
            asset = new Asset
            {
                OrganizationId = session.OrganizationId,
                Purpose = AssetPurpose.BuildArtifact,
                ObjectKey = session.ObjectKey,
                FileName = session.FileName,
                ContentType = session.ContentType ?? "application/octet-stream",
                Size = session.Size,
                Sha256 = session.Sha256,
                Status = AssetStatus.Uploaded,
                UploadDate = Now(),
            };

            await _assetRepository.AddAsync(asset, cancellationToken);
        }

        var change = new MsChangeInputDto { Kind = MsChangeKind.Packages, FlightId = flightId, Packages = new MsPackageChangeDto { AddAssetIds = [asset.Id] } };

        var versionId = MsContentReader.Encode(new MsContentReader.Request
        {
            Kind = MsProductKind.Msix,
            ProductId = record.ExternalId,
            Target = flightId == null ? MsSubmissionTarget.App : MsSubmissionTarget.Flight,
            FlightId = flightId,
        }, MsChangeKind.Packages);

        var target = new StoreContentTarget(connection.SecretJson, record.ExternalId, versionId);
        var snapshot = await gateway.ReadAsync(target, ContentParts.Release, cancellationToken);
        var baseHash = snapshot.Get(Storeasy.Application.Features.MsStore.ApplyMsChange.ApplyMsChangeCommandHandler.BaseHashKey);

        var outcome = await gateway.ApplyAsync(target, new ContentChangeSet { Microsoft = new MsPlannedChange { Change = change, BaseHash = baseHash } }, context, cancellationToken);

        return $"{string.Join(" ", outcome)} The draft waits for certification: a member submits it (SubmitForReview, typed confirmation).";
    }

    private async Task<string> DeliverToTestFlightAsync(ArtifactSession session, StoreApp record, OpenedStoreConnection connection, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var package = session.Package;
        var secret = connection.SecretJson;
        var buildId = session.StoreBuildId;

        if (buildId == null)
        {
            var found = await _testFlight.FindBuildAsync(secret, record.ExternalId, package.VersionName, package.VersionCode, cancellationToken);

            buildId = found?.Id;
        }

        if (buildId == null && _appleUploader.Uploads)
        {
            if (session.StoreUploadId == null)
            {
                session.StoreUploadId = await _appleUploader.UploadAsync(new AppleBuildUploadRequest(secret, record.ExternalId, package, session.ObjectKey, session.Size, session.FileName), context, cancellationToken);
                await SaveAsync(session, cancellationToken);
            }

            var upload = await _appleUploader.GetStateAsync(secret, session.StoreUploadId, cancellationToken);

            if (upload.State == "FAILED")
                throw new StoreJobException($"App Store Connect refused the build: {upload.Errors ?? "no reason given"}", transient: false);

            buildId = upload.BuildId;
        }

        if (buildId == null)
        {
            session.StatusDescription = _appleUploader.Uploads ? "Waiting for App Store Connect to process the upload" : "Waiting for the build to appear in App Store Connect";
            return null;
        }

        session.StoreBuildId = buildId;

        var (state, encryption) = await _testFlight.GetBuildAsync(secret, buildId, cancellationToken);

        if (state is "INVALID" or "FAILED")
            throw new StoreJobException($"App Store Connect marked the build {state}.", transient: false);

        if (state != "VALID")
        {
            session.StatusDescription = "Waiting for App Store Connect processing";
            return null;
        }

        if (encryption == null && package.UsesNonExemptEncryption is { } uses)
            await _testFlight.SetExportComplianceAsync(secret, buildId, uses, cancellationToken);

        var rule = session.AppliedRule;

        if (!string.IsNullOrWhiteSpace(rule.Notes))
            await _testFlightGateway.ApplyAsync(secret, record.ExternalId, new TestFlightChange { Operation = TestFlightOperation.WhatToTest, BuildExternalId = buildId, Locale = "en-US", Text = rule.Notes.Length > 4000 ? rule.Notes[..4000] : rule.Notes }, cancellationToken);

        if (rule.Target != DeliveryTarget.TestFlightGroup)
            return $"Build {buildId} processed (VALID).";

        var snapshot = await _testFlightGateway.ReadAsync(secret, record.ExternalId, cancellationToken);
        var group = snapshot.Groups.FirstOrDefault(g => g.ExternalId == rule.TargetId) ?? snapshot.Groups.FirstOrDefault(g => string.Equals(g.Name, rule.TargetId, StringComparison.OrdinalIgnoreCase));

        if (group == null || !group.IsInternal)
            throw new StoreJobException($"\"{rule.TargetName}\" is not an internal TestFlight group of the app.", transient: false);

        if (group.BuildExternalIds?.Contains(buildId) != true)
            await _testFlightGateway.ApplyAsync(secret, record.ExternalId, new TestFlightChange { Operation = TestFlightOperation.UpdateBuilds, GroupExternalId = group.ExternalId, AddBuildExternalIds = [buildId] }, cancellationToken);

        rule.TargetName = group.Name;

        return $"Build {buildId} VALID, added to \"{group.Name}\".";
    }

    private async Task<string> HashAsync(string objectKey, CancellationToken cancellationToken)
    {
        await using var stream = await _fileStorage.OpenReadAsync(objectKey, cancellationToken);

        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private Task RejectAsync(ArtifactSession session, string reason, CancellationToken cancellationToken)
        => FinishAsync(session, ArtifactSessionStatus.Rejected, $"Rejected: {reason}", cancellationToken);

    private async Task FinishAsync(ArtifactSession session, ArtifactSessionStatus status, string summary, CancellationToken cancellationToken)
    {
        var now = Now();
        var text = SecretRedactor.RedactAndTrim(summary, ArtifactSession.TextMaxLength);

        foreach (var pending in session.Steps.Where(s => s.Status is ArtifactStepStatus.Pending or ArtifactStepStatus.Running && s.Kind != ArtifactStepKind.Report))
        {
            pending.Status = ArtifactStepStatus.Skipped;
        }

        var report = Step(session, ArtifactStepKind.Report);

        report.Status = ArtifactStepStatus.Done;
        report.Message = $"exit code {IntakeRules.ExitCodeOf(status)}";
        report.StartDate ??= now;
        report.EndDate = now;

        session.Status = status;
        session.StatusDescription = text;
        session.EndDate = now;
        session.CiExitCode = IntakeRules.ExitCodeOf(status);
        session.CiSummary = text;

        await SaveAsync(session, cancellationToken);

        var action = status switch
        {
            ArtifactSessionStatus.Delivered => AuditAction.ArtifactDelivered,
            ArtifactSessionStatus.Held => AuditAction.ArtifactHeld,
            _ => AuditAction.ArtifactRejected,
        };

        await _auditTrail.WriteManyAsync([new AuditEntry(session.OrganizationId,
                                                         action,
                                                         session.Id.ToString(),
                                                         text,
                                                         IntakeRules.StoreOf(session.Platform),
                                                         new { SessionId = session.Id, session.AppId, session.FileName, session.Status, session.CiExitCode },
                                                         session.UserId,
                                                         session.UserName ?? (session.CiTokenPrefix != null ? $"ci:{session.CiTokenPrefix}" : session.TrustedPublisherId != null ? $"oidc:{session.TrustedPublisherId}" : "system"),
                                                         session.Source == ArtifactSource.Manual ? ActionChannel.Web : ActionChannel.Ci)],
                                         cancellationToken);

        await PublishAsync(session, cancellationToken);
    }

    private Task SaveAsync(ArtifactSession session, CancellationToken cancellationToken)
        => _sessionRepository.UpdateAsync(session,
                                          cancellationToken,
                                          s => s.Status,
                                          s => s.StatusDescription,
                                          s => s.Steps,
                                          s => s.Package,
                                          s => s.Checks,
                                          s => s.AppliedRule,
                                          s => s.StoreAppId,
                                          s => s.HoldReason,
                                          s => s.CiExitCode,
                                          s => s.CiSummary,
                                          s => s.EndDate,
                                          s => s.Size,
                                          s => s.Sha256,
                                          s => s.DownloadUrl,
                                          s => s.OriginalSessionId,
                                          s => s.PackageDeleted,
                                          s => s.StoreBuildId,
                                          s => s.StoreUploadId);

    private Task PublishAsync(ArtifactSession session, CancellationToken cancellationToken)
        => _activityPublisher.PublishAsync(new ActivityEventDto
        {
            Type = ActivityEventTypes.ArtifactProgress,
            OrganizationId = session.OrganizationId,
            AppId = session.AppId,
            JobId = session.StoreJobId,
            Store = IntakeRules.StoreOf(session.Platform),
            Message = $"Session {session.Id}: {session.Status}",
            Date = Now(),
        }, cancellationToken);

    private ArtifactStep Start(ArtifactSession session, ArtifactStepKind kind, string message)
    {
        var step = Step(session, kind);

        step.Status = ArtifactStepStatus.Running;
        step.Message = message;
        step.StartDate ??= Now();

        return step;
    }

    private void End(ArtifactStep step, ArtifactStepStatus status, string message)
    {
        step.Status = status;
        step.Message = SecretRedactor.RedactAndTrim(message, ArtifactSession.TextMaxLength);
        step.EndDate = Now();
    }

    private static ArtifactStep Step(ArtifactSession session, ArtifactStepKind kind) => session.Steps.First(s => s.Kind == kind);

    private static void EnsureSteps(ArtifactSession session)
    {
        session.Steps ??= [];

        foreach (var kind in Enum.GetValues<ArtifactStepKind>().Where(k => session.Steps.TrueForAll(s => s.Kind != k)))
        {
            session.Steps.Add(new ArtifactStep { Kind = kind });
        }

        session.Steps = [.. session.Steps.OrderBy(s => s.Kind)];
        session.Checks ??= [];
    }

    private static string Describe(ArtifactAppliedRule rule) => rule == null ? "no target" : rule.Target switch
    {
        DeliveryTarget.TestFlightGroup => $"TestFlight group {rule.TargetName}",
        DeliveryTarget.PlayTrack => $"Google Play {rule.TargetName}",
        DeliveryTarget.MsFlight => $"package flight {rule.TargetName}",
        _ => "the store (upload only)",
    };

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
