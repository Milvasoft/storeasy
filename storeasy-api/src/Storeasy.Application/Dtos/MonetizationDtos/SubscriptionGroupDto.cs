namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// An App Store subscription group with its subscriptions in level order.
/// </summary>
public class SubscriptionGroupDto
{
    /// <summary>
    /// storeasy id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The store's id.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Reference name.
    /// </summary>
    public string ReferenceName { get; set; }

    /// <summary>
    /// Localizations.
    /// </summary>
    public List<ProductLocalization> Localizations { get; set; }

    /// <summary>
    /// Subscriptions, level 1 first.
    /// </summary>
    public List<SubscriptionSummaryDto> Subscriptions { get; set; }
}
