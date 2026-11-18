using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Users.DeleteUser;

/// <summary>
/// Handles the deletion of the user. A user who is the last owner of an organization is not deleted (409 naming the
/// organizations: give them another owner first). Otherwise the user's organization memberships are removed with the
/// user, the cached organization access goes, and the revocation stamp (which also closes the live connections) is rotated after the
/// delete, so the deleted user's access tokens are rejected at once. Only a holder of full privilege deletes a user who
/// can do more than the actor.
/// </summary>
/// <param name="UserRepository"></param>
/// <param name="MemberRepository"></param>
/// <param name="SecurityStampStore"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="TransactionRunner"></param>
/// <param name="CurrentUser"></param>
/// <param name="Localizer"></param>
[Log]
[UserActivityTrack(UserActivity.DeleteUser)]
public record DeleteUserCommandHandler(IStoreasyRepositoryBase<User> UserRepository,
                                       IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                       ISecurityStampStore SecurityStampStore,
                                       IOrganizationAccess OrganizationAccess,
                                       ITransactionRunner TransactionRunner,
                                       ICurrentUser CurrentUser,
                                       IMilvaLocalizer Localizer) : IInterceptable, ICommandHandler<DeleteUserCommand, int>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly ISecurityStampStore _securityStampStore = SecurityStampStore;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IMilvaLocalizer _localizer = Localizer;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetForDeleteAsync(request.UserId, cancellationToken: cancellationToken);

        if (user == null)
            return Response<int>.Error(0, MessageKey.UserNotFound);

        var actor = await _userRepository.GetPlatformPermissionsAsync(_currentUser.GetRequiredId(), cancellationToken);

        if (!PlatformPermissions.CanGrant(actor, await _userRepository.GetPlatformPermissionsAsync(request.UserId, cancellationToken)))
            return Response<int>.Error(0, MessageKey.PermissionEscalation);

        var memberships = await _memberRepository.GetAllAsync(m => m.UserId == request.UserId && !m.Organization.IsDeleted,
                                                              projection: m => new OrganizationMember { Id = m.Id, OrganizationId = m.OrganizationId, Role = m.Role, Organization = new Organization { Name = m.Organization.Name } },
                                                              cancellationToken: cancellationToken);

        var owned = memberships.Where(m => m.Role == OrgRole.Owner).Select(m => m.OrganizationId).ToList();

        if (owned.Count > 0)
        {
            var otherOwners = await _memberRepository.GetAllAsync(m => owned.Contains(m.OrganizationId) && m.Role == OrgRole.Owner && m.UserId != request.UserId && !m.User.IsDeleted,
                                                                  projection: m => new OrganizationMember { OrganizationId = m.OrganizationId },
                                                                  cancellationToken: cancellationToken);

            var soleOwned = memberships.Where(m => m.Role == OrgRole.Owner && !otherOwners.Any(o => o.OrganizationId == m.OrganizationId)).Select(m => m.Organization.Name).ToList();

            if (soleOwned.Count > 0)
                return Response<int>.Error(0, new ResponseMessage(MessageKey.UserIsLastOwner, _localizer[MessageKey.UserIsLastOwner, string.Join(", ", soleOwned)]));
        }

        await _transactionRunner.RunAsync(async () =>
        {
            await _memberRepository.ExecuteDeleteAsync(m => m.UserId == request.UserId, cancellationToken: cancellationToken);

            await _userRepository.DeleteAsync(user, cancellationToken: cancellationToken);
        });

        foreach (var membership in memberships)
            await _organizationAccess.InvalidateAsync(membership.OrganizationId, request.UserId);

        await _securityStampStore.RotateAsync(request.UserId, cancellationToken);

        return Response<int>.Success(request.UserId);
    }
}
