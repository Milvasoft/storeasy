namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A Play subscription: a container of base plans (what is sold) and their offers. Play has no groups.
/// </summary>
public class PlaySubscriptionDto
{
    /// <summary>
    /// storeasy id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Product id.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// State (active when a base plan is active).
    /// </summary>
    public ProductState State { get; set; }

    /// <summary>
    /// Listings (title, benefits).
    /// </summary>
    public List<ProductLocalization> Listings { get; set; }

    /// <summary>
    /// Base plans.
    /// </summary>
    public List<BasePlanDto> BasePlans { get; set; }
}
