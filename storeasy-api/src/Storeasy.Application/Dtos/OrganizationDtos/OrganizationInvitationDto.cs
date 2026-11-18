using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Organizations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// A pending invitation. The token is never listed; it is returned once by the invite command.
/// </summary>
public class OrganizationInvitationDto
{
    /// <summary>
    /// Invitation id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Invited e-mail.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Role on acceptance.
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
    /// Name of the member who sent the invitation.
    /// </summary>
    public string InviterName { get; set; }

    /// <summary>
    /// UTC expiry.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// UTC instant it was sent.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Projection from an invitation row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<OrganizationInvitation, OrganizationInvitationDto>> Projection { get; } = i => new OrganizationInvitationDto
    {
        Id = i.Id,
        Email = i.Email,
        Role = i.Role,
        CustomRoleId = i.CustomRoleId,
        CustomRoleName = i.CustomRole != null ? i.CustomRole.Name : null,
        InviterName = i.Inviter.Surname == null ? i.Inviter.Name : i.Inviter.Name + " " + i.Inviter.Surname,
        ExpiryDate = i.ExpiryDate,
        CreationDate = i.CreationDate,
    };
}
