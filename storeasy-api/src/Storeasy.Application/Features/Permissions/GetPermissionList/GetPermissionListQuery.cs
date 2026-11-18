using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.PermissionDtos;

namespace Storeasy.Application.Features.Permissions.GetPermissionList;

/// <summary>
/// Data transfer object for permission details.
/// </summary>
public record GetPermissionListQuery : ListRequest, IListRequestQuery<PermissionListDto>
{
}
