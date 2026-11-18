using Milvasoft.Attributes.Annotations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.AccountDtos;

/// <summary>
/// Data transfer object for account details.
/// </summary>
[Translate]
[ExcludeFromMetadata]
public class AccountDetailDto : StoreasyBaseDto<int>
{
    /// <summary>
    /// Unique username of the user. 
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Email of the user.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Whether the e-mail address was verified. Unverified users can sign in but cannot create organizations or store
    /// connections.
    /// </summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Name of the user.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Surname of the user.
    /// </summary>
    public string Surname { get; set; }

    /// <summary>
    /// Roles the user belongs to.
    /// </summary>
    public List<NameIntNavigationDto> Roles { get; set; }

    /// <summary>
    /// Projection expression for mapping User entity to AccountDetailDto.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<User, AccountDetailDto>> Projection { get; } = u => new AccountDetailDto
    {
        Id = u.Id,
        UserName = u.UserName,
        Email = u.Email,
        EmailConfirmed = u.EmailConfirmed,
        Name = u.Name,
        Surname = u.Surname,
        Roles = u.RoleRelations.Select(rr => new NameIntNavigationDto { Id = rr.Role.Id, Name = rr.Role.Name }).ToList()
    };
}
