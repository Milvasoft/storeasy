using Storeasy.Domain.TestFlight;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.TestFlightDtos;

/// <summary>
/// A TestFlight tester.
/// </summary>
public class BetaTesterDto
{
    /// <summary>Tester id.</summary>
    public long Id { get; set; }

    /// <summary>First name.</summary>
    public string FirstName { get; set; }

    /// <summary>Last name.</summary>
    public string LastName { get; set; }

    /// <summary>E-mail.</summary>
    public string Email { get; set; }

    /// <summary>How the tester joined.</summary>
    public BetaInviteType InviteType { get; set; }

    /// <summary>State.</summary>
    public BetaTesterState State { get; set; }

    /// <summary>The store's ids of the tester's groups.</summary>
    public List<string> GroupExternalIds { get; set; }

    /// <summary>The groups as stored (<c>,id1,id2,</c>).</summary>
    [JsonIgnore]
    public string GroupKeys { get; set; }

    /// <summary>
    /// Fills <see cref="GroupExternalIds"/> from <see cref="GroupKeys"/>.
    /// </summary>
    public BetaTesterDto Complete()
    {
        GroupExternalIds = [.. (GroupKeys ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)];

        return this;
    }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<BetaTester, BetaTesterDto>> Projection { get; } = t => new BetaTesterDto
    {
        Id = t.Id,
        FirstName = t.FirstName,
        LastName = t.LastName,
        Email = t.Email,
        InviteType = t.InviteType,
        State = t.State,
        GroupKeys = t.GroupKeys,
    };
}
