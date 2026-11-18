using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RoleDtos;

namespace Storeasy.Application.Features.Roles.GetRoleDetail;

/// <summary>
/// Handles the role detail operation.
/// </summary>
/// <param name="roleRepository"></param>
public class GetRoleDetailQueryHandler(IStoreasyRepositoryBase<Role> roleRepository) : IInterceptable, IQueryHandler<GetRoleDetailQuery, RoleDetailDto>
{
    private readonly IStoreasyRepositoryBase<Role> _roleRepository = roleRepository;

    /// <inheritdoc/>
    public async Task<Response<RoleDetailDto>> Handle(GetRoleDetailQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, projection: RoleDetailDto.Projection, cancellationToken: cancellationToken);

        if (role == null)
            return Response<RoleDetailDto>.Error(null, MessageKey.RoleNotFound);

        return Response<RoleDetailDto>.Success(role);
    }
}
