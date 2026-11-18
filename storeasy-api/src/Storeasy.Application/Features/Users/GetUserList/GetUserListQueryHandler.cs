using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.UserDtos;

namespace Storeasy.Application.Features.Users.GetUserList;

/// <summary>
/// Handles the user list operation.
/// </summary>
/// <param name="userRepository"></param>
public class GetUserListQueryHandler(IStoreasyRepositoryBase<User> userRepository) : IInterceptable, IListQueryHandler<GetUserListQuery, UserListDto>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = userRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<UserListDto>> Handle(GetUserListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        // The user export runs this query, so its page may go up to the export cap.
        request.RowCount = request.RowCount.ClampRowCount(20, GlobalConstant.MaxExportRowCount);

        var response = await _userRepository.GetAllAsync(request, projection: UserListDto.Projection, cancellationToken: cancellationToken);

        return response;
    }
}