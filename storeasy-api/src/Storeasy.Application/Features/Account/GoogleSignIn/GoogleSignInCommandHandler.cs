using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Core.Exceptions;
using Milvasoft.Core.Helpers;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Identity.Abstract;
using Storeasy.Application.Dtos.AccountDtos;
using Storeasy.Application.Features.Account.Login;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Organizations;
using System.Security.Cryptography;

namespace Storeasy.Application.Features.Account.GoogleSignIn;

/// <summary>
/// Handles <see cref="GoogleSignInCommand"/>. Linking goes only by an address Google verified. When the existing account
/// of that address was never verified (someone may have registered it without owning it), Google's proof takes it over:
/// the address becomes verified, the unknown password is replaced with a random one (the owner can set a new one with
/// "forgot password") and every session and token of the account ends. A new account gets a random password too, so it
/// signs in with Google until its owner sets one. Locked accounts stay locked.
/// </summary>
/// <param name="UserRepository"></param>
/// <param name="ExternalLoginRepository"></param>
/// <param name="InvitationRepository"></param>
/// <param name="MemberRepository"></param>
/// <param name="UserSessionRepository"></param>
/// <param name="UserSessionHistoriesRepository"></param>
/// <param name="UserManager"></param>
/// <param name="GoogleSignIn"></param>
/// <param name="AccountManager"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="AuditTrail"></param>
/// <param name="SecurityStampStore"></param>
/// <param name="TransactionRunner"></param>
/// <param name="Localizer"></param>
/// <param name="RegistrationModes"></param>
/// <param name="TimeProvider"></param>
public record GoogleSignInCommandHandler(IStoreasyRepositoryBase<User> UserRepository,
                                         IStoreasyRepositoryBase<UserExternalLogin> ExternalLoginRepository,
                                         IStoreasyRepositoryBase<OrganizationInvitation> InvitationRepository,
                                         IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                         IStoreasyRepositoryBase<UserSession> UserSessionRepository,
                                         IStoreasyRepositoryBase<UserSessionHistory> UserSessionHistoriesRepository,
                                         IMilvaUserManager<User, int> UserManager,
                                         IGoogleSignIn GoogleSignIn,
                                         IAccountManager AccountManager,
                                         IOrganizationAccess OrganizationAccess,
                                         IAuditTrail AuditTrail,
                                         ISecurityStampStore SecurityStampStore,
                                         ITransactionRunner TransactionRunner,
                                         IMilvaLocalizer Localizer,
                                         IRegistrationModes RegistrationModes,
                                         TimeProvider TimeProvider) : IInterceptable, ICommandHandler<GoogleSignInCommand, LoginResponseDto>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly IStoreasyRepositoryBase<UserExternalLogin> _externalLoginRepository = ExternalLoginRepository;
    private readonly IStoreasyRepositoryBase<OrganizationInvitation> _invitationRepository = InvitationRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly IStoreasyRepositoryBase<UserSession> _userSessionRepository = UserSessionRepository;
    private readonly IStoreasyRepositoryBase<UserSessionHistory> _userSessionHistoriesRepository = UserSessionHistoriesRepository;
    private readonly IMilvaUserManager<User, int> _userManager = UserManager;
    private readonly IGoogleSignIn _googleSignIn = GoogleSignIn;
    private readonly IAccountManager _accountManager = AccountManager;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ISecurityStampStore _securityStampStore = SecurityStampStore;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly IRegistrationModes _registrationModes = RegistrationModes;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<LoginResponseDto>> Handle(GoogleSignInCommand request, CancellationToken cancellationToken)
    {
        if (!_googleSignIn.IsEnabled)
            return Response<LoginResponseDto>.Error(null, MessageKey.GoogleSignInDisabled);

        var result = await _googleSignIn.CompleteAsync(request.Code, request.State, cancellationToken);

        if (result.ErrorKey != null)
            return Response<LoginResponseDto>.Error(null, result.ErrorKey);

        var identity = result.Identity;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var link = await _externalLoginRepository.GetFirstOrDefaultAsync(l => l.Provider == ExternalLoginProvider.Google && l.Subject == identity.Subject,
                                                                         projection: l => new UserExternalLogin { Id = l.Id, UserId = l.UserId },
                                                                         cancellationToken: cancellationToken);

        int userId;

        if (link != null)
        {
            userId = link.UserId;

            await _externalLoginRepository.ExecuteUpdateAsync(link.Id,
                                                              new SetPropertyBuilder<UserExternalLogin>().SetPropertyValue(l => l.LastLoginDate, (DateTime?)now)
                                                                                                         .SetPropertyValue(l => l.Email, Trim(identity.Email, 256)),
                                                              cancellationToken);
        }
        else
        {
            if (!identity.EmailVerified)
                return Response<LoginResponseDto>.Error(null, MessageKey.GoogleEmailNotVerified);

            var resolved = await LinkOrCreateAsync(identity, request.InvitationToken, now, cancellationToken);

            if (resolved.ErrorKey != null)
                return Response<LoginResponseDto>.Error(null, resolved.ErrorKey);

            userId = resolved.UserId;
        }

        var user = await _userRepository.GetFirstOrDefaultAsync(u => u.Id == userId, User.Projections.Login, cancellationToken: cancellationToken);

        if (user == null)
            return Response<LoginResponseDto>.Error(null, MessageKey.Unauthorized);

        if (_userManager.IsLockedOut(user) && user.LockoutEnd > DateTimeOffset.UtcNow)
            return Response<LoginResponseDto>.Error(null, LockoutMessages.Build(_localizer, user.LockoutEnd.Value));

        var token = await _accountManager.LoginAsync(user, request.DeviceId, cancellationToken);

        return Response<LoginResponseDto>.Success(new LoginResponseDto { Id = user.Id, UserType = user.UserType, Token = token });
    }

    /// <summary>
    /// Links the account of the verified address, or creates one when the registration mode allows it.
    /// </summary>
    private async Task<(int UserId, string ErrorKey)> LinkOrCreateAsync(GoogleIdentity identity, string invitationToken, DateTime now, CancellationToken cancellationToken)
    {
        var email = identity.Email;
        var normalizedEmail = email.MilvaNormalize();

        var existing = await _userRepository.GetFirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, User.Projections.ChangePassword, cancellationToken: cancellationToken);

        if (existing != null)
        {
            var emailConfirmed = await _userRepository.GetFirstOrDefaultAsync(u => u.Id == existing.Id, projection: u => (bool?)u.EmailConfirmed, cancellationToken: cancellationToken);

            await LinkExistingAsync(existing, emailConfirmed == true, identity, now, cancellationToken);

            return (existing.Id, null);
        }

        var (mode, _) = await _registrationModes.GetAsync(cancellationToken);

        if (mode == RegistrationMode.Closed)
            return (0, MessageKey.RegistrationClosed);

        var invitation = string.IsNullOrWhiteSpace(invitationToken) ? null : await _invitationRepository.FindOpenInvitationAsync(invitationToken, now, cancellationToken);

        if (!string.IsNullOrWhiteSpace(invitationToken) && (invitation == null || invitation.NormalizedEmail != email.ToUpperInvariant()))
            return (0, MessageKey.InvitationInvalid);

        if (mode == RegistrationMode.InviteOnly && invitation == null)
            return (0, MessageKey.RegistrationInvitationRequired);

        return await CreateAsync(identity, invitation, now, cancellationToken);
    }

    private async Task LinkExistingAsync(User existing, bool emailConfirmed, GoogleIdentity identity, DateTime now, CancellationToken cancellationToken)
    {
        var externalLogin = NewLink(existing.Id, identity, now);

        if (emailConfirmed)
        {
            await _externalLoginRepository.AddAsync(externalLogin, cancellationToken);

            return;
        }

        // Never verified: Google's proof of the address takes the account over from whoever registered it.
        _userManager.SetPasswordHash(existing, RandomPassword());
        existing.EmailConfirmed = true;

        await _transactionRunner.RunAsync(async () =>
        {
            await _userRepository.UpdateAsync(existing, cancellationToken, u => u.PasswordHash, u => u.SecurityStamp, u => u.EmailConfirmed);

            await _userSessionRepository.RevokeAllSessionsAsync(_userSessionHistoriesRepository, existing, cancellationToken);

            await _externalLoginRepository.AddAsync(externalLogin, cancellationToken);
        });

        await _securityStampStore.RotateAsync(existing.Id, cancellationToken);
    }

    private async Task<(int UserId, string ErrorKey)> CreateAsync(GoogleIdentity identity, OrganizationInvitation invitation, DateTime now, CancellationToken cancellationToken)
    {
        var email = identity.Email;
        var name = string.IsNullOrWhiteSpace(identity.GivenName) ? email[..email.IndexOf('@', StringComparison.Ordinal)] : identity.GivenName.Trim();

        var user = new User
        {
            UserName = email.ToLowerInvariant(),
            Email = email,
            Name = Trim(name, User.NameMaxLength),
            Surname = Trim(identity.FamilyName?.Trim(), User.NameMaxLength),
            UserType = UserType.AppUser,
            EmailConfirmed = true,
            LockoutEnabled = true,
        };

        var userNameError = _userManager.ValidateUserName(user.UserName);

        if (userNameError is not null)
            return (0, MessageKey.GoogleSignInFailed);

        _userManager.ConfigureForCreate(ref user, RandomPassword());

        user.NormalizedEmail = email.MilvaNormalize();

        _userRepository.FetchSoftDeletedEntities(true);

        var taken = await _userRepository.GetFirstOrDefaultAsync(u => u.NormalizedUserName == user.NormalizedUserName || u.NormalizedEmail == user.NormalizedEmail,
                                                                 projection: u => new { u.Id, u.NormalizedEmail },
                                                                 cancellationToken: cancellationToken);

        _userRepository.FetchSoftDeletedEntities(false);

        if (taken != null)
            return (0, taken.NormalizedEmail == user.NormalizedEmail ? MessageKey.EmailExists : MessageKey.UserExists);

        int? memberId = null;

        try
        {
            await _transactionRunner.RunAsync(async () =>
            {
                await _userRepository.AddAsync(user, cancellationToken);

                await _externalLoginRepository.AddAsync(NewLink(user.Id, identity, now), cancellationToken);

                if (invitation != null)
                {
                    memberId = await _invitationRepository.JoinOrganizationAsync(_memberRepository, invitation.Id, invitation.OrganizationId, user.Id, invitation.Role, invitation.CustomRoleId, now, cancellationToken);

                    if (memberId == null)
                        throw new MilvaUserFriendlyException(MessageKey.InvitationInvalid);
                }
            });
        }
        catch (DbUpdateException ex) when (UserUniqueConstraintHelper.GetDuplicateMessageKey(ex) is string messageKey)
        {
            return (0, messageKey);
        }

        if (invitation != null)
        {
            await _organizationAccess.InvalidateAsync(invitation.OrganizationId, user.Id);

            await _auditTrail.WriteAsync(invitation.OrganizationId, AuditAction.MemberJoined, memberId.ToString(), $"{user.UserName} signed up with Google and joined as {OrgRoleRules.Describe(invitation.Role, invitation.CustomRoleId)}.", cancellationToken: cancellationToken);
        }

        return (user.Id, null);
    }

    private static UserExternalLogin NewLink(int userId, GoogleIdentity identity, DateTime now) => new()
    {
        UserId = userId,
        Provider = ExternalLoginProvider.Google,
        Subject = Trim(identity.Subject, UserExternalLogin.SubjectMaxLength),
        Email = Trim(identity.Email, 256),
        LastLoginDate = now,
    };

    /// <summary>
    /// A password nobody knows that satisfies any password policy.
    /// </summary>
    private static string RandomPassword() => $"{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}aA1!";

    private static string Trim(string value, int max) => value?.Length > max ? value[..max] : value;
}
