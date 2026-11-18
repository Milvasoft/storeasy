namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// One field the change writes, before and after.
/// </summary>
public class MsPlanItemDto
{
    /// <summary>
    /// Area (listing, image, package, pricing, rollout...).
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Language, when the field is per language.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Field.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Current value (shortened).
    /// </summary>
    public string Before { get; set; }

    /// <summary>
    /// New value (shortened).
    /// </summary>
    public string After { get; set; }

    /// <summary>
    /// add, update or remove.
    /// </summary>
    public string Action { get; set; }
}
