using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Types.Structs;
using Storeasy.Application.Dtos.FeatureFlagDtos;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Dtos.SettingsDtos;
using Storeasy.Application.Features.Roles.CreateRole;
using Storeasy.Application.Features.Roles.UpdateRole;
using Storeasy.Application.Features.Users.CreateUser;
using Storeasy.Application.Features.Users.UpdateUser;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Platform administration: privilege escalation, role changes, user deletion, languages, status, settings, catalog, flags and health.")]
public class PlatformAdminTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<int> PermissionIdAsync(string permission)
    {
        var parts = permission.Split('.');

        return await _factory.DbAsync(db => db.Permissions.Where(p => p.PermissionGroup == parts[0] && p.Name == parts[1]).Select(p => p.Id).SingleAsync());
    }

    private async Task<int> RoleAsync(TestUser root, params string[] permissions)
    {
        var ids = new List<int>();

        foreach (var permission in permissions)
            ids.Add(await PermissionIdAsync(permission));

        var (http, body) = await root.Client.PostAsync<int>("roles/role", new CreateRoleCommand { Name = $"role{TestApi.Unique()}", PermissionIdList = ids });

        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.Messages?.FirstOrDefault()?.Message);

        return body.Data;
    }

    /// <summary>A manager who manages roles and users but holds nothing else.</summary>
    private async Task<(TestUser Root, TestUser Manager)> LimitedManagerAsync()
    {
        var root = await _factory.LoginAsRootAsync();
        var roleId = await RoleAsync(root, PermissionCatalog.RoleManagement.Create, PermissionCatalog.RoleManagement.Update, PermissionCatalog.RoleManagement.Delete,
                                     PermissionCatalog.UserManagement.Create, PermissionCatalog.UserManagement.Update, PermissionCatalog.UserManagement.Delete);

        return (root, await _factory.CreateAndLoginUserAsync(UserType.Manager, roleId));
    }

    [Fact]
    public async Task LimitedManager_ShouldNotGrantFullPrivilegeOrPermissionsItLacks()
    {
        // Arrange
        var (root, manager) = await LimitedManagerAsync();
        var superAdmin = await PermissionIdAsync(PermissionCatalog.App.SuperAdmin);
        var flagUpdate = await PermissionIdAsync(PermissionCatalog.FeatureFlagManagement.Update);
        var userCreate = await PermissionIdAsync(PermissionCatalog.UserManagement.Create);
        var powerfulRole = await RoleAsync(root, PermissionCatalog.FeatureFlagManagement.Update);

        // Act
        var (full, fullBody) = await manager.Client.PostAsync<int>("roles/role", new CreateRoleCommand { Name = $"r{TestApi.Unique()}", PermissionIdList = [superAdmin] });
        var (lacking, _) = await manager.Client.PostAsync<int>("roles/role", new CreateRoleCommand { Name = $"r{TestApi.Unique()}", PermissionIdList = [flagUpdate] });
        var (held, heldBody) = await manager.Client.PostAsync<int>("roles/role", new CreateRoleCommand { Name = $"r{TestApi.Unique()}", PermissionIdList = [userCreate] });
        var (raise, _) = await manager.Client.PutAsync<int>("roles/role", new UpdateRoleCommand { Id = held.StatusCode == HttpStatusCode.OK ? heldBody.Data : 0, PermissionIdList = new UpdateProperty<List<int>>([userCreate, flagUpdate]) });
        var (editPowerful, _) = await manager.Client.PutAsync<int>("roles/role", new UpdateRoleCommand { Id = powerfulRole, Name = new UpdateProperty<string>("renamed") });
        var (giveRole, _) = await manager.Client.PostAsync<int>("users/user", new CreateUserCommand { UserName = $"u{TestApi.Unique()}", Name = "N", Surname = "S", Password = TestApi.DefaultPassword, UserType = UserType.Manager, RoleIdList = [powerfulRole] });
        var (seed, _) = await manager.Client.PutAsync<int>("roles/role", new UpdateRoleCommand { Id = 1, Name = new UpdateProperty<string>("hijack") });
        var (resetRoot, _) = await manager.Client.PutAsync<int>("users/user", new UpdateUserCommand { Id = 1, NewPassword = new UpdateProperty<string>("Hij4ck!pass") });

        // Assert
        full.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        fullBody.Messages.Should().Contain(m => m.Key == MessageKey.PermissionEscalation);
        lacking.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        held.StatusCode.Should().Be(HttpStatusCode.OK);
        raise.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        editPowerful.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        giveRole.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        seed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resetRoot.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangingARolesPermissions_ShouldEndTheOldTokensOfItsMembers()
    {
        // Arrange
        var root = await _factory.LoginAsRootAsync();
        var roleId = await RoleAsync(root, PermissionCatalog.RuleCatalogManagement.List);
        var member = await _factory.CreateAndLoginUserAsync(UserType.Manager, roleId);
        var (before, _) = await member.Client.GetAsync<PlatformRuleCatalogDto>("rules/platform-catalog");

        // Act
        var (update, _) = await root.Client.PutAsync<int>("roles/role", new UpdateRoleCommand { Id = roleId, PermissionIdList = new UpdateProperty<List<int>>([await PermissionIdAsync(PermissionCatalog.LanguageManagement.List)]) });
        var after = await member.Client.GetAsync($"{TestApi.Base}/rules/platform-catalog", TestContext.Current.CancellationToken);
        var again = await _factory.LoginAsync(member.UserName, TestApi.DefaultPassword);
        var (withNewToken, _) = await again.Client.GetAsync<PlatformRuleCatalogDto>("rules/platform-catalog");

        // Assert
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        withNewToken.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the new token carries the new permissions");
    }

    [Fact]
    public async Task DeletingAUser_ShouldRemoveMemberships_ButNotTheLastOwner()
    {
        // Arrange
        var root = await _factory.LoginAsRootAsync();
        var org = await _factory.NewOrgAsync();
        var member = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Editor);
        var coOwner = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Admin);

        // Act
        var (lastOwner, lastOwnerBody) = await root.Client.DeleteAsync<int>($"users/user?UserId={org.Owner.Id}");
        var (deleteMember, _) = await root.Client.DeleteAsync<int>($"users/user?UserId={member.Id}");
        var memberships = await _factory.DbAsync(db => db.OrganizationMembers.CountAsync(m => m.UserId == member.Id));

        await _factory.DbAsync(db => db.OrganizationMembers.Where(m => m.UserId == coOwner.Id && m.OrganizationId == org.OrganizationId).ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, OrgRole.Owner)));

        var (secondOwnerLeft, _) = await root.Client.DeleteAsync<int>($"users/user?UserId={org.Owner.Id}");

        // Assert
        lastOwner.StatusCode.Should().Be(HttpStatusCode.Conflict);
        lastOwnerBody.Messages.Should().Contain(m => m.Key == MessageKey.UserIsLastOwner && m.Message.Contains("Org"));
        deleteMember.StatusCode.Should().Be(HttpStatusCode.OK);
        memberships.Should().Be(0);
        secondOwnerLeft.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnsupportedLanguage_ShouldNotBecomeTheDefault()
    {
        var root = await _factory.LoginAsRootAsync();
        var language = new Language { Code = $"x{TestApi.Unique()[..5]}", Name = "Test", Supported = false, IsDefault = false };

        await _factory.DbAsync(async db =>
        {
            db.Languages.Add(language);
            await db.SaveChangesAsync();
        });

        var (http, body) = await root.Client.PutAsync<int>("languages/language", new { id = language.Id, isDefault = new { value = true, isUpdated = true } });

        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Messages.Should().Contain(m => m.Key == MessageKey.UnsupportedLanguageCannotBeDefault);
        (await _factory.DbAsync(db => db.Languages.AsNoTracking().SingleAsync(l => l.Id == language.Id))).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SystemStatus_ShouldReportTheDependenciesToManagersOnly()
    {
        var root = await _factory.LoginAsRootAsync();
        var (_, manager) = await LimitedManagerAsync();
        var appUser = await _factory.CreateAndLoginUserAsync();

        var (http, status) = await root.Client.GetAsync<SystemStatusDto>("system/status");
        var (limited, _) = await manager.Client.GetAsync<SystemStatusDto>("system/status");
        var (user, _) = await appUser.Client.GetAsync<SystemStatusDto>("system/status");

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        status.Data.Database.Healthy.Should().BeTrue();
        status.Data.Redis.Healthy.Should().BeTrue();
        status.Data.EncryptionKeyConfigured.Should().BeTrue();
        status.Data.GoogleSignInConfigured.Should().BeTrue();
        status.Data.SignalRBackplane.Should().Be("Redis");
        status.Data.Version.Should().NotBeNullOrWhiteSpace();
        status.Data.Jobs.Should().NotBeNull();
        limited.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        user.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SystemStatus_ShouldProbeTheSmtpServer()
    {
        // Arrange: one SMTP address that accepts connections and one that refuses them.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var openPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        var closedPort = FreePort();

        // Act
        var reachable = await ProbeSmtpAsync(openPort);
        var refused = await ProbeSmtpAsync(closedPort);

        // Assert
        reachable.Configured.Should().BeTrue();
        reachable.Healthy.Should().BeTrue();
        reachable.Provider.Should().Be(nameof(EmailProvider.Smtp));
        refused.Healthy.Should().BeFalse();
        refused.Error.Should().NotBeNullOrWhiteSpace();
    }

    private async Task<DependencyStatusDto> ProbeSmtpAsync(int port)
    {
        await using var host = _factory.CreateHost(new()
        {
            ["Storeasy:Email:Provider"] = nameof(EmailProvider.Smtp),
            ["Storeasy:Email:Smtp:Host"] = "127.0.0.1",
            ["Storeasy:Email:Smtp:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Storeasy:Email:Smtp:From"] = "status@example.com",
        });
        using var scope = host.Services.CreateScope();
        var status = await scope.ServiceProvider.GetRequiredService<ISystemStatusProbe>().CheckAsync(CancellationToken.None);

        return status.Mail;
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();

        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    [Fact]
    public async Task RegistrationMode_ShouldBeChangedAtRuntimeAndWinOverTheConfiguration()
    {
        var root = await _factory.LoginAsRootAsync();

        try
        {
            var (save, _) = await root.Client.PutAsync<RegistrationMode>("system/settings/registration-mode", new { mode = RegistrationMode.Closed });
            var (_, settings) = await root.Client.GetAsync<SystemSettingsDto>("system/settings");
            var (_, publicSettings) = await _factory.CreateClientWithAddress().GetAsync<PublicSettingsDto>("system/public-settings");
            var (register, _) = await _factory.CreateClientWithAddress().PostAsync<int>("account/register", new { email = $"closed{TestApi.Unique()}@example.com", password = TestApi.DefaultPassword, name = "Ada" });

            save.StatusCode.Should().Be(HttpStatusCode.OK);
            settings.Data.RegistrationMode.Should().Be(RegistrationMode.Closed);
            settings.Data.RegistrationModeSaved.Should().BeTrue();
            settings.Data.ConfiguredRegistrationMode.Should().Be(RegistrationMode.Open);
            publicSettings.Data.RegistrationMode.Should().Be(RegistrationMode.Closed);
            publicSettings.Data.Version.Should().NotBeNullOrWhiteSpace();
            register.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await root.Client.PutAsync<RegistrationMode>("system/settings/registration-mode", new { mode = (RegistrationMode?)null });
        }

        var (_, restored) = await root.Client.GetAsync<SystemSettingsDto>("system/settings");

        restored.Data.RegistrationModeSaved.Should().BeFalse();
        restored.Data.RegistrationMode.Should().Be(RegistrationMode.Open);
    }

    [Fact]
    public async Task PlatformCatalog_FeatureFlagDescriptions_AndHealthAliases_ShouldWork()
    {
        var root = await _factory.LoginAsRootAsync();

        var (_, catalog) = await root.Client.GetAsync<PlatformRuleCatalogDto>("rules/platform-catalog");
        var (_, flags) = await root.Client.PatchListAsync<FeatureFlagListDto>("featureFlags", new ListRequest { PageNumber = 1, RowCount = 10 });
        var live = await _factory.CreateClient().GetAsync("/health/live", TestContext.Current.CancellationToken);
        var ready = await _factory.CreateClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);

        catalog.Data.Catalog.Version.Should().NotBeNullOrWhiteSpace();
        catalog.Data.Rules.Should().NotBeEmpty().And.OnlyContain(r => r.AffectedAppCount == 0);
        flags.Data.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.Description) && !f.Description.StartsWith("FeatureFlagDescription"));
        live.StatusCode.Should().Be(HttpStatusCode.OK);
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
