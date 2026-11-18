using Milvasoft.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.Attributes.Annotations;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Helpers;
using Milvasoft.Identity.Abstract;
using Milvasoft.Identity.Concrete;
using Milvasoft.Identity.Concrete.Options;
using Milvasoft.Interception.Ef.Transaction;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain;
using Storeasy.Infrastructure.Persistence.Context;
using System.Net.Sockets;

namespace Storeasy.Infrastructure.Persistence;

/// <summary>
/// Data seed methods.
/// </summary>
/// <param name="serviceProvider"></param>
public class DatabaseMigrator(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly StoreasyDbContext _dbContext = serviceProvider.GetService<StoreasyDbContext>();

    /// <summary>
    /// Remove, recreates and seed database for development purposes.
    /// </summary>
    /// <returns></returns>
    public async Task<Response> ResetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var configuration = _serviceProvider.GetService<IConfiguration>();

        await _dbContext.Database.EnsureDeletedAsync(cancellationToken);

        var connectionString = configuration.GetConnectionString("DefaultConnectionString");

        var opt = new DbContextOptionsBuilder<StoreasyDbContext>()
                    .UseNpgsql(connectionString, b => b.MigrationsHistoryTable(TableNames.EfMigrationHistory).MigrationsAssembly("Storeasy.Api").EnableRetryOnFailure())
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);

        opt.ConfigureWarnings(warnings => { warnings.Log(RelationalEventId.PendingModelChangesWarning); });

        try
        {
            using var db = new StoreasyDbContext(opt.Options);

            await db.Database.MigrateAsync(cancellationToken: cancellationToken);

        }
        catch (Exception ex)
        {
            // Retry
            if (ex.InnerException is SocketException || ex.InnerException is IOException)
            {
                using var db = new StoreasyDbContext(opt.Options);

                await db.Database.MigrateAsync(cancellationToken: cancellationToken);
            }
            else
                throw;
        }

        return Response.Success();
    }

    /// <summary>
    /// Creates default triggers.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task CreateTriggersAsync(CancellationToken cancellationToken)
    {
        var createTriggerSql = await File.ReadAllTextAsync(Path.Combine(GlobalConstant.SqlFilesPath, "create_triggers.sql"), cancellationToken);

        await _dbContext.Database.ExecuteSqlRawAsync(createTriggerSql, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Seeds default production data.
    /// </summary>
    /// <returns></returns>
    public async Task<string> SeedDefaultDataAsync(string rootPass = null, CancellationToken cancellationToken = default)
    {
        var superAdminPermission = new Permission
        {
            Id = 1,
            Name = nameof(PermissionCatalog.App.SuperAdmin),
            Description = "Provides access to the entire system.",
            NormalizedName = nameof(PermissionCatalog.App.SuperAdmin).MilvaNormalize(),
            PermissionGroup = nameof(PermissionCatalog.App),
            PermissionGroupDescription = "Application-wide permissions."
        };

        await _dbContext.Permissions.AddAsync(superAdminPermission, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var superAdminRole = new Role
        {
            Id = 1,
            Name = nameof(PermissionCatalog.App.SuperAdmin),
            CreationDate = DateTime.UtcNow,
            CreatorUserName = GlobalConstant.SystemUsername,
            RolePermissionRelations =
            [
                new()
                {
                    PermissionId = superAdminPermission.Id,
                    RoleId = 1
                }
            ]
        };

        await _dbContext.Roles.AddAsync(superAdminRole, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var rootUser = new User
        {
            Id = 1,
            UserName = GlobalConstant.RootUsername,
            NormalizedUserName = "ROOTUSER",
            Email = string.Empty,
            NormalizedEmail = string.Empty,
            Name = "Administrator",
            Surname = "User",
            UserType = Domain.Enums.UserType.Manager,
            CreationDate = DateTime.UtcNow,
            CreatorUserName = GlobalConstant.SystemUsername,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            TwoFactorEnabled = false,
            LockoutEnabled = false,
            AccessFailedCount = 0,

            // Seeded with its revocation stamp: create_triggers.sql protects this row from updates, so the stamp
            // cannot be assigned on the first login the way it is for every other user.
            SessionStamp = Guid.NewGuid().ToString("N"),
            RoleRelations =
            [
                new()
                {
                    RoleId = superAdminRole.Id
                }
            ]
        };

        rootPass ??= IdentityHelpers.GenerateRandomPassword(new MilvaRandomPaswordGenerationOption
        {
            Length = 16,
        });

        _dbContext.ServiceProvider.GetService<IMilvaUserManager<User, int>>().SetPasswordHash(rootUser, rootPass);

        await _dbContext.Users.AddAsync(rootUser, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return rootPass;
    }

    /// <summary>
    /// Seeds fake data.
    /// </summary>
    /// <returns></returns>
    [Transaction]
    public async Task SeedFakeDataAsync(bool sameData = true, string locale = "tr", CancellationToken cancellationToken = default)
    {
        var roleFaker = new RoleFaker(sameData, locale);

        var roles = roleFaker.Generate(100);

        await _dbContext.BulkInsertAsync(roles, cancellationToken: cancellationToken);

        var userFaker = new UserFaker(sameData, locale, roles);

        var users = userFaker.Generate(250);

        await _dbContext.BulkInsertAsync(users, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Migrate default permissions.
    /// </summary>
    /// <param name="permissionManager"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<Response<string>> MigratePermissionsAsync(IPermissionManager permissionManager, CancellationToken cancellationToken = default) => permissionManager.MigratePermissionsAsync(cancellationToken);

    /// <summary>
    /// Initial data seed and migration operation for production.
    /// </summary>
    /// <returns></returns>
    [ExcludeFromMetadata]
    public async Task<Response<string>> InitDatabaseAsync(IPermissionManager permissionManager, CancellationToken cancellationToken = default)
    {
        try
        {
            var initialMigrationSql = await File.ReadAllTextAsync(Path.Combine(GlobalConstant.SqlFilesPath, "initial_migration_fetch.sql"), cancellationToken);

            var initialMigration = await _dbContext.Database.SqlQueryRaw<EfMigrationHistory>(initialMigrationSql).FirstOrDefaultAsync(cancellationToken);

            if (initialMigration == null)
                return Response<string>.Error("Initial migration cannot found!");

            var migrationLog = await _dbContext.MigrationHistory.FirstOrDefaultAsync(m => m.MigrationId == initialMigration.MigrationId, cancellationToken: cancellationToken);

            if (migrationLog?.MigrationCompleted ?? false)
                return Response<string>.Error("Already initialized!");

            var rootPass = await SeedDefaultDataAsync(cancellationToken: cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await CreateTriggersAsync(cancellationToken);

            await LanguageSeeder.SeedAsync(_dbContext, cancellationToken);

            await MigratePermissionsAsync(permissionManager, cancellationToken);

            if (migrationLog is null)
            {
                migrationLog = new MigrationHistory
                {
                    MigrationId = initialMigration.MigrationId,
                    MigrationCompleted = true
                };

                await _dbContext.MigrationHistory.AddAsync(migrationLog, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await _dbContext.MigrationHistory.Where(m => m.MigrationId == migrationLog.MigrationId)
                                                          .ExecuteUpdateAsync(i => i.SetProperty(x => x.MigrationCompleted, true), cancellationToken: cancellationToken);
            }

            return Response<string>.Success(rootPass);
        }
        catch (Exception)
        {
            return Response<string>.Error("Already initialized!");
        }
    }
}
