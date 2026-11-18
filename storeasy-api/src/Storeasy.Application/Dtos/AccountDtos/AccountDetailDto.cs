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
    /// Kind of account (platform manager or app user).
    /// </summary>
    public UserType UserType { get; set; }

    /// <summary>
    /// Interface language the user chose; null for the browser's.
    /// </summary>
    public string PreferredLanguage { get; set; }

    /// <summary>
    /// UTC instant the account was created.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Roles the user belongs to.
    /// </summary>
    public List<NameIntNavigationDto> Roles { get; set; }

    /// <summary>
    /// Platform permissions of the user (<c>{Group}.{Permission}</c>, e.g. <c>RuleCatalogManagement.List</c>), so the web
    /// can show the platform screens; a super admin gets every permission of the catalog. Organization permissions are in
    /// <c>organizations/mine</c>.
    /// </summary>
    public List<string> Permissions { get; set; }

    /// <summary>
    /// External sign in providers linked to the account.
    /// </summary>
    public List<ExternalLoginProvider> ExternalLogins { get; set; }

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
        UserType = u.UserType,
        PreferredLanguage = u.PreferredLanguage,
        CreationDate = u.CreationDate,
        Roles = u.RoleRelations.Select(rr => new NameIntNavigationDto { Id = rr.Role.Id, Name = rr.Role.Name }).ToList(),
        Permissions = u.RoleRelations.SelectMany(rr => rr.Role.RolePermissionRelations.Select(rp => rp.Permission.PermissionGroup + "." + rp.Permission.Name)).ToList(),
    };
}
