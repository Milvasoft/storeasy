using Storeasy.Domain.Rules;

namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// One media class (slot) of a locale with its catalog requirement and fill state.
/// </summary>
public class MediaClassDto
{
    /// <summary>
    /// The store's class key (display type, image type).
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Catalog rule.
    /// </summary>
    public string RuleId { get; set; }

    /// <summary>
    /// Title in the language of the request.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Accepted sizes.
    /// </summary>
    public List<RuleSize> Sizes { get; set; }

    /// <summary>
    /// Catalog requirement level.
    /// </summary>
    public string Requirement { get; set; }

    /// <summary>
    /// Whether the class is required today.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Whether the class applies to the app (iPad only when the app supports iPad).
    /// </summary>
    public RuleApplicability Applicability { get; set; }

    /// <summary>
    /// Catalog condition wording.
    /// </summary>
    public string Condition { get; set; }

    /// <summary>
    /// Own items, fallback or none.
    /// </summary>
    public MediaSlotFill Fill { get; set; }

    /// <summary>
    /// Class the store derives this one from.
    /// </summary>
    public string FallbackFromClassKey { get; set; }

    /// <summary>
    /// Own items.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Most items.
    /// </summary>
    public int? Max { get; set; }

    /// <summary>
    /// Items in store order.
    /// </summary>
    public List<MediaItemDto> Items { get; set; }
}
