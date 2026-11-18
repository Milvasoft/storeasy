namespace Storeasy.Application.Dtos.ReadinessDtos;

/// <summary>
/// A media class in a readiness item.
/// </summary>
public class ReadinessSlotDto
{
    /// <summary>
    /// Store class key.
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Catalog rule.
    /// </summary>
    public string RuleId { get; set; }

    /// <summary>
    /// Whether it is required today.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Own items, fallback or none.
    /// </summary>
    public MediaSlotFill FilledBy { get; set; }

    /// <summary>
    /// Class the store derives it from.
    /// </summary>
    public string FallbackFromClassKey { get; set; }

    /// <summary>
    /// Own items.
    /// </summary>
    public int Count { get; set; }
}
