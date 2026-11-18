using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Exceptions;
using Milvasoft.Core.Helpers;
using Milvasoft.Identity.Abstract;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Account.Register;

/// <summary>
/// Handles <see cref="RegisterCommand"/>. The user and, with an invitation, the membership are written in one
/// transaction; the verification e-mail goes out after the commit (best effort, the user can ask for it again).
/// </summary>
/// <param name="UserRepository"></param>
/// <param name="InvitationRepository"></param>
/// <param name="MemberRepository"></param>
/// <param name="UserManager"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="AuditTrail"></param>
/// <param name="AccountMailer"></param>
/// <param name="TransactionRunner"></param>
/// <param name="RegistrationModes"></param>
/// <param name="TimeProvider"></param>
[Log]
public record RegisterCommandHandler(IStoreasyRepositoryBase<User> UserRepository,
                                     IStoreasyRepositoryBase<OrganizationInvitation> InvitationRepository,
                                     IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                     IMilvaUserManager<User, int> UserManager,
                                     IOrganizationAccess OrganizationAccess,
                                     IAuditTrail AuditTrail,
                                     IAccountMailer AccountMailer,
                                     ITransactionRunner TransactionRunner,
                                     IRegistrationModes RegistrationModes,
                                     TimeProvider TimeProvider) : IInterceptable, ICommandHandler<RegisterCommand, int>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly IStoreasyRepositoryBase<OrganizationInvitation> _invitationRepository = InvitationRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly IMilvaUserManager<User, int> _userManager = UserManager;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly IAccountMailer _accountMailer = AccountMailer;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly IRegistrationModes _registrationModes = RegistrationModes;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var (mode, _) = await _registrationModes.GetAsync(cancellationToken);

        if (mode == RegistrationMode.Closed)
            return Response<int>.Error(0, MessageKey.RegistrationClosed);

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (mode == RegistrationMode.InviteOnly && string.IsNullOrWhiteSpace(request.InvitationToken))
            return Response<int>.Error(0, MessageKey.RegistrationInvitationRequired);

        var invitation = string.IsNullOrWhiteSpace(request.InvitationToken) ? null : await _invitationRepository.FindOpenInvitationAsync(request.InvitationToken, now, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.InvitationToken) && (invitation == null || invitation.NormalizedEmail != normalizedEmail))
            return Response<int>.Error(0, MessageKey.InvitationInvalid);

        var user = new User
        {
            UserName = email.ToLowerInvariant(),
            Email = email,
            Name = request.Name.Trim(),
            Surname = request.Surname?.Trim(),
            UserType = UserType.AppUser,
            EmailConfirmed = false,
            LockoutEnabled = true,
        };

        var userNameError = _userManager.ValidateUserName(user.UserName);

        if (userNameError is not null)
            return Response<int>.Error(0, userNameError);

        // The password policy is checked by RegisterCommandValidator (IdentityPassword).
        _userManager.ConfigureForCreate(ref user, request.Password);

        user.NormalizedEmail = email.MilvaNormalize();

        _userRepository.FetchSoftDeletedEntities(true);

        var taken = await _userRepository.GetFirstOrDefaultAsync(u => u.NormalizedUserName == user.NormalizedUserName || u.NormalizedEmail == user.NormalizedEmail,
                                                                 projection: u => new { u.Id, u.NormalizedEmail },
                                                                 cancellationToken: cancellationToken);

        if (taken != null)
            return Response<int>.Error(0, taken.NormalizedEmail == user.NormalizedEmail ? MessageKey.EmailExists : MessageKey.UserExists);

        int? memberId = null;

        try
        {
            await _transactionRunner.RunAsync(async () =>
            {
                await _userRepository.AddAsync(user, cancellationToken);

                if (invitation != null)
                {
                    memberId = await _invitationRepository.JoinOrganizationAsync(_memberRepository, invitation.Id, invitation.OrganizationId, user.Id, invitation.Role, invitation.CustomRoleId, now, cancellationToken);

                    // Consumed by a concurrent request: roll the account back too, the caller retries without a stale link.
                    if (memberId == null)
                        throw new MilvaUserFriendlyException(MessageKey.InvitationInvalid);
                }
            });
        }
        catch (DbUpdateException ex) when (UserUniqueConstraintHelper.GetDuplicateMessageKey(ex) is string messageKey)
        {
            // Lost the race against a concurrent registration with the same e-mail.
            return Response<int>.Error(0, messageKey);
        }

        if (invitation != null)
        {
            await _organizationAccess.InvalidateAsync(invitation.OrganizationId, user.Id);

            await _auditTrail.WriteAsync(invitation.OrganizationId, AuditAction.MemberJoined, memberId.ToString(), $"{user.UserName} registered and joined as {invitation.Role}.", cancellationToken: cancellationToken);
        }

        await _accountMailer.SendEmailVerificationAsync(user.Id, user.Email, user.Name, cancellationToken);

        return Response<int>.Success(user.Id);
    }
}
