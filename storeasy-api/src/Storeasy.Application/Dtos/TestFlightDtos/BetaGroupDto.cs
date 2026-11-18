namespace Storeasy.Application.Dtos.TestFlightDtos;

/// <summary>
/// A TestFlight beta group.
/// </summary>
public class BetaGroupDto
{
    /// <summary>Group id.</summary>
    public int Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; }

    /// <summary>Internal group.</summary>
    public bool IsInternal { get; set; }

    /// <summary>Testers.</summary>
    public int TesterCount { get; set; }

    /// <summary>Public link on.</summary>
    public bool PublicLinkEnabled { get; set; }

    /// <summary>Public link.</summary>
    public string PublicLink { get; set; }

    /// <summary>Public link limit.</summary>
    public int? PublicLinkLimit { get; set; }

    /// <summary>Limit on.</summary>
    public bool PublicLinkLimitEnabled { get; set; }

    /// <summary>Builds in the group.</summary>
    public List<long> BuildIds { get; set; } = [];

    /// <summary>UTC instant of the last sync.</summary>
    public DateTime SyncedDate { get; set; }
}
