namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// The subscriptions of a store record as last synced: App Store groups, or Play subscriptions (one endpoint, the shape
/// follows the store).
/// </summary>
public class SubscriptionGroupsDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// UTC instant of the last sync; null when never synced.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// App Store subscription groups.
    /// </summary>
    public List<SubscriptionGroupDto> Groups { get; set; } = [];

    /// <summary>
    /// Play subscriptions.
    /// </summary>
    public List<PlaySubscriptionDto> PlaySubscriptions { get; set; } = [];
}
