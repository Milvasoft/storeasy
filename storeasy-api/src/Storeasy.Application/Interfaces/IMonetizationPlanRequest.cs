using Storeasy.Application.Dtos.MonetizationDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// A request that plans monetization changes of a store record (subscriptions, In-App Purchases, sandbox testers).
/// </summary>
public interface IMonetizationPlanRequest
{
    /// <summary>
    /// Store record.
    /// </summary>
    int StoreAppId { get; }

    /// <summary>
    /// Changes in the order they are written.
    /// </summary>
    List<MonetizationChangeDto> Changes { get; }
}
