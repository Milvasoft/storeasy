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
    /// Short lived URL of the organization's logo (a square PNG); null when it has none and shows its initial.
    /// </summary>
    public string LogoUrl { get; set; }

    /// <summary>
    /// Object key of the logo (the handler turns it into <see cref="LogoUrl"/>).
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public string LogoKey { get; set; }

    /// <summary>
    /// The current user's role.
    /// </summary>
    public OrgRole Role { get; set; }

    /// <summary>
    /// The custom role when <see cref="Role"/> is <see cref="OrgRole.Custom"/>.
    /// </summary>
    public int? CustomRoleId { get; set; }

    /// <summary>
    /// Name of the custom role.
    /// </summary>
    public string CustomRoleName { get; set; }

    /// <summary>
    /// Stored permissions of the custom role (the handler turns them into <see cref="Permissions"/>).
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public List<OrgPermission> CustomPermissions { get; set; }

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
        LogoKey = m.Organization.LogoKey,
        Role = m.Role,
        CustomRoleId = m.CustomRoleId,
        CustomRoleName = m.CustomRole != null ? m.CustomRole.Name : null,
        CustomPermissions = m.CustomRole != null ? m.CustomRole.Permissions : null,
    };
}
