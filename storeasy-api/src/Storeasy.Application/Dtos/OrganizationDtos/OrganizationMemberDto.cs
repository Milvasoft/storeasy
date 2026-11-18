using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Organizations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// A member row of the members screen.
/// </summary>
public class OrganizationMemberDto
{
    /// <summary>
    /// Membership id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// User id.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// E-mail of the user.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// First name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Last name.
    /// </summary>
    public string Surname { get; set; }

    /// <summary>
    /// Role in the organization.
    /// </summary>
    public OrgRole Role { get; set; }

    /// <summary>
    /// UTC instant the user joined.
    /// </summary>
    public DateTime? JoinDate { get; set; }

    /// <summary>
    /// Projection from a membership row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<OrganizationMember, OrganizationMemberDto>> Projection { get; } = m => new OrganizationMemberDto
    {
        Id = m.Id,
        UserId = m.UserId,
        Email = m.User.Email,
        Name = m.User.Name,
        Surname = m.User.Surname,
        Role = m.Role,
        JoinDate = m.CreationDate,
    };
}
