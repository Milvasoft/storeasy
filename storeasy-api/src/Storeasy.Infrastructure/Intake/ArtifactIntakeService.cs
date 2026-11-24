using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Organizations;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Storeasy.Infrastructure.Intake;

/// <summary>
/// Default <see cref="IArtifactIntakeService"/>. A session's file goes straight to the bucket through presigned part URLs
/// (the API never sees the bytes). Opening is idempotent per file: an open session of the same app and SHA-256 is resumed
/// (its stored parts listed, fresh URLs for the missing ones), a finished one makes a <c>Duplicate</c> record. Completing
/// is a conditional update from <c>Uploading</c>, so retries and concurrent calls queue one processing job.
/// </summary>
/// <param name="sessionRepository"></param>
/// <param name="appRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="settingRepository"></param>
/// <param name="fileStorage"></param>
/// <param name="artifactStorage"></param>
/// <param name="storeJobQueue"></param>
/// <param name="auditTrail"></param>
/// <param name="activityPublisher"></param>
/// <param name="currentUser"></param>
/// <param name="currentToken"></param>
/// <param name="localizer"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed partial class ArtifactIntakeService(IStoreasyRepositoryBase<ArtifactSession> sessionRepository,
                                                  IStoreasyRepositoryBase<App> appRepository,
                                                  IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                                  IStoreasyRepositoryBase<IntakeSetting> settingRepository,
                                                  IFileStorage fileStorage,
                                                  IArtifactStorage artifactStorage,
                                                  IStoreJobQueue storeJobQueue,
                                                  IAuditTrail auditTrail,
                                                  IActivityPublisher activityPublisher,
                                                  ICurrentUser currentUser,
                                                  ICurrentToken currentToken,
                                                  IMilvaLocalizer localizer,
                                                  IntakeOptions options,
                                                  TimeProvider timeProvider) : IArtifactIntakeService
{
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<IntakeSetting> _settingRepository = settingRepository;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IArtifactStorage _artifactStorage = artifactStorage;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly IActivityPublisher _activityPublisher = activityPublisher;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICurrentToken _currentToken = currentToken;
    private readonly IMilvaLocalizer _localizer = localizer;
    private readonly IntakeOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    [GeneratedRegex(@"[^A-Za-z0-9._-]+")]
    private static partial Regex UnsafeFileNameCharacters();


    /// <inheritdoc/>
    public async Task<Response<ArtifactUploadDto>> OpenAsync(ArtifactOpenRequest request, CancellationToken cancellationToken)
    {
        if (!_fileStorage.IsConfigured)
            return Response<ArtifactUploadDto>.Error(null, MessageKey.StorageUnavailable);

        if (IntakeRules.Classify(request.FileName) is not { } classified)
            return Response<ArtifactUploadDto>.Error(null, MessageKey.ArtifactTypeNotSupported);

        if (!_currentToken.AllowsApp(request.AppId))
            return Response<ArtifactUploadDto>.Error(null, MessageKey.AppForbidden);

        var app = await FindAppAsync(request.OrganizationId, request.AppId, cancellationToken);

        if (app == null)
            return Response<ArtifactUploadDto>.Error(null, MessageKey.AppNotFound);

        var setting = await _settingRepository.GetIntakeSettingAsync(request.OrganizationId, cancellationToken);

        if (request.Size > setting.MaxArtifactBytes)
            return Response<ArtifactUploadDto>.Error(null, new ResponseMessage(MessageKey.ArtifactTooLarge, _localizer[MessageKey.ArtifactTooLarge, setting.MaxArtifactBytes]));

        var deliveryError = await CheckDeliveryAsync(request.OrganizationId, request.AppId, classified.Platform, request.Delivery, cancellationToken);

        if (deliveryError != null)
            return Response<ArtifactUploadDto>.Error(null, deliveryError);

        var sha256 = request.Sha256.ToLowerInvariant();
        var existing = await FindSameFileAsync(request.OrganizationId, request.AppId, sha256, null, cancellationToken);

        if (existing != null && existing.Status == ArtifactSessionStatus.Uploading && existing.UploadId != null && existing.Size == request.Size)
            return Response<ArtifactUploadDto>.Success(await ResumeAsync(existing, cancellationToken));

        if (existing != null && existing.Status != ArtifactSessionStatus.Uploading)
            return Response<ArtifactUploadDto>.Success(await RecordDuplicateAsync(request, classified.Platform, existing.Id, cancellationToken));

        var now = Now();
        var key = ObjectKey(request.OrganizationId, request.AppId, request.FileName);
        var partSize = IntakeRules.PartSizeFor(request.Size);
        var uploadId = await _fileStorage.StartMultipartUploadAsync(key, ContentTypeOf(request.ContentType), cancellationToken);

        var session = NewSession(request.OrganizationId, request.AppId, classified.Platform, request.FileName, request.Size, sha256, request.Source, request.SourceRef, now);

        session.ContentType = ContentTypeOf(request.ContentType);
        session.Delivery = request.Delivery;
        session.ObjectKey = key;
        session.UploadId = uploadId;
        session.PartSize = partSize;
        session.Steps[0].Status = ArtifactStepStatus.Running;
        session.Steps[0].StartDate = now;
        session.Steps[0].Total = request.Size;

        await _sessionRepository.AddAsync(session, cancellationToken);

        await PublishAsync(session.OrganizationId, session.AppId, session.Id, "Uploading", cancellationToken);

        var lifetime = UrlLifetime();
        var parts = Enumerable.Range(1, IntakeRules.PartCount(request.Size, partSize))
                              .Select(n => new ArtifactPartUrlDto { PartNumber = n, Url = _fileStorage.CreatePartUploadUrl(key, uploadId, n, lifetime) })
                              .ToList();

        return Response<ArtifactUploadDto>.Success(new ArtifactUploadDto
        {
            SessionId = session.Id,
            PartSize = partSize,
            Parts = parts,
            UrlExpiryDate = now.Add(lifetime),
        });
    }

    /// <inheritdoc/>
    public async Task<Response<ArtifactSessionListDto>> CompleteAsync(int organizationId, long sessionId, IReadOnlyList<UploadedPart> parts, CancellationToken cancellationToken)
    {
        var session = await FindVisibleAsync(organizationId, sessionId, cancellationToken);

        if (session == null)
            return Response<ArtifactSessionListDto>.Error(null, MessageKey.ArtifactSessionNotFound);

        if (session.Status != ArtifactSessionStatus.Uploading || session.UploadId == null)
            return Response<ArtifactSessionListDto>.Success(await GetListAsync(session.Id, cancellationToken));

        var stored = await _artifactStorage.ListPartsAsync(session.ObjectKey, session.UploadId, cancellationToken);
        var expected = IntakeRules.PartCount(session.Size, session.PartSize);
        var sent = (parts ?? []).GroupBy(p => p.PartNumber).ToDictionary(g => g.Key, g => g.First().ETag);

        var bad = Enumerable.Range(1, expected)
                            .Where(n => !sent.TryGetValue(n, out var etag) || stored.FirstOrDefault(p => p.PartNumber == n) is not { } part || NormalizeETag(part.ETag) != NormalizeETag(etag))
                            .ToList();

        if (bad.Count > 0 || sent.Keys.Any(n => n > expected))
            return Response<ArtifactSessionListDto>.Error(null, new ResponseMessage(MessageKey.UploadIncomplete, _localizer[MessageKey.UploadIncomplete, string.Join(", ", bad.Count > 0 ? bad : sent.Keys.Where(n => n > expected))]));

        await _fileStorage.CompleteMultipartUploadAsync(session.ObjectKey, session.UploadId, [.. Enumerable.Range(1, expected).Select(n => new UploadedPart(n, sent[n]))], cancellationToken);

        var info = await _fileStorage.GetObjectInfoAsync(session.ObjectKey, cancellationToken);
        var now = Now();

        if (info == null || info.Size != session.Size)
        {
            await _fileStorage.DeleteAsync(session.ObjectKey, cancellationToken);

            session.Steps[0].Status = ArtifactStepStatus.Failed;
            session.Steps[0].Message = $"Stored {info?.Size ?? 0} bytes, declared {session.Size}.";
            session.Steps[0].EndDate = now;

            await _sessionRepository.ExecuteUpdateAsync(s => s.Id == session.Id && s.Status == ArtifactSessionStatus.Uploading,
                                                        new SetPropertyBuilder<ArtifactSession>().SetPropertyValue(s => s.Status, ArtifactSessionStatus.Rejected)
                                                                                                 .SetPropertyValue(s => s.StatusDescription, "The stored file does not match the declared size.")
                                                                                                 .SetPropertyValue(s => s.UploadId, (string)null)
                                                                                                 .SetPropertyValue(s => s.PackageDeleted, true)
                                                                                                 .SetPropertyValue(s => s.Steps, session.Steps)
                                                                                                 .SetPropertyValue(s => s.EndDate, (DateTime?)now)
                                                                                                 .SetPropertyValue(s => s.CiExitCode, (int?)IntakeRules.ExitRejected)
                                                                                                 .SetPropertyValue(s => s.CiSummary, "Rejected: the uploaded file does not match the declared size."),
                                                        cancellationToken);

            return Response<ArtifactSessionListDto>.Error(null, MessageKey.SizeMismatch);
        }

        session.Steps[0].Status = ArtifactStepStatus.Done;
        session.Steps[0].Bytes = session.Size;
        session.Steps[0].Message = $"{expected} parts, {session.ResumeCount} resumed";
        session.Steps[0].ResumeCount = session.ResumeCount;
        session.Steps[0].EndDate = now;

        var completed = await _sessionRepository.ExecuteUpdateAsync(s => s.Id == session.Id && s.Status == ArtifactSessionStatus.Uploading,
                                                                    new SetPropertyBuilder<ArtifactSession>().SetPropertyValue(s => s.Status, ArtifactSessionStatus.Parsing)
                                                                                                             .SetPropertyValue(s => s.StatusDescription, "Reading the package")
                                                                                                             .SetPropertyValue(s => s.UploadId, (string)null)
                                                                                                             .SetPropertyValue(s => s.Steps, session.Steps),
                                                                    cancellationToken);

        if (completed > 0)
        {
            await QueueProcessingAsync(session.OrganizationId, session.AppId, session.Id, false, cancellationToken);

            await _auditTrail.WriteAsync(session.OrganizationId,
                                         AuditAction.ArtifactReceived,
                                         session.Id.ToString(),
                                         $"Package {session.FileName} received ({session.Size} bytes).",
                                         IntakeRules.StoreOf(session.Platform, session.FileName),
                                         new { SessionId = session.Id, session.AppId, session.FileName, session.Sha256, session.Source, TokenPrefix = _currentToken.Prefix },
                                         cancellationToken);
        }

        return Response<ArtifactSessionListDto>.Success(await GetListAsync(session.Id, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<ArtifactSessionDetailDto> GetAsync(int organizationId, long sessionId, CancellationToken cancellationToken)
    {
        var detail = await _sessionRepository.GetFirstOrDefaultAsync(s => s.Id == sessionId && s.OrganizationId == organizationId,
                                                                     projection: s => new ArtifactSessionDetailDto
                                                                     {
                                                                         Id = s.Id,
                                                                         AppId = s.AppId,
                                                                         AppName = s.App.Name,
                                                                         Platform = s.Platform,
                                                                         FileName = s.FileName,
                                                                         Size = s.Size,
                                                                         Sha256 = s.Sha256,
                                                                         Source = s.Source,
                                                                         SourceRef = s.SourceRef,
                                                                         Status = s.Status,
                                                                         StatusDescription = s.StatusDescription,
                                                                         StartDate = s.StartDate,
                                                                         EndDate = s.EndDate,
                                                                         StoreJobId = s.StoreJobId,
                                                                         OriginalSessionId = s.OriginalSessionId,
                                                                         Steps = s.Steps,
                                                                         Package = s.Package,
                                                                         Checks = s.Checks,
                                                                         AppliedRule = s.AppliedRule,
                                                                         HoldReason = s.HoldReason,
                                                                         PackageDeleted = s.PackageDeleted,
                                                                         Identity = new ArtifactIdentityDto
                                                                         {
                                                                             Kind = s.IdentityKind,
                                                                             TrustedPublisherId = s.TrustedPublisherId,
                                                                             CiTokenPrefix = s.CiTokenPrefix,
                                                                             UserName = s.UserName,
                                                                         },
                                                                         CiResult = s.CiExitCode == null ? null : new ArtifactCiResultDto { ExitCode = s.CiExitCode.Value, Summary = s.CiSummary },
                                                                     },
                                                                     cancellationToken: cancellationToken);

        if (detail == null || !_currentToken.AllowsApp(detail.AppId))
            return null;

        detail.Steps ??= [];
        detail.Checks ??= [];

        return detail;
    }

    /// <inheritdoc/>
    public Task<Response<ArtifactUploadDto>> AcceptPulledAsync(ArtifactPullRequest request, CancellationToken cancellationToken)
        => AcceptRemoteAsync(request.Source.OrganizationId, request.Source.AppId, request.Source.Id, request.FileName, request.DownloadUrl, request.Size, request.Sha256, request.SourceKind, request.SourceRef, cancellationToken);

    /// <inheritdoc/>
    public Task<Response<ArtifactUploadDto>> AcceptDownloadAsync(int organizationId, int appId, string fileName, string downloadUrl, ArtifactSource sourceKind, ArtifactSourceRef sourceRef, CancellationToken cancellationToken)
        => AcceptRemoteAsync(organizationId, appId, null, fileName, downloadUrl, null, null, sourceKind, sourceRef, cancellationToken);

    private async Task<Response<ArtifactUploadDto>> AcceptRemoteAsync(int organizationId,
                                                                      int appId,
                                                                      int? pullSourceId,
                                                                      string fileName,
                                                                      string downloadUrl,
                                                                      long? size,
                                                                      string announcedSha256,
                                                                      ArtifactSource sourceKind,
                                                                      ArtifactSourceRef sourceRef,
                                                                      CancellationToken cancellationToken)
    {
        if (IntakeRules.Classify(fileName) is not { } classified)
            return Response<ArtifactUploadDto>.Error(null, MessageKey.ArtifactTypeNotSupported);

        var sha256 = announcedSha256?.ToLowerInvariant();

        if (sha256 != null)
        {
            var existing = await FindSameFileAsync(organizationId, appId, sha256, null, cancellationToken);

            if (existing != null)
            {
                var open = new ArtifactOpenRequest(organizationId, appId, fileName, size ?? existing.Size, sha256, null, sourceKind, sourceRef, null);

                return Response<ArtifactUploadDto>.Success(await RecordDuplicateAsync(open, classified.Platform, existing.Id, cancellationToken));
            }
        }

        var now = Now();
        var session = NewSession(organizationId, appId, classified.Platform, fileName, size ?? 0, sha256, sourceKind, sourceRef, now);

        session.ObjectKey = ObjectKey(organizationId, appId, fileName);
        session.DownloadUrl = downloadUrl;
        session.PullSourceId = pullSourceId;
        session.ContentType = "application/octet-stream";
        session.IdentityKind = null;
        session.UserId = null;
        session.UserName = null;
        session.StatusDescription = "Waiting for the download";

        await _sessionRepository.AddAsync(session, cancellationToken);

        await QueueProcessingAsync(session.OrganizationId, session.AppId, session.Id, false, cancellationToken);

        return Response<ArtifactUploadDto>.Success(new ArtifactUploadDto { SessionId = session.Id });
    }

    /// <inheritdoc/>
    public async Task<Response<ArtifactSessionDetailDto>> DeliverHeldAsync(int organizationId, long sessionId, CancellationToken cancellationToken)
    {
        var session = await FindVisibleAsync(organizationId, sessionId, cancellationToken);

        if (session == null)
            return Response<ArtifactSessionDetailDto>.Error(null, MessageKey.ArtifactSessionNotFound);

        if (session.Status != ArtifactSessionStatus.Held || session.PackageDeleted)
            return Response<ArtifactSessionDetailDto>.Error(null, MessageKey.ArtifactNotHeld);

        var moved = await _sessionRepository.ExecuteUpdateAsync(s => s.Id == session.Id && s.Status == ArtifactSessionStatus.Held,
                                                                new SetPropertyBuilder<ArtifactSession>().SetPropertyValue(s => s.Status, ArtifactSessionStatus.Checking)
                                                                                                         .SetPropertyValue(s => s.StatusDescription, "Delivery approved by a member")
                                                                                                         .SetPropertyValue(s => s.HoldReason, (string)null)
                                                                                                         .SetPropertyValue(s => s.CiExitCode, (int?)null)
                                                                                                         .SetPropertyValue(s => s.CiSummary, (string)null)
                                                                                                         .SetPropertyValue(s => s.EndDate, (DateTime?)null),
                                                                cancellationToken);

        if (moved == 0)
            return Response<ArtifactSessionDetailDto>.Error(null, MessageKey.ArtifactNotHeld);

        await QueueProcessingAsync(session.OrganizationId, session.AppId, session.Id, true, cancellationToken);

        return Response<ArtifactSessionDetailDto>.Success(await GetAsync(organizationId, sessionId, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<Response<long>> DeletePackageAsync(int organizationId, long sessionId, CancellationToken cancellationToken)
    {
        var session = await FindVisibleAsync(organizationId, sessionId, cancellationToken);

        if (session == null)
            return Response<long>.Error(0, MessageKey.ArtifactSessionNotFound);

        if (!IntakeRules.IsFinal(session.Status) || session.PackageDeleted)
            return Response<long>.Error(0, MessageKey.ArtifactNotHeld);

        if (session.Status != ArtifactSessionStatus.Duplicate && session.ObjectKey != null)
            await _fileStorage.DeleteAsync(session.ObjectKey, cancellationToken);

        await _sessionRepository.ExecuteUpdateAsync(session.Id, new SetPropertyBuilder<ArtifactSession>().SetPropertyValue(s => s.PackageDeleted, true), cancellationToken);

        await _auditTrail.WriteAsync(organizationId,
                                     AuditAction.ArtifactDeleted,
                                     session.Id.ToString(),
                                     $"Package {session.FileName} deleted.",
                                     IntakeRules.StoreOf(session.Platform, session.FileName),
                                     new { SessionId = session.Id, session.FileName },
                                     cancellationToken);

        return Response<long>.Success(session.Id);
    }

    /// <summary>
    /// Object key of a package: <c>org/{org}/artifacts/{app}/{nonce}/{safe name}</c>.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="appId"></param>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static string ObjectKey(int organizationId, int appId, string fileName)
    {
        var safe = UnsafeFileNameCharacters().Replace(Path.GetFileName(fileName ?? "package"), "-").Trim('-');

        if (safe.Length > 120)
            safe = safe[^120..];

        return $"org/{organizationId}/artifacts/{appId}/{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))}/{safe}";
    }

    private ArtifactSession NewSession(int organizationId, int appId, ArtifactPlatform platform, string fileName, long size, string sha256, ArtifactSource source, ArtifactSourceRef sourceRef, DateTime now)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);

        return new ArtifactSession
        {
            OrganizationId = organizationId,
            AppId = appId,
            Platform = platform,
            FileName = name.Length > ArtifactSession.FileNameMaxLength ? name[^ArtifactSession.FileNameMaxLength..] : name,
            Size = size,
            Sha256 = sha256,
            Source = source,
            SourceRef = sourceRef,
            Status = ArtifactSessionStatus.Uploading,
            StatusDescription = "Uploading",
            StartDate = now,
            IdentityKind = _currentToken.Kind switch
            {
                TokenKind.UploadToken => IdentityKind.Oidc,
                TokenKind.CiToken => IdentityKind.CiToken,
                _ => IdentityKind.User,
            },
            TrustedPublisherId = _currentToken.TrustedPublisherId,
            CiTokenId = _currentToken.Kind == TokenKind.CiToken ? (int?)_currentToken.TokenId : null,
            CiTokenPrefix = _currentToken.Kind == TokenKind.CiToken ? _currentToken.Prefix : null,
            UserId = _currentToken.IsMachine ? null : _currentUser.Id,
            UserName = _currentToken.IsMachine ? null : _currentUser.UserName,
            Steps = [.. Enum.GetValues<ArtifactStepKind>().Select(kind => new ArtifactStep { Kind = kind })],
        };
    }

    private async Task<ArtifactUploadDto> ResumeAsync(ArtifactSession existing, CancellationToken cancellationToken)
    {
        var stored = await _artifactStorage.ListPartsAsync(existing.ObjectKey, existing.UploadId, cancellationToken);
        var lifetime = UrlLifetime();
        var count = IntakeRules.PartCount(existing.Size, existing.PartSize);
        var done = stored.Where(p => p.PartNumber <= count).Select(p => p.PartNumber).ToHashSet();

        await _sessionRepository.ExecuteUpdateAsync(existing.Id, new SetPropertyBuilder<ArtifactSession>().SetPropertyValue(s => s.ResumeCount, existing.ResumeCount + 1), cancellationToken);

        return new ArtifactUploadDto
        {
            SessionId = existing.Id,
            Resumed = true,
            PartSize = existing.PartSize,
            Parts = [.. Enumerable.Range(1, count).Where(n => !done.Contains(n)).Select(n => new ArtifactPartUrlDto { PartNumber = n, Url = _fileStorage.CreatePartUploadUrl(existing.ObjectKey, existing.UploadId, n, lifetime) })],
            UploadedParts = [.. stored.Where(p => p.PartNumber <= count).Select(p => new ArtifactUploadedPartDto { PartNumber = p.PartNumber, ETag = p.ETag })],
            UrlExpiryDate = Now().Add(lifetime),
        };
    }

    private async Task<ArtifactUploadDto> RecordDuplicateAsync(ArtifactOpenRequest request, ArtifactPlatform platform, long originalId, CancellationToken cancellationToken)
    {
        var now = Now();
        var session = NewSession(request.OrganizationId, request.AppId, platform, request.FileName, request.Size, request.Sha256.ToLowerInvariant(), request.Source, request.SourceRef, now);

        session.Status = ArtifactSessionStatus.Duplicate;
        session.StatusDescription = $"Same package as session {originalId}";
        session.OriginalSessionId = originalId;
        session.EndDate = now;
        session.CiExitCode = IntakeRules.ExitOk;
        session.CiSummary = $"The package was already received (session {originalId}).";
        session.PackageDeleted = true;

        foreach (var step in session.Steps)
        {
            step.Status = ArtifactStepStatus.Skipped;
        }

        await _sessionRepository.AddAsync(session, cancellationToken);

        await PublishAsync(session.OrganizationId, session.AppId, session.Id, session.StatusDescription, cancellationToken);

        return new ArtifactUploadDto { SessionId = session.Id, Duplicate = true, OriginalSessionId = originalId };
    }

    private async Task<string> CheckDeliveryAsync(int organizationId, int appId, ArtifactPlatform platform, ArtifactDeliveryOverride delivery, CancellationToken cancellationToken)
    {
        if (delivery == null)
            return null;

        if (delivery.Store is { } store && store != IntakeRules.StoreOf(platform))
            return MessageKey.DeliveryTargetNotAllowed;

        if (!string.IsNullOrWhiteSpace(delivery.Track) && (platform != ArtifactPlatform.Android || IntakeRules.ProductionTracks.Contains(delivery.Track.Trim())))
            return MessageKey.DeliveryTargetNotAllowed;

        if (!string.IsNullOrWhiteSpace(delivery.Group) && platform != ArtifactPlatform.Ios)
            return MessageKey.DeliveryTargetNotAllowed;

        if (delivery.Store == null)
            return null;

        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.AppId == appId && s.OrganizationId == organizationId && s.Store == delivery.Store,
                                                                      projection: s => new StoreApp { Id = s.Id },
                                                                      cancellationToken: cancellationToken);

        return record == null ? MessageKey.DeliveryStoreMismatch : null;
    }

    private Task<App> FindAppAsync(int organizationId, int appId, CancellationToken cancellationToken)
        => _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == organizationId && !a.IsDeleted,
                                                 projection: a => new App { Id = a.Id, Name = a.Name },
                                                 cancellationToken: cancellationToken);

    /// <summary>
    /// The earlier session of the same file that a new upload repeats: one in progress, held or delivered. A rejected or
    /// failed session never blocks the same file, since the reason may be gone (the app record, the rules or the store
    /// changed), so the file is read and checked again.
    /// </summary>
    private async Task<ArtifactSession> FindSameFileAsync(int organizationId, int appId, string sha256, long? exceptId, CancellationToken cancellationToken)
    {
        var candidates = await _sessionRepository.GetAllAsync(s => s.OrganizationId == organizationId
                                                                   && s.AppId == appId
                                                                   && s.Sha256 == sha256
                                                                   && s.Id != exceptId
                                                                   && s.Status != ArtifactSessionStatus.Duplicate
                                                                   && s.Status != ArtifactSessionStatus.Rejected
                                                                   && s.Status != ArtifactSessionStatus.Failed
                                                                   && !(s.Status == ArtifactSessionStatus.Uploading && s.UploadId == null),
                                                              projection: s => new ArtifactSession { Id = s.Id, Status = s.Status, ObjectKey = s.ObjectKey, UploadId = s.UploadId, Size = s.Size, PartSize = s.PartSize, ResumeCount = s.ResumeCount },
                                                              cancellationToken: cancellationToken);

        return candidates.Where(s => s.Status != ArtifactSessionStatus.Uploading).OrderBy(s => s.Id).FirstOrDefault()
               ?? candidates.OrderByDescending(s => s.Id).FirstOrDefault();
    }

    private async Task<ArtifactSession> FindVisibleAsync(int organizationId, long sessionId, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetFirstOrDefaultAsync(s => s.Id == sessionId && s.OrganizationId == organizationId,
                                                                      projection: s => new ArtifactSession
                                                                      {
                                                                          Id = s.Id,
                                                                          OrganizationId = s.OrganizationId,
                                                                          AppId = s.AppId,
                                                                          Platform = s.Platform,
                                                                          FileName = s.FileName,
                                                                          Size = s.Size,
                                                                          Sha256 = s.Sha256,
                                                                          Source = s.Source,
                                                                          Status = s.Status,
                                                                          ObjectKey = s.ObjectKey,
                                                                          UploadId = s.UploadId,
                                                                          PartSize = s.PartSize,
                                                                          ResumeCount = s.ResumeCount,
                                                                          PackageDeleted = s.PackageDeleted,
                                                                          Steps = s.Steps,
                                                                      },
                                                                      cancellationToken: cancellationToken);

        if (session == null || !_currentToken.AllowsApp(session.AppId))
            return null;

        session.Steps ??= [];

        while (session.Steps.Count < Enum.GetValues<ArtifactStepKind>().Length)
        {
            session.Steps.Add(new ArtifactStep { Kind = (ArtifactStepKind)(session.Steps.Count + 1) });
        }

        return session;
    }

    private Task<ArtifactSessionListDto> GetListAsync(long sessionId, CancellationToken cancellationToken)
        => _sessionRepository.GetFirstOrDefaultAsync(s => s.Id == sessionId, projection: ArtifactSessionListDto.Projection, cancellationToken: cancellationToken);

    private async Task QueueProcessingAsync(int organizationId, int appId, long sessionId, bool force, CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _options.ProcessMaxAttempts);
        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(organizationId, StoreJobKind.ProcessArtifact, new ProcessArtifactPayload(sessionId, maxAttempts, force), null, appId, maxAttempts)], cancellationToken);

        await _sessionRepository.ExecuteUpdateAsync(sessionId, new SetPropertyBuilder<ArtifactSession>().SetPropertyValue(s => s.StoreJobId, (long?)jobIds[0]), cancellationToken);
    }

    private Task PublishAsync(int organizationId, int appId, long sessionId, string message, CancellationToken cancellationToken)
        => _activityPublisher.PublishAsync(new ActivityEventDto
        {
            Type = ActivityEventTypes.ArtifactProgress,
            OrganizationId = organizationId,
            AppId = appId,
            Message = $"Session {sessionId}: {message}",
            Date = Now(),
        }, cancellationToken);

    private TimeSpan UrlLifetime() => TimeSpan.FromMinutes(Math.Max(60, _options.UrlLifetimeMinutes));

    private static string ContentTypeOf(string contentType) => string.IsNullOrWhiteSpace(contentType) || contentType.Length > 100 ? "application/octet-stream" : contentType;

    private static string NormalizeETag(string etag) => etag?.Trim().Trim('"').ToLowerInvariant();

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
