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
        var superAdminPermission = CreateSuperAdminPermission();

        await _dbContext.Permissions.AddAsync(superAdminPermission, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var superAdminRole = CreateSuperAdminRole(superAdminPermission.Id);

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
    /// Key of the session level advisory lock that serializes <see cref="EnsureStaticDataAsync"/> across instances.
    /// </summary>
    public const long StaticDataLockKey = 0x53544F5245415330; // "STOREAS0"

    /// <summary>
    /// Prepares a database for a single team instance on startup, without a developer step and without the root user:
    /// the SuperAdmin permission and role, the seed protection triggers, the languages and the permissions of the
    /// catalog, then marks the database initialized like <see cref="InitDatabaseAsync"/> does. Every part is created
    /// only when it is missing, so it is idempotent and safe on a database that <c>database/init</c> initialized.
    /// Instances that start together wait for each other on a PostgreSQL advisory lock held on this context's
    /// connection. Returns true when the database was initialized by this call.
    /// </summary>
    /// <param name="permissionManager"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> EnsureStaticDataAsync(IPermissionManager permissionManager, CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock({0})", [StaticDataLockKey], cancellationToken);

            try
            {
                return await EnsureStaticDataLockedAsync(permissionManager, cancellationToken);
            }
            finally
            {
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", [StaticDataLockKey], CancellationToken.None);
            }
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task<bool> EnsureStaticDataLockedAsync(IPermissionManager permissionManager, CancellationToken cancellationToken)
    {
        var initialMigrationSql = await File.ReadAllTextAsync(Path.Combine(GlobalConstant.SqlFilesPath, "initial_migration_fetch.sql"), cancellationToken);

        var initialMigration = await _dbContext.Database.SqlQueryRaw<EfMigrationHistory>(initialMigrationSql).FirstOrDefaultAsync(cancellationToken)
                               ?? throw new InvalidOperationException("The initial migration is not applied; the database cannot be prepared.");

        var migrationLog = await _dbContext.MigrationHistory.AsNoTracking().FirstOrDefaultAsync(m => m.MigrationId == initialMigration.MigrationId, cancellationToken);

        if (migrationLog?.MigrationCompleted ?? false)
        {
            // Initialized before (by an earlier start or database/init): only the catalog may have new permissions.
            await MigratePermissionsAsync(permissionManager, cancellationToken);

            return false;
        }

        // The seed rows have fixed ids below 21, so they go in before the triggers that protect that band.
        if (!await _dbContext.Permissions.IgnoreQueryFilters().AnyAsync(p => p.Id == 1, cancellationToken))
        {
            await _dbContext.Permissions.AddAsync(CreateSuperAdminPermission(), cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await _dbContext.Roles.IgnoreQueryFilters().AnyAsync(r => r.Id == 1, cancellationToken))
        {
            await _dbContext.Roles.AddAsync(CreateSuperAdminRole(1), cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var triggersExist = await _dbContext.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'prevent_seed_modification_trigger') AS \"Value\"")
                                                     .SingleAsync(cancellationToken);

        if (!triggersExist)
            await CreateTriggersAsync(cancellationToken);

        await LanguageSeeder.SeedAsync(_dbContext, cancellationToken);

        await MigratePermissionsAsync(permissionManager, cancellationToken);

        if (migrationLog is null)
        {
            await _dbContext.MigrationHistory.AddAsync(new MigrationHistory
            {
                MigrationId = initialMigration.MigrationId,
                MigrationCompleted = true
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await _dbContext.MigrationHistory.Where(m => m.MigrationId == migrationLog.MigrationId)
                                             .ExecuteUpdateAsync(i => i.SetProperty(x => x.MigrationCompleted, true), cancellationToken: cancellationToken);
        }

        return true;
    }

    private static Permission CreateSuperAdminPermission() => new()
    {
        Id = 1,
        Name = nameof(PermissionCatalog.App.SuperAdmin),
        Description = "Provides access to the entire system.",
        NormalizedName = nameof(PermissionCatalog.App.SuperAdmin).MilvaNormalize(),
        PermissionGroup = nameof(PermissionCatalog.App),
        PermissionGroupDescription = "Application-wide permissions."
    };

    private static Role CreateSuperAdminRole(int permissionId) => new()
    {
        Id = 1,
        Name = nameof(PermissionCatalog.App.SuperAdmin),
        CreationDate = DateTime.UtcNow,
        CreatorUserName = GlobalConstant.SystemUsername,
        RolePermissionRelations =
        [
            new()
            {
                PermissionId = permissionId,
                RoleId = 1
            }
        ]
    };

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
