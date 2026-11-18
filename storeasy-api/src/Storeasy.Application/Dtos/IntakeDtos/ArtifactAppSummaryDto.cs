namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// The last package of an app and its delivery rules (design 13.1).
/// </summary>
public class ArtifactAppSummaryDto
{
    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// App name.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// Last session, if any.
    /// </summary>
    public ArtifactSessionListDto LastSession { get; set; }

    /// <summary>
    /// Delivery rules.
    /// </summary>
    public List<DeliveryRuleDto> Rules { get; set; } = [];
}
