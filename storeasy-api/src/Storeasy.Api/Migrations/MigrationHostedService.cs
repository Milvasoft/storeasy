using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.Helpers;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Infrastructure.Persistence;
using Storeasy.Infrastructure.Persistence.Context;
using Serilog;

namespace Storeasy.Api.Migrations;

/// <summary>
/// Applies migrations when the application starts, prepares the static data of a single team instance, creates the
/// missing feature flag rows, then loads the language and permission lists the app reads from memory and, while a single
/// team instance has no user, prepares the setup code. Every step is idempotent, so it is safe on every startup.
/// </summary>
/// <param name="scopeFactory"></param>
public class MigrationHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    /// <summary>
    /// Starts hosted service.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<StoreasyDbContext>();

        try
        {
            Log.Logger.Information("Applying migrations..");

            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);

            if (!pendingMigrations.IsNullOrEmpty())
            {
                Log.Logger.Information("Applying {Count} pending migration(s)...", pendingMigrations.Count());

                await context.Database.MigrateAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "An error occurred while applying migrations.");
        }

        var deploymentOptions = scope.ServiceProvider.GetRequiredService<DeploymentOptions>();

        if (deploymentOptions.IsSingleTeam)
        {
            try
            {
                // Single team: no developer step. The static data (SuperAdmin role, triggers, languages, permissions) is
                // created when missing, under an advisory lock so instances that start together do it once.
                var permissionManager = scope.ServiceProvider.GetRequiredService<IPermissionManager>();

                if (await new DatabaseMigrator(scope.ServiceProvider).EnsureStaticDataAsync(permissionManager, cancellationToken))
                    Log.Logger.Information("The database was prepared for its first run.");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "An error occurred while preparing the database.");
            }
        }

        try
        {
            // Every switch in FeatureFlagKeys gets its row, so it can be toggled from the management endpoints.
            await FeatureFlagSeeder.SeedAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "An error occurred while creating the feature flags.");
        }

        try
        {
            // Runs after the migration, so a fresh database already has the tables.
            await LanguageSeeder.LoadAsync(context, cancellationToken);

            if (PermissionCatalog.Permissions.IsEmpty)
            {
                var permissionManager = scope.ServiceProvider.GetRequiredService<IPermissionManager>();

                var permissions = await permissionManager.GetAllPermissionsAsync(cancellationToken);

                foreach (var permission in permissions)
                    PermissionCatalog.Permissions.Add(permission);
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "An error occurred while initializing language and permission lists.");
        }

        if (deploymentOptions.IsSingleTeam)
        {
            try
            {
                // While the instance has no user: the one time setup code, written to the log once.
                await scope.ServiceProvider.GetRequiredService<IFirstRunSetup>().PrepareAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "An error occurred while preparing the first run setup.");
            }
        }
    }

    /// <summary>
    /// Stops hosted service.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}