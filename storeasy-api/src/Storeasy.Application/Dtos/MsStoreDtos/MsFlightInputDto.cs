namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A package flight to create.
/// </summary>
public class MsFlightInputDto
{
    /// <summary>
    /// Name, unique in the app.
    /// </summary>
    public string FriendlyName { get; set; }

    /// <summary>
    /// Partner Center flight group ids (at least one).
    /// </summary>
    public List<string> GroupIds { get; set; } = [];

    /// <summary>
    /// Flight the new one ranks above; null for the lowest rank.
    /// </summary>
    public string RankHigherThan { get; set; }
}
