using Mapster;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Identity.Abstract;
using Storeasy.Application.Utils.Helpers;

namespace Storeasy.Application.Features.Users.CreateUser;

/// <summary>
/// Handles the creation of the user.
/// </summary>
/// <param name="UserRepository"></param>
/// <param name="UserManager"></param>
/// <param name="RolePermissionRelationRepository"></param>
/// <param name="CurrentUser"></param>
[UserActivityTrack(UserActivity.CreateUser)]
public record CreateUserCommandHandler(IStoreasyRepositoryBase<User> UserRepository,
                                       IMilvaUserManager<User, int> UserManager,
                                       IStoreasyRepositoryBase<RolePermissionRelation> RolePermissionRelationRepository,
                                       ICurrentUser CurrentUser) : IInterceptable, ICommandHandler<CreateUserCommand, int>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly IMilvaUserManager<User, int> _userManager = UserManager;
    private readonly IStoreasyRepositoryBase<RolePermissionRelation> _rolePermissionRelationRepository = RolePermissionRelationRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Only a holder of full privilege gives roles granting full privilege or a permission it does not hold itself.
        var actor = await _userRepository.GetPlatformPermissionsAsync(_currentUser.GetRequiredId(), cancellationToken);

        if (!PlatformPermissions.CanGrant(actor, await _rolePermissionRelationRepository.GetRolePermissionNamesAsync(request.RoleIdList?.Distinct().ToList(), cancellationToken)))
            return Response<int>.Error(0, MessageKey.PermissionEscalation);

        var user = request.Adapt<User>();

        var errorMessageForUserName = _userManager.ValidateUserName(user.UserName);

        if (errorMessageForUserName is not null)
            return Response<int>.Error(default, errorMessageForUserName);

        // The password policy is checked by CreateUserCommandValidator (IdentityPassword).
        _userManager.ConfigureForCreate(ref user, request.Password);

        _userRepository.FetchSoftDeletedEntities(true);

        var userWithSameUsername = await _userRepository.GetFirstOrDefaultAsync(i => i.NormalizedUserName == user.NormalizedUserName, projection: i => new User { Id = i.Id }, cancellationToken: cancellationToken);

        if (userWithSameUsername != null)
            return Response<int>.Error(0, MessageKey.UserExists);

        user.RoleRelations = request.RoleIdList?.Select(roleId => new UserRoleRelation { RoleId = roleId }).ToList();

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
        }
        catch (DbUpdateException ex) when (UserUniqueConstraintHelper.GetDuplicateMessageKey(ex) is string messageKey)
        {
            // Lost the race against a concurrent insert with the same username, or the e-mail is already taken.
            return Response<int>.Error(0, messageKey);
        }

        return Response<int>.Success(user.Id);
    }
}
