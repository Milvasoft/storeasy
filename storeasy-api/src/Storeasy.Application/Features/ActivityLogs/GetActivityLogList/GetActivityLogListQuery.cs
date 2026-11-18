using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.ActivityLogDtos;

namespace Storeasy.Application.Features.ActivityLogs.GetActivityLogList;

/// <summary>
/// Data transfer object for user activity log list.
/// </summary>
public record GetActivityLogListQuery : ListRequest, IListRequestQuery<ActivityLogListDto>
{
}
