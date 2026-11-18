namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// A territory and whether the app is available there.
/// </summary>
public class TerritoryDto
{
    /// <summary>
    /// Territory code (ISO 3166-1 alpha-3 in the App Store).
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Whether the app is available.
    /// </summary>
    public bool Available { get; set; }
}
