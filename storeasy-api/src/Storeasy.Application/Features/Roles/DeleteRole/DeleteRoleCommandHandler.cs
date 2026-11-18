using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Roles.DeleteRole;

/// <summary>
/// Handles the deletion of the role. Only a holder of full privilege deletes a role granting a permission the actor does
/// not hold. Its members' session stamps are rotated, so tokens carrying the removed permissions stop working.
/// </summary>
/// <param name="RoleRepository"></param>
/// <param name="RolePermissionRelationRepository"></param>
/// <param name="UserRoleRelationRepository"></param>
/// <param name="UserRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="SecurityStampStore"></param>
[Log]
[UserActivityTrack(UserActivity.DeleteRole)]
public record DeleteRoleCommandHandler(IStoreasyRepositoryBase<Role> RoleRepository,
                                       IStoreasyRepositoryBase<RolePermissionRelation> RolePermissionRelationRepository,
                                       IStoreasyRepositoryBase<UserRoleRelation> UserRoleRelationRepository,
                                       IStoreasyRepositoryBase<User> UserRepository,
                                       ICurrentUser CurrentUser,
                                       ISecurityStampStore SecurityStampStore) : IInterceptable, ICommandHandler<DeleteRoleCommand, int>
{
    private readonly IStoreasyRepositoryBase<Role> _roleRepository = RoleRepository;
    private readonly IStoreasyRepositoryBase<RolePermissionRelation> _rolePermissionRelationRepository = RolePermissionRelationRepository;
    private readonly IStoreasyRepositoryBase<UserRoleRelation> _userRoleRelationRepository = UserRoleRelationRepository;
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly ISecurityStampStore _securityStampStore = SecurityStampStore;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetForDeleteAsync(request.RoleId, cancellationToken: cancellationToken);

        if (role == null)
            return Response<int>.Error(0, MessageKey.RoleNotFound);

        var actor = await _userRepository.GetPlatformPermissionsAsync(_currentUser.GetRequiredId(), cancellationToken);

        if (!PlatformPermissions.CanGrant(actor, await _rolePermissionRelationRepository.GetRolePermissionNamesAsync([request.RoleId], cancellationToken)))
            return Response<int>.Error(0, MessageKey.PermissionEscalation);

        var holders = await _userRoleRelationRepository.GetRoleHolderIdsAsync([request.RoleId], cancellationToken);

        await _roleRepository.DeleteAsync(role, cancellationToken: cancellationToken);

        await _securityStampStore.RotateManyAsync(holders, cancellationToken);

        return Response<int>.Success(request.RoleId);
    }
}
