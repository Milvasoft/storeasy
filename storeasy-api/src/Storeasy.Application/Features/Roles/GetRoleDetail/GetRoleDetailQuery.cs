using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RoleDtos;

namespace Storeasy.Application.Features.Roles.GetRoleDetail;

/// <summary>
/// Data transfer object for role details.
/// </summary>
public record GetRoleDetailQuery : IQuery<RoleDetailDto>
{
    /// <summary>
    /// Role id to access details.
    /// </summary>
    public int RoleId { get; set; }
}
