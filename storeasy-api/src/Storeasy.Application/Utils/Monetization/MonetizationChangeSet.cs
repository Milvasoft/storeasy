using Storeasy.Application.Dtos.MonetizationDtos;

namespace Storeasy.Application.Utils.Monetization;

/// <summary>
/// What the apply job of a monetization plan writes: the requested changes in order, completed by the planner with the
/// store's ids, the resolved price points (equalization included) and the checked asset of a review screenshot.
/// </summary>
public sealed class MonetizationChangeSet
{
    /// <summary>
    /// Operations in the order they are written (a group before its subscriptions, a product before its prices).
    /// </summary>
    public List<MonetizationChangeDto> Operations { get; set; } = [];
}
