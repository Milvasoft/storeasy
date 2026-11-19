using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Apps;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.AppDtos;

/// <summary>
/// A storeasy app with its store records and its newest builds across the stores.
/// </summary>
public class AppDetailDto : AppListDto
{
    /// <summary>
    /// How many builds the detail carries at most.
    /// </summary>
    public const int BuildLimit = 100;

    /// <summary>
    /// Newest builds first (by upload date, then by id).
    /// </summary>
    public List<BuildDto> Builds { get; set; }

    /// <summary>
    /// What the rules catalog knows about the app beyond the stores (saved with <c>PUT apps/profile</c>); null until
    /// it is saved.
    /// </summary>
    public AppProfile Profile { get; set; }

    /// <summary>
    /// Projection from an app row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static new Expression<Func<App, AppDetailDto>> Projection { get; } = a => new AppDetailDto
    {
        Id = a.Id,
        Name = a.Name,
        IconUrl = a.IconUrl,
        CreationDate = a.CreationDate,
        HiddenDate = a.HiddenDate,
        Profile = a.Profile,
        Stores = a.StoreApps.OrderBy(s => s.Store).Select(s => new AppStoreRecordDto
        {
            StoreAppId = s.Id,
            Store = s.Store,
            ExternalId = s.ExternalId,
            BundleId = s.BundleId,
            Name = s.Name,
            StoreConnectionId = s.StoreConnectionId,
            SyncedDate = s.SyncedDate,
        }).ToList(),
        Builds = a.StoreApps.SelectMany(s => s.Builds)
                            .OrderByDescending(b => b.UploadedDate)
                            .ThenByDescending(b => b.Id)
                            .Take(BuildLimit)
                            .Select(b => new BuildDto
                            {
                                Id = b.Id,
                                StoreAppId = b.StoreAppId,
                                Store = b.Store,
                                ExternalId = b.ExternalId,
                                Version = b.Version,
                                BuildNumber = b.BuildNumber,
                                ProcessingState = b.ProcessingState,
                                Tracks = b.Tracks,
                                MinOsVersion = b.MinOsVersion,
                                UploadedDate = b.UploadedDate,
                                ExpiryDate = b.ExpiryDate,
                                UsesNonExemptEncryption = b.UsesNonExemptEncryption,
                            }).ToList(),
    };
}
