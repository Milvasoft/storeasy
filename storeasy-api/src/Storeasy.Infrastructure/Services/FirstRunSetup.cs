using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.Helpers;
using Milvasoft.Identity.Abstract;
using Storeasy.Application.Features.Settings.CompleteSetup;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using Storeasy.Infrastructure.Persistence.Context;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IFirstRunSetup"/>. The setup runs in one transaction that first takes a PostgreSQL transaction
/// level advisory lock, so two requests (on one or on several instances) cannot both create an administrator: the
/// second one waits, then finds the user of the first and is refused.
/// </summary>
/// <param name="dbContext"></param>
/// <param name="transactionRunner"></param>
/// <param name="userRepository"></param>
/// <param name="organizationRepository"></param>
/// <param name="userManager"></param>
/// <param name="setupCodeStore"></param>
/// <param name="deploymentOptions"></param>
public sealed class FirstRunSetup(StoreasyDbContext dbContext,
                                  ITransactionRunner transactionRunner,
                                  IStoreasyRepositoryBase<User> userRepository,
                                  IStoreasyRepositoryBase<Organization> organizationRepository,
                                  IMilvaUserManager<User, int> userManager,
                                  SetupCodeStore setupCodeStore,
                                  DeploymentOptions deploymentOptions) : IFirstRunSetup
{
    /// <summary>
    /// Key of the advisory lock that serializes the first run setup.
    /// </summary>
    public const long SetupLockKey = 0x53544F5245415331; // "STOREAS1"

    private readonly StoreasyDbContext _dbContext = dbContext;
    private readonly ITransactionRunner _transactionRunner = transactionRunner;
    private readonly IStoreasyRepositoryBase<User> _userRepository = userRepository;
    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = organizationRepository;
    private readonly IMilvaUserManager<User, int> _userManager = userManager;
    private readonly SetupCodeStore _setupCodeStore = setupCodeStore;
    private readonly DeploymentOptions _deploymentOptions = deploymentOptions;

    /// <inheritdoc/>
    public async Task<bool> IsRequiredAsync(CancellationToken cancellationToken)
    {
        if (!_deploymentOptions.IsSingleTeam || _setupCodeStore.Completed)
            return false;

        var anyUser = await _dbContext.Users.IgnoreQueryFilters().AnyAsync(cancellationToken);

        if (anyUser)
            _setupCodeStore.MarkCompleted();

        return !anyUser;
    }

    /// <inheritdoc/>
    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (await IsRequiredAsync(cancellationToken))
            await _setupCodeStore.PrepareAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyCodeAsync(string code)
    {
        // A request can arrive before the startup prepared the code (or after a failed start): prepare it now, which
        // also writes it to the log, so the operator has a code to type.
        await _setupCodeStore.PrepareAsync();

        return await _setupCodeStore.VerifyAsync(code);
    }

    /// <inheritdoc/>
    public async Task<(int UserId, int OrganizationId, string ErrorKey)> CompleteAsync(CompleteSetupCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var user = new User
        {
            UserName = email.ToLowerInvariant(),
            Email = email,
            Name = request.Name.Trim(),
            Surname = string.IsNullOrWhiteSpace(request.Surname) ? null : request.Surname.Trim(),
            UserType = UserType.Manager,
            EmailConfirmed = true,
            LockoutEnabled = true,
        };

        var userNameError = _userManager.ValidateUserName(user.UserName);

        if (userNameError is not null)
            return (0, 0, userNameError);

        // The password policy is checked by CompleteSetupCommandValidator (IdentityPassword).
        _userManager.ConfigureForCreate(ref user, request.Password);

        user.NormalizedEmail = email.MilvaNormalize();

        var organizationName = request.OrganizationName.Trim();
        var slug = OrgRoleRules.ToSlug(organizationName);

        // A name without letters or digits ("!!") has no slug of its own.
        if (slug.Length < 3)
            slug = "team";

        string errorKey = null;
        var organizationId = 0;

        await _transactionRunner.RunAsync(async () =>
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", [SetupLockKey], cancellationToken);

            if (await _dbContext.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
            {
                errorKey = MessageKey.SetupAlreadyCompleted;
                return;
            }

            var superAdminRoleId = await _dbContext.Roles.Where(r => r.Name == nameof(PermissionCatalog.App.SuperAdmin))
                                                         .Select(r => (int?)r.Id)
                                                         .FirstOrDefaultAsync(cancellationToken);

            // The startup seeds the role; without it the instance is not ready.
            if (superAdminRoleId == null)
            {
                errorKey = MessageKey.SetupNotReady;
                return;
            }

            user.RoleRelations = [new() { RoleId = superAdminRoleId.Value }];

            await _userRepository.AddAsync(user, cancellationToken);

            var organization = new Organization
            {
                Name = organizationName,
                Slug = slug,
                Members = [new OrganizationMember { UserId = user.Id, Role = OrgRole.Owner }],
            };

            await _organizationRepository.AddAsync(organization, cancellationToken);

            organizationId = organization.Id;
        });

        if (errorKey != null)
        {
            if (errorKey == MessageKey.SetupAlreadyCompleted)
                _setupCodeStore.MarkCompleted();

            return (0, 0, errorKey);
        }

        await _setupCodeStore.CompleteAsync();

        return (user.Id, organizationId, null);
    }
}
