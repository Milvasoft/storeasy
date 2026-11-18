using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.MsStore;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A Partner Center flight group as recorded in storeasy.
/// </summary>
public class MsFlightGroupDto
{
    /// <summary>
    /// Group id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Name; null when nobody recorded it yet.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Members, when known.
    /// </summary>
    public int? MemberCount { get; set; }

    /// <summary>
    /// Projection from a flight group row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<MsFlightGroup, MsFlightGroupDto>> Projection { get; } = g => new MsFlightGroupDto
    {
        Id = g.GroupId,
        Name = g.Name,
        MemberCount = g.MemberCount,
    };
}
