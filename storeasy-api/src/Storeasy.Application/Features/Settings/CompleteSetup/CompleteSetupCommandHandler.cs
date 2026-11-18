using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AccountDtos;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Application.Features.Settings.CompleteSetup;

/// <summary>
/// Handles <see cref="CompleteSetupCommand"/>: checks the mode and the setup code, lets <see cref="IFirstRunSetup"/>
/// create the administrator and the team in one transaction, then signs the administrator in like the login does. No
/// <c>[Log]</c>: the response carries the token pair, which cannot be masked.
/// </summary>
/// <param name="FirstRunSetup"></param>
/// <param name="DeploymentOptions"></param>
/// <param name="UserRepository"></param>
/// <param name="AccountManager"></param>
/// <param name="AuditTrail"></param>
public record CompleteSetupCommandHandler(IFirstRunSetup FirstRunSetup,
                                          DeploymentOptions DeploymentOptions,
                                          IStoreasyRepositoryBase<User> UserRepository,
                                          IAccountManager AccountManager,
                                          IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<CompleteSetupCommand, LoginResponseDto>
{
    private readonly IFirstRunSetup _firstRunSetup = FirstRunSetup;
    private readonly DeploymentOptions _deploymentOptions = DeploymentOptions;
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly IAccountManager _accountManager = AccountManager;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<LoginResponseDto>> Handle(CompleteSetupCommand request, CancellationToken cancellationToken)
    {
        // Multi tenant instances are initialized by developer/database/init; the endpoint does not exist there.
        if (!_deploymentOptions.IsSingleTeam)
            return Response<LoginResponseDto>.Error(null, MessageKey.PageNotFound);

        if (!await _firstRunSetup.IsRequiredAsync(cancellationToken))
            return Response<LoginResponseDto>.Error(null, MessageKey.SetupAlreadyCompleted);

        if (!await _firstRunSetup.VerifyCodeAsync(request.SetupCode))
            return Response<LoginResponseDto>.Error(null, MessageKey.SetupCodeInvalid);

        var (userId, organizationId, errorKey) = await _firstRunSetup.CompleteAsync(request, cancellationToken);

        if (errorKey != null)
            return Response<LoginResponseDto>.Error(null, errorKey);

        await _auditTrail.WriteAsync(organizationId, AuditAction.OrganizationCreated, organizationId.ToString(), $"Organization '{request.OrganizationName.Trim()}' created by the first run setup.", cancellationToken: cancellationToken);

        var user = await _userRepository.GetFirstOrDefaultAsync(u => u.Id == userId, User.Projections.Login, cancellationToken: cancellationToken);

        var token = await _accountManager.LoginAsync(user, request.DeviceId, cancellationToken);

        return Response<LoginResponseDto>.Success(new LoginResponseDto { Id = user.Id, UserType = user.UserType, Token = token });
    }
}
