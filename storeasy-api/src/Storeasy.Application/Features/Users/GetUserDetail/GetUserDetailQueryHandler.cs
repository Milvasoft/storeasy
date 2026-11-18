using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.UserDtos;

namespace Storeasy.Application.Features.Users.GetUserDetail;

/// <summary>
/// Handles the user detail operation.
/// </summary>
/// <param name="userRepository"></param>
public class GetUserDetailQueryHandler(IStoreasyRepositoryBase<User> userRepository) : IInterceptable, IQueryHandler<GetUserDetailQuery, UserDetailDto>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = userRepository;

    /// <inheritdoc/>
    public async Task<Response<UserDetailDto>> Handle(GetUserDetailQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, projection: UserDetailDto.Projection, cancellationToken: cancellationToken);

        if (user == null)
            return Response<UserDetailDto>.Error(null, MessageKey.UserNotFound);

        return Response<UserDetailDto>.Success(user);
    }
}
