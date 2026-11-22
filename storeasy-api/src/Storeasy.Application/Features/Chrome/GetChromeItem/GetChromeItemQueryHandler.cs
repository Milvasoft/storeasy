using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Application.Utils.Chrome;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Chrome;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.Chrome.GetChromeItem;

/// <summary>
/// Handles <see cref="GetChromeItemQuery"/>.
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="itemRepository"></param>
/// <param name="connectionRepository"></param>
/// <param name="sessionRepository"></param>
public class GetChromeItemQueryHandler(IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                       IStoreasyRepositoryBase<ChromeItem> itemRepository,
                                       IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                       IStoreasyRepositoryBase<ArtifactSession> sessionRepository) : IInterceptable, IQueryHandler<GetChromeItemQuery, ChromeItemDto>
{
    /// <summary>
    /// Most packages listed.
    /// </summary>
    public const int MaxPackages = 20;

    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<ChromeItem> _itemRepository = itemRepository;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;

    /// <inheritdoc/>
    public async Task<Response<ChromeItemDto>> Handle(GetChromeItemQuery request, CancellationToken cancellationToken)
    {
        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == request.StoreAppId && s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Chrome && !s.App.IsDeleted,
                                                                      projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, ExternalId = s.ExternalId, StoreConnectionId = s.StoreConnectionId, App = new App { Name = s.App.Name } },
                                                                      cancellationToken: cancellationToken);

        if (record == null)
            return Response<ChromeItemDto>.Error(null, MessageKey.StoreAppNotFound);

        var item = await _itemRepository.GetFirstOrDefaultAsync(i => i.StoreAppId == record.Id && i.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken)
                   ?? new ChromeItem { ItemId = record.ExternalId };

        var publisherId = record.StoreConnectionId is int connectionId
            ? (await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == connectionId && c.OrganizationId == request.OrganizationId,
                                                                  projection: c => new StoreConnection { Fingerprint = c.Fingerprint },
                                                                  cancellationToken: cancellationToken))?.Fingerprint?.PublisherId
            : null;

        var sessions = await _sessionRepository.GetAllAsync(s => s.OrganizationId == request.OrganizationId && s.AppId == record.AppId && s.Platform == ArtifactPlatform.Extension,
                                                            projection: s => new ArtifactSession
                                                            {
                                                                Id = s.Id,
                                                                FileName = s.FileName,
                                                                Size = s.Size,
                                                                Status = s.Status,
                                                                StartDate = s.StartDate,
                                                                PackageDeleted = s.PackageDeleted,
                                                                Package = s.Package,
                                                            },
                                                            cancellationToken: cancellationToken);

        var dto = new ChromeItemDto
        {
            StoreAppId = record.Id,
            AppId = record.AppId,
            AppName = record.App?.Name,
            ItemId = record.ExternalId,
            ConnectionId = record.StoreConnectionId,
            PublisherId = publisherId,
            PublishedState = item.PublishedState,
            PublishedVersion = item.PublishedVersion,
            PublishedDeployPercentage = item.PublishedDeployPercentage,
            SubmittedState = item.SubmittedState,
            SubmittedVersion = item.SubmittedVersion,
            SubmittedDeployPercentage = item.SubmittedDeployPercentage,
            LastUploadState = item.LastUploadState,
            UploadedVersion = item.UploadedVersion,
            Warned = item.Warned,
            TakenDown = item.TakenDown,
            Rating = item.Rating,
            RatingCount = item.RatingCount,
            WeeklyUsers = item.WeeklyUsers,
            StatusHistory = [.. (item.StatusHistory ?? []).OrderByDescending(h => h.Date)],
            SyncedDate = item.SyncedDate,
            SyncError = item.SyncError,
            StoreUrl = ChromeLinks.StorePage(record.ExternalId),
            DashboardUrl = ChromeLinks.Dashboard(publisherId, record.ExternalId),
            Packages = [.. sessions.OrderByDescending(s => s.StartDate).Take(MaxPackages).Select(s =>
            {
                var issues = ChromeManifest.IssuesOf(s.Package);

                return new ChromePackageDto
                {
                    SessionId = s.Id,
                    FileName = s.FileName,
                    Size = s.Size,
                    Version = s.Package?.VersionName,
                    Name = s.Package?.Identifier,
                    ManifestVersion = s.Package?.ManifestVersion,
                    Status = s.Status,
                    StartDate = s.StartDate,
                    PackageDeleted = s.PackageDeleted,
                    Uploaded = s.Status == ArtifactSessionStatus.Delivered,
                    Issues = [.. issues.Select(i => i.Text)],
                    Blocked = s.Package == null || issues.Any(i => i.Blocking),
                };
            })],
        };

        return Response<ChromeItemDto>.Success(dto);
    }
}
