using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.UserDtos;

namespace Storeasy.Application.Features.Users.GetUserList;

/// <summary>
/// Data transfer object for user list.
/// </summary>
public record GetUserListQuery : ListRequest, IListRequestQuery<UserListDto>
{
}