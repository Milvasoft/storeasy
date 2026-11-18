using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.RoleDtos;

namespace Storeasy.Application.Features.Roles.GetRoleList;

/// <summary>
/// Data transfer object for role list.
/// </summary>
public record GetRoleListQuery : ListRequest, IListRequestQuery<RoleListDto>
{
}