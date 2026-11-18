using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;

namespace Storeasy.Infrastructure.Intake;

/// <summary>
/// The storage housekeeping of package intake and of the phase 1 asset uploads: deletes stored packages past their
/// organization's retention (default 30 days), closes upload sessions nobody completed (aborting their multipart upload),
/// removes assets that stayed <see cref="AssetStatus.Pending"/>, aborts any multipart upload of the bucket older than the
/// abandon limit and deletes expired upload tokens. Run by <c>IntakeCleanupJob</c>; each run handles at most
/// <see cref="BatchSize"/> rows of each kind.
/// </summary>
/// <param name="sessionRepository"></param>
/// <param name="assetRepository"></param>
/// <param name="settingRepository"></param>
/// <param name="uploadTokenRepository"></param>
/// <param name="fileStorage"></param>
/// <param name="artifactStorage"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class IntakeCleaner(IStoreasyRepositoryBase<ArtifactSession> sessionRepository,
                                  IStoreasyRepositoryBase<Asset> assetRepository,
                                  IStoreasyRepositoryBase<IntakeSetting> settingRepository,
                                  IStoreasyRepositoryBase<UploadToken> uploadTokenRepository,
                                  IFileStorage fileStorage,
                                  IArtifactStorage artifactStorage,
                                  IntakeOptions options,
                                  TimeProvider timeProvider)
{
    /// <summary>
    /// Most rows of each kind one run handles.
    /// </summary>
    public const int BatchSize = 500;

    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = assetRepository;
    private readonly IStoreasyRepositoryBase<IntakeSetting> _settingRepository = settingRepository;
    private readonly IStoreasyRepositoryBase<UploadToken> _uploadTokenRepository = uploadTokenRepository;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IArtifactStorage _artifactStorage = artifactStorage;
    private readonly IntakeOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Runs one cleanup and returns the number of rows changed.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = await _uploadTokenRepository.ExecuteDeleteAsync(t => t.ExpiryDate < now.AddDays(-1), cancellationToken: cancellationToken);

        if (!_fileStorage.IsConfigured)
            return changed;

        changed += await ExpirePackagesAsync(now, cancellationToken);
        changed += await CloseAbandonedSessionsAsync(now, cancellationToken);
        changed += await RemovePendingAssetsAsync(now, cancellationToken);

        var abandoned = await _artifactStorage.ListMultipartUploadsAsync("org/", now.AddHours(-Math.Max(1, _options.AbandonedUploadHours)), cancellationToken);

        foreach (var upload in abandoned.Take(BatchSize))
        {
            await _fileStorage.AbortMultipartUploadAsync(upload.Key, upload.UploadId, cancellationToken);
        }

        return changed;
    }

    private async Task<int> ExpirePackagesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var settings = (await _settingRepository.GetAllAsync(s => s.ArtifactRetentionDays > 0,
                                                             projection: s => new IntakeSetting { OrganizationId = s.OrganizationId, ArtifactRetentionDays = s.ArtifactRetentionDays },
                                                             cancellationToken: cancellationToken)).ToDictionary(s => s.OrganizationId, s => s.ArtifactRetentionDays);

        var shortest = settings.Count == 0 ? IntakeSetting.DefaultRetentionDays : Math.Min(IntakeSetting.DefaultRetentionDays, settings.Values.Min());
        var oldest = now.AddDays(-shortest);

        var candidates = await _sessionRepository.GetAllAsync(s => !s.PackageDeleted
                                                                   && s.ObjectKey != null
                                                                   && s.StartDate < oldest
                                                                   && (s.Status == ArtifactSessionStatus.Delivered || s.Status == ArtifactSessionStatus.Rejected || s.Status == ArtifactSessionStatus.Held
                                                                       || s.Status == ArtifactSessionStatus.Failed || s.Status == ArtifactSessionStatus.Duplicate),
                                                              projection: s => new ArtifactSession { Id = s.Id, OrganizationId = s.OrganizationId, ObjectKey = s.ObjectKey, StartDate = s.StartDate, Status = s.Status },
                                                              cancellationToken: cancellationToken);

        var expired = candidates.Where(s => s.StartDate < now.AddDays(-settings.GetValueOrDefault(s.OrganizationId, IntakeSetting.DefaultRetentionDays)))
                                .OrderBy(s => s.Id)
                                .Take(BatchSize)
                                .ToList();

        foreach (var session in expired.Where(s => s.Status != ArtifactSessionStatus.Duplicate))
        {
            await _fileStorage.DeleteAsync(session.ObjectKey, cancellationToken);
        }

        var ids = expired.Select(s => s.Id).ToList();

        return ids.Count == 0 ? 0 : await _sessionRepository.ExecuteUpdateAsync(s => ids.Contains(s.Id), new SetPropertyBuilder<ArtifactSession>().SetPropertyValue(s => s.PackageDeleted, true), cancellationToken);
    }

    private async Task<int> CloseAbandonedSessionsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var threshold = now.AddHours(-Math.Max(1, _options.AbandonedUploadHours));

        var abandoned = await _sessionRepository.GetAllAsync(s => s.Status == ArtifactSessionStatus.Uploading && s.StartDate < threshold,
                                                             projection: s => new ArtifactSession { Id = s.Id, ObjectKey = s.ObjectKey, UploadId = s.UploadId },
                                                             cancellationToken: cancellationToken);

        var batch = abandoned.OrderBy(s => s.Id).Take(BatchSize).ToList();

        foreach (var session in batch.Where(s => s.UploadId != null))
        {
            await _fileStorage.AbortMultipartUploadAsync(session.ObjectKey, session.UploadId, cancellationToken);
        }

        var ids = batch.Select(s => s.Id).ToList();

        if (ids.Count == 0)
            return 0;

        return await _sessionRepository.ExecuteUpdateAsync(s => ids.Contains(s.Id) && s.Status == ArtifactSessionStatus.Uploading,
                                                           new SetPropertyBuilder<ArtifactSession>().SetPropertyValue(s => s.Status, ArtifactSessionStatus.Failed)
                                                                                                    .SetPropertyValue(s => s.StatusDescription, "The upload was abandoned.")
                                                                                                    .SetPropertyValue(s => s.UploadId, (string)null)
                                                                                                    .SetPropertyValue(s => s.PackageDeleted, true)
                                                                                                    .SetPropertyValue(s => s.EndDate, (DateTime?)now)
                                                                                                    .SetPropertyValue(s => s.CiExitCode, (int?)IntakeRules.ExitStoreError)
                                                                                                    .SetPropertyValue(s => s.CiSummary, "The upload was not completed in time."),
                                                           cancellationToken);
    }

    private async Task<int> RemovePendingAssetsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var threshold = now.AddHours(-Math.Max(1, _options.PendingAssetHours));

        var pending = await _assetRepository.GetAllAsync(a => a.Status == AssetStatus.Pending && a.CreationDate < threshold,
                                                         projection: a => new Asset { Id = a.Id, ObjectKey = a.ObjectKey, UploadId = a.UploadId },
                                                         cancellationToken: cancellationToken);

        var batch = pending.OrderBy(a => a.Id).Take(BatchSize).ToList();

        foreach (var asset in batch)
        {
            if (asset.UploadId != null)
                await _fileStorage.AbortMultipartUploadAsync(asset.ObjectKey, asset.UploadId, cancellationToken);

            await _fileStorage.DeleteAsync(asset.ObjectKey, cancellationToken);
        }

        var ids = batch.Select(a => a.Id).ToList();

        return ids.Count == 0 ? 0 : await _assetRepository.ExecuteDeleteAsync(a => ids.Contains(a.Id) && a.Status == AssetStatus.Pending, cancellationToken: cancellationToken);
    }
}
