using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.Caching.Redis.Accessor;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.AppStartup;
using Storeasy.Application.Dtos.AccountDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Dtos.SettingsDtos;
using Storeasy.Application.Features.Account.Register;
using Storeasy.Application.Features.Organizations.CreateOrganization;
using Storeasy.Application.Features.Settings.CompleteSetup;
using Storeasy.Application.Features.Settings.UpdateRegistrationMode;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using Storeasy.Infrastructure.Persistence;
using Storeasy.Infrastructure.Persistence.Context;
using Storeasy.IntegrationTests.TestBase;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Storeasy.IntegrationTests.ControllersTests;

/// <summary>
/// Single team mode (<c>Storeasy:Mode=SingleTeam</c>, the default of a deployment): the database is prepared on startup
/// without a developer step, the first run setup needs the one time code, there is one organization and no sign up.
/// Every test builds its own host on its own database, because the shared host runs in multi tenant mode.
/// </summary>
[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Integration tests for the single team mode and the first run setup.")]
public partial class SingleTeamTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private const string _setupCode = "TEST-SETUP-CODE-0001";
    private const string _adminPassword = "Setup1234!";

    [GeneratedRegex("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$")]
    private static partial Regex GeneratedCodeFormat();

    #region Startup

    [Fact]
    public async Task Startup_OnABlankDatabase_ShouldMigrateAndSeedTheStaticDataWithoutARootUser_AndStartAgainIdempotently()
    {
        // Arrange
        var connectionString = await _factory.CreateBlankDatabaseAsync();

        // Act: the first start prepares the database; a second start on the same database finds it prepared.
        StaticData first;

        await using (var host = CreateSingleTeamHost(connectionString))
            first = await ReadStaticDataAsync(host);

        StaticData second;

        await using (var host = CreateSingleTeamHost(connectionString))
            second = await ReadStaticDataAsync(host);

        // Assert
        first.Initialized.Should().BeTrue();
        first.SuperAdminRoles.Should().Be(1);
        first.Permissions.Should().BeGreaterThan(1);
        first.Languages.Should().BeGreaterThan(0);
        first.Triggers.Should().BeGreaterThan(0);
        first.Users.Should().Be(0, "a single team instance has no root user, the first run setup creates the administrator");
        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task EnsureStaticData_RunByTwoInstancesAtOnce_ShouldPrepareTheDatabaseOnce()
    {
        // Arrange: the schema exists but nothing is seeded; a multi tenant host does not prepare it on startup.
        var connectionString = await _factory.CreateEmptyDatabaseAsync();

        await using var host = _factory.CreateHost(new() { [StoreasyApiFactory.ConnectionStringKey] = connectionString });

        // Act: two scopes stand for two instances that start together.
        async Task<bool> PrepareAsync()
        {
            using var scope = host.Services.CreateScope();

            return await new DatabaseMigrator(scope.ServiceProvider).EnsureStaticDataAsync(scope.ServiceProvider.GetRequiredService<IPermissionManager>(), TestContext.Current.CancellationToken);
        }

        var results = await Task.WhenAll(Task.Run(PrepareAsync, TestContext.Current.CancellationToken), Task.Run(PrepareAsync, TestContext.Current.CancellationToken));

        var data = await ReadStaticDataAsync(host);

        // Assert: one of them prepared it, the other waited and found it prepared.
        results.Should().BeEquivalentTo([true, false]);
        data.SuperAdminRoles.Should().Be(1);
        data.Initialized.Should().BeTrue();
        data.Users.Should().Be(0);
    }

    #endregion

    #region First run setup

    [Fact]
    public async Task PublicSettings_BeforeAndAfterTheSetup_ShouldReportTheModeAndWhetherTheSetupIsRequired()
    {
        // Arrange
        await using var host = CreateSingleTeamHost(await _factory.CreateBlankDatabaseAsync(), _setupCode);

        // Act
        var (_, before) = await host.CreateClientWithAddress().GetAsync<PublicSettingsDto>("system/public-settings");

        await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", NewSetup(_setupCode));

        var (_, after) = await host.CreateClientWithAddress().GetAsync<PublicSettingsDto>("system/public-settings");

        // Assert: registration is invite only whatever is configured.
        before.Data.Mode.Should().Be(DeploymentMode.SingleTeam);
        before.Data.SetupRequired.Should().BeTrue();
        before.Data.RegistrationMode.Should().Be(RegistrationMode.InviteOnly);
        after.Data.SetupRequired.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("WRONG-CODE-0000")]
    public async Task Setup_WithoutTheRightCode_ShouldBeRefusedAndCreateNothing(string code)
    {
        // Arrange
        await using var host = CreateSingleTeamHost(await _factory.CreateBlankDatabaseAsync(), _setupCode);

        // Act
        var (http, body) = await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", NewSetup(code));

        // Assert
        http.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
        body.IsSuccess.Should().BeFalse();
        (await CountAsync(host, db => db.Users.IgnoreQueryFilters().CountAsync())).Should().Be(0);
        (await CountAsync(host, db => db.Organizations.IgnoreQueryFilters().CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Setup_WithTheGeneratedCode_ShouldAcceptItInAnyCaseAndWithoutDashes()
    {
        // Arrange: no configured code, so the startup generated one; it is read from Redis like a second instance would.
        await using var host = CreateSingleTeamHost(await _factory.CreateBlankDatabaseAsync());

        var code = await ReadSharedSetupCodeAsync(host);

        // Act
        var (http, body) = await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", NewSetup(code.Replace("-", string.Empty).ToLowerInvariant()));

        // Assert
        code.Should().MatchRegex(GeneratedCodeFormat().ToString());
        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());
        (await ReadSharedSetupCodeAsync(host)).Should().BeNull("the code is forgotten once the setup is completed");
    }

    [Fact]
    public async Task Setup_WithTheRightCode_ShouldCreateTheVerifiedAdministratorAndTheTeamAndSignIn()
    {
        // Arrange
        await using var host = CreateSingleTeamHost(await _factory.CreateBlankDatabaseAsync(), _setupCode);

        var setup = NewSetup(_setupCode);

        // Act
        var (http, body) = await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", setup);

        var client = host.CreateClientWithAddress().UseAccessToken(body.Data.Token.AccessToken);

        var (organizationsHttp, organizations) = await client.GetAsync<List<MyOrganizationDto>>("organizations/mine");
        var (loginHttp, _) = await host.CreateClientWithAddress().RequestLoginAsync(setup.Email, _adminPassword);

        // Assert: the response signs in; the user is the platform administrator and the owner of the single team.
        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());
        body.Data.UserType.Should().Be(UserType.Manager);
        body.Data.Token.AccessToken.Should().NotBeNullOrWhiteSpace();
        organizationsHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        organizations.Data.Should().ContainSingle().Which.Name.Should().Be(setup.OrganizationName);
        organizations.Data[0].Role.Should().Be(OrgRole.Owner);
        loginHttp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = host.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<StoreasyDbContext>();

        var user = await db.Users.AsNoTracking()
                                 .Where(u => u.Id == body.Data.Id)
                                 .Select(u => new { u.UserName, u.Email, u.EmailConfirmed, u.UserType, Roles = u.RoleRelations.Select(r => r.Role.Name).ToList() })
                                 .SingleAsync(TestContext.Current.CancellationToken);

        user.UserName.Should().Be(setup.Email.ToLowerInvariant());
        user.EmailConfirmed.Should().BeTrue();
        user.UserType.Should().Be(UserType.Manager);
        user.Roles.Should().Equal(nameof(PermissionCatalog.App.SuperAdmin));
        (await db.Users.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Setup_AfterItWasCompleted_ShouldBeRefusedForever()
    {
        // Arrange
        await using var host = CreateSingleTeamHost(await _factory.CreateBlankDatabaseAsync(), _setupCode);

        await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", NewSetup(_setupCode));

        // Act: the right code again, with another e-mail.
        var (http, body) = await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", NewSetup(_setupCode));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Messages.Should().Contain(m => m.Key == MessageKey.SetupAlreadyCompleted);
        (await CountAsync(host, db => db.Users.IgnoreQueryFilters().CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Setup_InMultiTenantMode_ShouldBe404()
    {
        // Act: the shared host runs in multi tenant mode.
        var (http, body) = await _factory.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", NewSetup(_setupCode));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region One team, no sign up

    [Fact]
    public async Task CreateOrganization_WhenTheTeamExists_ShouldBeRefused()
    {
        // Arrange
        await using var host = CreateSingleTeamHost(await _factory.CreateBlankDatabaseAsync(), _setupCode);

        var (_, setup) = await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", NewSetup(_setupCode));

        var client = host.CreateClientWithAddress().UseAccessToken(setup.Data.Token.AccessToken);

        // Act
        var (http, body) = await client.PostAsync<int>("organizations/organization", new CreateOrganizationCommand { Name = "Second team" });

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Messages.Should().Contain(m => m.Key == MessageKey.SingleTeamOrganizationExists);
        (await CountAsync(host, db => db.Organizations.IgnoreQueryFilters().CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task Register_WithoutAnInvitation_ShouldBeRefusedEvenWhenOpenRegistrationIsConfigured()
    {
        // Arrange
        await using var host = CreateSingleTeamHost(await _factory.CreateBlankDatabaseAsync(), _setupCode, new() { ["Storeasy:Registration:Mode"] = "Open" });

        var (_, setup) = await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", NewSetup(_setupCode));

        var admin = host.CreateClientWithAddress().UseAccessToken(setup.Data.Token.AccessToken);

        // Act
        var (registerHttp, register) = await host.CreateClientWithAddress().PostAsync<int>("account/register", new RegisterCommand
        {
            Email = $"stranger-{TestApi.Unique()}@example.com",
            Password = TestApi.DefaultPassword,
            Name = "Stranger",
        });

        var (modeHttp, mode) = await admin.PutAsync<RegistrationMode>("system/settings/registration-mode", new UpdateRegistrationModeCommand { Mode = RegistrationMode.Open });

        // Assert: people join the team by invitation, and the mode cannot be opened.
        registerHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        register.Messages.Should().Contain(m => m.Key == MessageKey.RegistrationInvitationRequired);
        modeHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        mode.Messages.Should().Contain(m => m.Key == MessageKey.RegistrationModeFixedInSingleTeam);
    }

    [Fact]
    public async Task ExistingInstall_WithAManagerAndNoOrganization_ShouldSkipTheSetupAndLetTheManagerCreateTheTeamOnce()
    {
        // Arrange: a database initialized by database/init (root user only, no organization), like an install made
        // before the single team mode, then started in single team mode.
        var connectionString = await _factory.CreateEmptyDatabaseAsync();

        var initKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        string rootPassword;

        await using (var initHost = _factory.CreateHost(new() { [StoreasyApiFactory.ConnectionStringKey] = connectionString, [StoreasyApiFactory.BootstrapInitKeyKey] = initKey }))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestApi.Base}/developer/database/init");

            request.Headers.Add("X-Storeasy-Init-Key", initKey);

            var response = await initHost.CreateClientWithAddress().SendAsync(request, TestContext.Current.CancellationToken);

            rootPassword = (await TestApi.ReadAsync<Response<string>>(response)).Data;
        }

        await using var host = CreateSingleTeamHost(connectionString, _setupCode);

        // Act
        var (_, settings) = await host.CreateClientWithAddress().GetAsync<PublicSettingsDto>("system/public-settings");
        var (setupHttp, _) = await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("system/setup", NewSetup(_setupCode));

        var root = await host.LoginAsync(GlobalConstant.RootUsername, rootPassword);

        var (_, before) = await root.Client.GetAsync<List<MyOrganizationDto>>("organizations/mine");
        var (createHttp, created) = await root.Client.PostAsync<int>("organizations/organization", new CreateOrganizationCommand { Name = "Our team" });
        var (secondHttp, _) = await root.Client.PostAsync<int>("organizations/organization", new CreateOrganizationCommand { Name = "Another team" });
        var (_, after) = await root.Client.GetAsync<List<MyOrganizationDto>>("organizations/mine");

        // Assert: no setup screen (a user exists); the manager has no organization yet, creates the team once and owns it.
        settings.Data.SetupRequired.Should().BeFalse();
        setupHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        before.Data.Should().BeEmpty();
        createHttp.StatusCode.Should().Be(HttpStatusCode.OK, created?.MessagesToString());
        secondHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        after.Data.Should().ContainSingle().Which.Role.Should().Be(OrgRole.Owner);
    }

    #endregion

    #region Helpers

    private sealed record StaticData(bool Initialized, int SuperAdminRoles, int Permissions, int Languages, int Triggers, int Users);

    private WebApplicationFactory<IApiMarker> CreateSingleTeamHost(string connectionString, string setupCode = null, Dictionary<string, string> extra = null)
    {
        var overrides = new Dictionary<string, string>
        {
            [StoreasyApiFactory.ConnectionStringKey] = connectionString,
            [StoreasyApiFactory.DeploymentModeKey] = nameof(DeploymentMode.SingleTeam),
            [StoreasyApiFactory.BootstrapSetupCodeKey] = setupCode ?? string.Empty,

            // Its own Redis keys: the shared setup code of this host never meets another host's.
            [StoreasyApiFactory.CacheKeyPrefixKey] = $"single-{TestApi.Unique()}",
        };

        foreach (var (key, value) in extra ?? [])
            overrides[key] = value;

        return _factory.CreateHost(overrides);
    }

    private static CompleteSetupCommand NewSetup(string code) => new()
    {
        SetupCode = code,
        Name = "Ada",
        Surname = "Admin",
        Email = $"Owner-{TestApi.Unique()}@Example.com",
        Password = _adminPassword,
        OrganizationName = "Acme Mobile",
        DeviceId = "setup-device",
    };

    private static async Task<StaticData> ReadStaticDataAsync(WebApplicationFactory<IApiMarker> host)
    {
        using var scope = host.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<StoreasyDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var initialized = await db.MigrationHistory.AnyAsync(m => m.MigrationCompleted, cancellationToken);
        var roles = await db.Roles.IgnoreQueryFilters().CountAsync(r => r.Name == nameof(PermissionCatalog.App.SuperAdmin), cancellationToken);
        var permissions = await db.Permissions.IgnoreQueryFilters().CountAsync(cancellationToken);
        var languages = await db.Languages.IgnoreQueryFilters().CountAsync(cancellationToken);
        var users = await db.Users.IgnoreQueryFilters().CountAsync(cancellationToken);
        var triggers = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_trigger WHERE tgname = 'prevent_seed_modification_trigger'").SingleAsync(cancellationToken);

        return new StaticData(initialized, roles, permissions, languages, triggers, users);
    }

    private static async Task<int> CountAsync(WebApplicationFactory<IApiMarker> host, Func<StoreasyDbContext, Task<int>> count)
    {
        using var scope = host.Services.CreateScope();

        return await count(scope.ServiceProvider.GetRequiredService<StoreasyDbContext>());
    }

    private static async Task<string> ReadSharedSetupCodeAsync(WebApplicationFactory<IApiMarker> host)
    {
        var cache = host.Services.GetRequiredService<IRedisAccessor>();
        var keys = host.Services.GetRequiredService<CacheKeys>();

        var value = await cache.GetDatabase().StringGetAsync(keys.SetupCode());

        return value.HasValue ? value.ToString() : null;
    }

    #endregion
}
