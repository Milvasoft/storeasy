namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// The delivery rules of an app (design 13.18).
/// </summary>
public class DeliveryRulesDto
{
    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Rules, one per platform.
    /// </summary>
    public List<DeliveryRuleDto> Rules { get; set; } = [];

    /// <summary>
    /// Who changed them last.
    /// </summary>
    public string UpdatedByUserName { get; set; }

    /// <summary>
    /// UTC last change.
    /// </summary>
    public DateTime? UpdatedDate { get; set; }
}
