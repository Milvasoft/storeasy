using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Organizations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// An organization the current user belongs to, with the user's role and permissions in it.
/// </summary>
public class MyOrganizationDto
{
    /// <summary>
    /// Organization id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// URL key.
    /// </summary>
    public string Slug { get; set; }

    /// <summary>
    /// The current user's role.
    /// </summary>
    public OrgRole Role { get; set; }

    /// <summary>
    /// Permissions of <see cref="Role"/>, so the client can hide actions without a matrix of its own.
    /// </summary>
    public IReadOnlyList<OrgPermission> Permissions { get; set; }

    /// <summary>
    /// Projection from a membership row; <see cref="Permissions"/> is filled after the query.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<OrganizationMember, MyOrganizationDto>> Projection { get; } = m => new MyOrganizationDto
    {
        Id = m.OrganizationId,
        Name = m.Organization.Name,
        Slug = m.Organization.Slug,
        Role = m.Role,
    };
}
