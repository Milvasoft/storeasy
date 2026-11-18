using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.Attributes.Annotations;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Types.Structs;
using Storeasy.Application.Features.Roles.CreateRole;
using Storeasy.Application.Features.Roles.UpdateRole;
using Storeasy.Application.Features.Users.CreateUser;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain;
using Storeasy.Infrastructure.Persistence;
using Storeasy.Infrastructure.Persistence.Context;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Developer service. Every operation runs only when <c>MILVA_ENV</c> is exactly <c>dev</c>
/// (<see cref="StoreasyExtensions.IsCurrentEnvDevelopment"/>); in any other environment (<c>test</c>, <c>prod</c>, empty,
/// unknown) it answers <see cref="MessageKey.PageNotFound"/>.
/// </summary>
/// <param name="serviceProvider"></param>
public class DeveloperService(IServiceProvider serviceProvider) : IDeveloperService
{
    /// <summary>
    /// Password of every user <see cref="SeedDevelopmentDataAsync"/> creates (the root user included). It satisfies the
    /// default password policy (<c>Milvasoft:Identity:Password</c>); with a stricter policy the seed fails with the
    /// validation message instead of reporting success over users that were never created.
    /// </summary>
    public const string DevelopmentPassword = "Dev12345!";

    private readonly IMediator _mediator = serviceProvider.GetService<IMediator>();
    private readonly IPermissionManager _permissionManager = serviceProvider.GetService<IPermissionManager>();
    private readonly StoreasyDbContext _storeasyDbContext = serviceProvider.GetService<StoreasyDbContext>();
    private readonly DatabaseMigrator _databaseMigrator = new(serviceProvider);
    private readonly BootstrapOptions _bootstrapOptions = serviceProvider.GetService<BootstrapOptions>() ?? new BootstrapOptions();
    private readonly IHttpContextAccessor _httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();

    /// <summary>
    /// Remove, recreates and seed database for development purposes.
    /// </summary>
    /// <returns></returns>
    [ExcludeFromMetadata]
    public async Task<Response> ResetDatabaseAsync()
    {
        if (!StoreasyExtensions.IsCurrentEnvDevelopment())
            return Response.Error(MessageKey.PageNotFound);

        return await _databaseMigrator.ResetDatabaseAsync(default);
    }

    /// <summary>
    /// Seeds data for development purposes: the <c>Viewer</c> role and the <c>admin</c> and <c>viewer</c> users. The
    /// endpoint needs a super admin token, so the database is normally initialized already (<c>database/init</c>, root
    /// user with its generated password); only when the root user is missing are the defaults seeded here too, with
    /// <see cref="DevelopmentPassword"/> as the root password.
    /// </summary>
    /// <returns></returns>
    public async Task<Response> SeedDevelopmentDataAsync()
    {
        if (!StoreasyExtensions.IsCurrentEnvDevelopment())
            return Response.Error(MessageKey.PageNotFound);

        try
        {
            var initialized = await _storeasyDbContext.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == 1);

            if (!initialized)
            {
                await _databaseMigrator.SeedDefaultDataAsync(DevelopmentPassword);

                await _databaseMigrator.CreateTriggersAsync(default);

                await DatabaseMigrator.MigratePermissionsAsync(_permissionManager, default);

                await LanguageSeeder.SeedAsync(_storeasyDbContext, default);
            }

            // Viewer role: every list permission of the catalog, looked up by name (the ids depend on the catalog order).
            // Every step below returns its own failure (validation messages included) instead of being ignored.
            var listPermissionIds = await _storeasyDbContext.Permissions.Where(p => p.Name == nameof(PermissionCatalog.UserManagement.List))
                                                                         .Select(p => p.Id)
                                                                         .ToListAsync();

            var addedRole = await _mediator.Send(new CreateRoleCommand
            {
                Name = "Viewer"
            });

            if (!addedRole.IsSuccess)
                return addedRole;

            var updatedRole = await _mediator.Send(new UpdateRoleCommand
            {
                Id = addedRole.Data,
                PermissionIdList = new UpdateProperty<List<int>>
                {
                    IsUpdated = true,
                    Value = listPermissionIds
                }
            });

            if (!updatedRole.IsSuccess)
                return updatedRole;

            //Another Super Admin User creation
            var addedAdmin = await _mediator.Send(new CreateUserCommand
            {
                Name = "Admin",
                Surname = "User",
                UserType = Domain.Enums.UserType.Manager,
                UserName = "admin",
                Email = "admin@example.com",
                Password = DevelopmentPassword,
                RoleIdList = [1]
            });

            if (!addedAdmin.IsSuccess)
                return addedAdmin;

            //Viewer User creation
            var addedViewer = await _mediator.Send(new CreateUserCommand
            {
                Name = "Viewer",
                Surname = "User",
                UserName = "viewer",
                UserType = Domain.Enums.UserType.AppUser,
                Email = "viewer@example.com",
                Password = DevelopmentPassword,
                RoleIdList = [addedRole.Data],
            });

            if (!addedViewer.IsSuccess)
                return addedViewer;

            return Response.Success();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return Response.Error("Already seeded!");
        }
    }

    /// <summary>
    /// Seeds fake data.
    /// </summary>
    /// <param name="sameData"></param>
    /// <param name="locale"></param>
    /// <returns></returns>
    public async Task<Response> SeedFakeDataAsync(bool sameData = true, string locale = "tr")
    {
        if (!StoreasyExtensions.IsCurrentEnvDevelopment())
            return Response.Error(MessageKey.PageNotFound);

        await _databaseMigrator.SeedFakeDataAsync(sameData, locale, default);

        return Response.Success();
    }

    /// <summary>
    /// Initial migration operation: seeds the root user and returns its generated password once. Runs only when all of
    /// these hold: <c>MILVA_ENV</c> is <c>dev</c>, <see cref="BootstrapOptions.InitKey"/> is configured (at least
    /// <see cref="BootstrapOptions.MinimumInitKeyLength"/> characters), the request carries it in
    /// <see cref="BootstrapOptions.InitKeyHeaderName"/> and the database is not initialized yet. The key is read from the
    /// request here (never passed as an argument), so no interceptor or log ever sees it.
    /// </summary>
    /// <returns></returns>
    [ExcludeFromMetadata]
    public async Task<Response<string>> InitDatabaseAsync()
    {
        // Closed (reported as not found) outside development and while no init key is configured.
        if (!StoreasyExtensions.IsCurrentEnvDevelopment() || !_bootstrapOptions.IsInitKeyConfigured)
            return Response<string>.Error(null, MessageKey.PageNotFound);

        var providedKey = _httpContextAccessor?.HttpContext?.Request.Headers[BootstrapOptions.InitKeyHeaderName].ToString();

        if (!InitKeyMatches(providedKey, _bootstrapOptions.InitKey))
            return Response<string>.Error(null, MessageKey.Unauthorized);

        return await _databaseMigrator.InitDatabaseAsync(_permissionManager, default);
    }

    /// <summary>
    /// Exports existing data to a JSON file.
    /// </summary>
    /// <returns></returns>
    public async Task<Response> ExportExistingDataAsync()
    {
        if (!StoreasyExtensions.IsCurrentEnvDevelopment())
            return Response.Error(MessageKey.PageNotFound);

        var users = _storeasyDbContext.Users.Include(p => p.RoleRelations)
                                             .AsNoTracking()
                                             .AsAsyncEnumerable();

        // The folder has no committed files, so it does not exist in the output until the first export.
        Directory.CreateDirectory(GlobalConstant.JsonFilesPath);

        var filePath = Path.Combine(GlobalConstant.JsonFilesPath, "export.json");

        if (File.Exists(filePath))
            File.Delete(filePath);

        await using var stream = File.Create(filePath);
        using var streamWriter = new StreamWriter(stream, new UTF8Encoding(false));
        using var jsonWriter = new JsonTextWriter(streamWriter)
        {
            Formatting = Formatting.Indented
        };

        var serializer = new Newtonsoft.Json.JsonSerializer
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

        await jsonWriter.WriteStartArrayAsync();

        await foreach (var user in users)
        {
            try
            {
                serializer.Serialize(jsonWriter, user);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Serialize error: {ex.Message}");
            }
        }

        // Dizi kapat
        await jsonWriter.WriteEndArrayAsync();
        await jsonWriter.FlushAsync();
        await streamWriter.FlushAsync();

        return Response.Success();
    }

    /// <summary>
    /// Imports existing data.
    /// </summary>
    /// <returns></returns>
    public async Task<Response> ImportExistingDataAsync()
    {
        if (!StoreasyExtensions.IsCurrentEnvDevelopment())
            return Response.Error(MessageKey.PageNotFound);

        var filePath = Path.Combine(GlobalConstant.JsonFilesPath, "export.json");

        if (!File.Exists(filePath))
            return Response.Error("Cannot find exported file.");

        await using var stream = File.OpenRead(filePath);
        using var jsonDoc = await JsonDocument.ParseAsync(stream);

        if (jsonDoc.RootElement.ValueKind != JsonValueKind.Array)
            return Response.Error("Invalid file format.");

        var options = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNameCaseInsensitive = true
        };

        int importedCount = 0;

        foreach (var element in jsonDoc.RootElement.EnumerateArray())
        {
            try
            {

                importedCount++;
            }
            catch (Exception ex)
            {
                // Logla ve devam et
                Console.WriteLine($"Import error: {ex.Message}");
            }
        }

        await _storeasyDbContext.SaveChangesAsync();
        return Response.Success($"{importedCount} records imported.");
    }

    /// <summary>
    /// Compares the provided init key with the configured one in constant time. Both are hashed first, so neither the
    /// content nor the length of the configured key leaks through the comparison time.
    /// </summary>
    /// <param name="providedKey"></param>
    /// <param name="configuredKey"></param>
    /// <returns></returns>
    private static bool InitKeyMatches(string providedKey, string configuredKey)
    {
        if (string.IsNullOrEmpty(providedKey) || string.IsNullOrEmpty(configuredKey))
            return false;

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));

        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}
