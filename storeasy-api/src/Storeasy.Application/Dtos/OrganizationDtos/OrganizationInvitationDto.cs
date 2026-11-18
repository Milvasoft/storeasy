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
        ExpiryDate = i.ExpiryDate,
        CreationDate = i.CreationDate,
    };
}
