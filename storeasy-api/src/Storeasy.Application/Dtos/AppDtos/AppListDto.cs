using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Apps;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.AppDtos;

/// <summary>
/// A storeasy app with its store records.
/// </summary>
public class AppListDto
{
    /// <summary>
    /// App id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Icon address.
    /// </summary>
    public string IconUrl { get; set; }

    /// <summary>
    /// UTC instant the app was added.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// UTC instant a member hid the app from the apps list; null while it is shown.
    /// </summary>
    public DateTime? HiddenDate { get; set; }

    /// <summary>
    /// Place of the app in the apps list a member chose (0 is the first); null for an app that was never placed.
    /// </summary>
    public int? DisplayOrder { get; set; }

    /// <summary>
    /// Records in the stores (one per store at most).
    /// </summary>
    public List<AppStoreRecordDto> Stores { get; set; }

    /// <summary>
    /// Projection from an app row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<App, AppListDto>> Projection { get; } = a => new AppListDto
    {
        Id = a.Id,
        Name = a.Name,
        IconUrl = a.IconUrl,
        CreationDate = a.CreationDate,
        HiddenDate = a.HiddenDate,
        DisplayOrder = a.DisplayOrder,
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
    };
}
