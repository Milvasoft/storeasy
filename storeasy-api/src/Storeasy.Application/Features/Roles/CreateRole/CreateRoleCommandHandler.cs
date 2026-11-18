using Mapster;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Roles.CreateRole;

/// <summary>
/// Handles the creation of the role. Only a holder of full privilege grants full privilege or a permission it does not
/// hold itself (<see cref="PlatformPermissions.CanGrant"/>).
/// </summary>
/// <param name="RoleRepository"></param>
/// <param name="UserRepository"></param>
/// <param name="PermissionRepository"></param>
/// <param name="CurrentUser"></param>
[Log]
[UserActivityTrack(UserActivity.CreateRole)]
public record CreateRoleCommandHandler(IStoreasyRepositoryBase<Role> RoleRepository,
                                       IStoreasyRepositoryBase<User> UserRepository,
                                       IStoreasyRepositoryBase<Permission> PermissionRepository,
                                       ICurrentUser CurrentUser) : IInterceptable, ICommandHandler<CreateRoleCommand, int>
{
    private readonly IStoreasyRepositoryBase<Role> _roleRepository = RoleRepository;
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly IStoreasyRepositoryBase<Permission> _permissionRepository = PermissionRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var actor = await _userRepository.GetPlatformPermissionsAsync(_currentUser.GetRequiredId(), cancellationToken);
        var granted = await _permissionRepository.GetPermissionNamesAsync(request.PermissionIdList?.Distinct().ToList(), cancellationToken);

        if (!PlatformPermissions.CanGrant(actor, granted))
            return Response<int>.Error(0, MessageKey.PermissionEscalation);

        var role = request.Adapt<Role>();

        role.RolePermissionRelations = request.PermissionIdList?.Distinct().Select(permissionId => new RolePermissionRelation { PermissionId = permissionId }).ToList();

        await _roleRepository.AddAsync(role, cancellationToken);

        return Response<int>.Success(role.Id);
    }
}
