using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Firefox;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Firefox.GetFirefoxAddon;

/// <summary>
/// Handles <see cref="GetFirefoxAddonQuery"/>.
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="addonRepository"></param>
/// <param name="sessionRepository"></param>
/// <param name="contentService"></param>
public class GetFirefoxAddonQueryHandler(IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                         IStoreasyRepositoryBase<FirefoxAddon> addonRepository,
                                         IStoreasyRepositoryBase<ArtifactSession> sessionRepository,
                                         IStoreContentService contentService) : IInterceptable, IQueryHandler<GetFirefoxAddonQuery, FirefoxAddonDto>
{
    /// <summary>
    /// Most packages listed.
    /// </summary>
    public const int MaxPackages = 20;

    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<FirefoxAddon> _addonRepository = addonRepository;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public async Task<Response<FirefoxAddonDto>> Handle(GetFirefoxAddonQuery request, CancellationToken cancellationToken)
    {
        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == request.StoreAppId && s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Firefox && !s.App.IsDeleted,
                                                                      projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, ExternalId = s.ExternalId, BundleId = s.BundleId, StoreConnectionId = s.StoreConnectionId, App = new App { Name = s.App.Name } },
                                                                      cancellationToken: cancellationToken);

        if (record == null)
            return Response<FirefoxAddonDto>.Error(null, MessageKey.StoreAppNotFound);

        var row = await _addonRepository.GetFirstOrDefaultAsync(a => a.StoreAppId == record.Id && a.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken)
                  ?? new FirefoxAddon { Guid = record.BundleId };

        var dto = new FirefoxAddonDto
        {
            StoreAppId = record.Id,
            AppId = record.AppId,
            AppName = record.App?.Name,
            ConnectionId = record.StoreConnectionId,
            AddonId = row.AddonId,
            Guid = row.Guid ?? record.BundleId,
            Slug = row.Slug,
            Status = row.Status,
            IsDisabled = row.IsDisabled,
            DefaultLocale = row.DefaultLocale,
            ListedVersion = row.ListedVersion,
            LatestVersion = row.LatestVersion,
            LatestChannel = row.LatestChannel,
            LatestFileStatus = row.LatestFileStatus,
            Rating = row.Rating,
            RatingCount = row.RatingCount,
            AverageDailyUsers = row.AverageDailyUsers,
            WeeklyDownloads = row.WeeklyDownloads,
            StatusHistory = [.. (row.StatusHistory ?? []).OrderByDescending(h => h.Date)],
            SyncedDate = row.SyncedDate,
            SyncError = row.SyncError,
            StoreUrl = row.AddonId > 0 || row.Slug != null ? FirefoxLinks.StorePage(row.Slug, row.AddonId) : null,
            DeveloperHubUrl = row.AddonId > 0 || row.Slug != null ? FirefoxLinks.Edit(row.Slug, row.AddonId) : FirefoxLinks.DeveloperHub,
        };

        await ReadLiveAsync(request.OrganizationId, record, dto, cancellationToken);

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

        dto.Packages = [.. sessions.Where(s => s.Package?.Kind == PackageKind.Xpi || s.FileName?.EndsWith(".xpi", StringComparison.OrdinalIgnoreCase) == true)
                                   .OrderByDescending(s => s.StartDate)
                                   .Take(MaxPackages)
                                   .Select(s =>
                                   {
                                       var issues = FirefoxManifest.IssuesOf(s.Package);

                                       return new FirefoxPackageDto
                                       {
                                           SessionId = s.Id,
                                           FileName = s.FileName,
                                           Size = s.Size,
                                           Version = s.Package?.VersionName,
                                           GeckoId = s.Package?.Identifier,
                                           Name = s.Package?.DisplayName,
                                           ManifestVersion = s.Package?.ManifestVersion,
                                           Status = s.Status,
                                           StartDate = s.StartDate,
                                           PackageDeleted = s.PackageDeleted,
                                           Issues = [.. issues.Select(i => i.English)],
                                           Blocked = s.Package == null || issues.Any(i => i.Blocking),
                                       };
                                   })];

        return Response<FirefoxAddonDto>.Success(dto);
    }

    private async Task ReadLiveAsync(int organizationId, StoreApp record, FirefoxAddonDto dto, CancellationToken cancellationToken)
    {
        if (record.StoreConnectionId == null)
            return;

        var app = await _contentService.GetStoreAppAsync(organizationId, record.Id, cancellationToken);

        if (app == null)
            return;

        var read = await _contentService.ReadAsync(app, ContentParts.All, null, cancellationToken);
        var state = read.IsSuccess ? FirefoxContent.FromSnapshot(read.Snapshot) : null;

        if (state == null)
        {
            dto.ReadError = read.ErrorDetail ?? read.ErrorKey;
            return;
        }

        var addon = state.Addon;

        dto.Listing = [.. FirefoxContent.ListingOf(addon).SelectMany(f => f.Values.Where(v => !string.IsNullOrEmpty(v.Value) && !v.Key.StartsWith('_'))
                                                                              .OrderBy(v => v.Key, StringComparer.Ordinal)
                                                                              .Select(v => new FirefoxListingValue { Field = f.Field, Locale = v.Key, Value = v.Value }))];
        dto.Categories = addon.Categories;
        dto.IconUrl = addon.IconUrl;
        dto.DefaultLocale ??= addon.DefaultLocale;
        dto.Previews = [.. addon.Previews.OrderBy(p => p.Position).Select(p => new FirefoxPreviewDto { Id = p.Id, ImageUrl = p.ImageUrl, ThumbnailUrl = p.ThumbnailUrl, Position = p.Position, Caption = p.Caption, Width = p.Width, Height = p.Height })];
        dto.Versions = [.. state.Versions.Select(v => new FirefoxVersionDto
        {
            Id = v.Id,
            Version = v.Version,
            Channel = v.Channel,
            FileStatus = v.FileStatus,
            IsDisabled = v.IsDisabled,
            ReleaseNotes = [.. v.ReleaseNotes.Where(n => !string.IsNullOrEmpty(n.Value) && !n.Key.StartsWith('_')).Select(n => new FirefoxLocalizedText { Locale = n.Key, Value = n.Value })],
            License = v.License,
            HasSource = v.HasSource,
            Created = v.Created,
            FileUrl = v.FileUrl,
            DataCollectionPermissions = v.DataCollectionPermissions,
        })];
    }
}
