using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Roles.UpdateRole;

/// <summary>
/// Handles the update of the role. Only a holder of full privilege changes a role that grants (or would grant) full
/// privilege or a permission the actor does not hold. When the permissions change, the session stamp of every member of
/// the role is rotated after the commit, so tokens carrying the old permissions stop working at once (a refresh issues
/// a token with the new ones).
/// </summary>
/// <param name="RoleRepository"></param>
/// <param name="RolePermissionRelationRepository"></param>
/// <param name="UserRoleRelationRepository"></param>
/// <param name="UserRepository"></param>
/// <param name="PermissionRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="SecurityStampStore"></param>
/// <param name="TransactionRunner"></param>
[Log]
[UserActivityTrack(UserActivity.UpdateRole)]
public record UpdateRoleCommandHandler(IStoreasyRepositoryBase<Role> RoleRepository,
                                       IStoreasyRepositoryBase<RolePermissionRelation> RolePermissionRelationRepository,
                                       IStoreasyRepositoryBase<UserRoleRelation> UserRoleRelationRepository,
                                       IStoreasyRepositoryBase<User> UserRepository,
                                       IStoreasyRepositoryBase<Permission> PermissionRepository,
                                       ICurrentUser CurrentUser,
                                       ISecurityStampStore SecurityStampStore,
                                       ITransactionRunner TransactionRunner) : IInterceptable, ICommandHandler<UpdateRoleCommand, int>
{
    private readonly IStoreasyRepositoryBase<Role> _roleRepository = RoleRepository;
    private readonly IStoreasyRepositoryBase<RolePermissionRelation> _rolePermissionRelationRepository = RolePermissionRelationRepository;
    private readonly IStoreasyRepositoryBase<UserRoleRelation> _userRoleRelationRepository = UserRoleRelationRepository;
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly IStoreasyRepositoryBase<Permission> _permissionRepository = PermissionRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly ISecurityStampStore _securityStampStore = SecurityStampStore;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var actor = await _userRepository.GetPlatformPermissionsAsync(_currentUser.GetRequiredId(), cancellationToken);
        var current = await _rolePermissionRelationRepository.GetRolePermissionNamesAsync([request.Id], cancellationToken);
        var permissionIds = request.PermissionIdList.IsUpdated ? request.PermissionIdList.Value?.Distinct().ToList() ?? [] : null;
        var granted = permissionIds == null ? [] : await _permissionRepository.GetPermissionNamesAsync(permissionIds, cancellationToken);

        if (!PlatformPermissions.CanGrant(actor, current.Concat(granted)))
            return Response<int>.Error(0, MessageKey.PermissionEscalation);

        var setPropertyBuilder = _roleRepository.GetUpdatablePropertiesBuilder(request);
        var updated = 0;

        await _transactionRunner.RunAsync(async () =>
        {
            updated = await _roleRepository.ExecuteUpdateAsync(request.Id, setPropertyBuilder, cancellationToken: cancellationToken);

            if (updated == 0 || permissionIds == null)
                return;

            await _rolePermissionRelationRepository.ExecuteDeleteAsync(rl => rl.RoleId == request.Id, cancellationToken: cancellationToken);

            if (permissionIds.Count > 0)
                await _rolePermissionRelationRepository.BulkAddAsync([.. permissionIds.Select(permissionId => new RolePermissionRelation { RoleId = request.Id, PermissionId = permissionId })], null, cancellationToken);
        });

        if (updated == 0)
            return Response<int>.Error(0, MessageKey.RoleNotFound);

        if (permissionIds != null)
            await _securityStampStore.RotateManyAsync(await _userRoleRelationRepository.GetRoleHolderIdsAsync([request.Id], cancellationToken), cancellationToken);

        return Response<int>.Success(request.Id);
    }
}
