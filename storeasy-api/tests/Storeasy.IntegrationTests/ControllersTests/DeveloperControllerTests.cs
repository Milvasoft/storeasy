﻿using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.AppStartup;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Persistence.Context;
using Storeasy.Infrastructure.Services;
using Storeasy.IntegrationTests.TestBase;
using System.Net;
using System.Security.Cryptography;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Integration tests for the developer (bootstrap) endpoints and the API documentation.")]
public class DeveloperControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private const string _initPath = $"{TestApi.Base}/developer/database/init";
    private const string _documentationPath = $"{GlobalConstant.RoutePrefix}/documentation";
    private const string _openApiPath = $"{GlobalConstant.RoutePrefix}/docs/v1.0/docs.json";

    #region Documentation

    [Fact]
    public async Task Documentation_InDevelopment_ShouldBeServedWithoutAnyToken()
    {
        // Arrange
        var client = _factory.CreateClientWithAddress();

        // Act
        var pageResponse = await client.GetAsync(_documentationPath, TestContext.Current.CancellationToken);
        var page = await pageResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var documentResponse = await client.GetAsync(_openApiPath, TestContext.Current.CancellationToken);
        var document = await documentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert: served, and no JWT of any kind is embedded (developers sign in through account/login and paste their token).
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        documentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Should().NotContain("eyJ");
        document.Should().NotContain("eyJ");
    }

    [Theory]
    [InlineData("test")]
    [InlineData("prod")]
    [InlineData("")]
    [InlineData("Dev")]
    [InlineData("development")]
    public async Task Documentation_OutsideDevelopment_ShouldBe404(string milvaEnv)
    {
        // Arrange: the documentation is mapped at startup, so the host is built with the environment under test.
        await using var host = _factory.CreateHost(new() { [StoreasyApiFactory.MilvaEnvironmentKey] = milvaEnv });

        var client = host.CreateClientWithAddress();

        // Act
        var pageResponse = await client.GetAsync(_documentationPath, TestContext.Current.CancellationToken);
        var page = await pageResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var documentResponse = await client.GetAsync(_openApiPath, TestContext.Current.CancellationToken);

        // Assert
        pageResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        documentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        page.Should().NotContain("eyJ");
    }

    #endregion

    #region Init

    [Fact]
    public async Task InitDatabaseAsync_WithoutAConfiguredKey_ShouldBe404()
    {
        // Arrange
        var connectionString = await _factory.CreateEmptyDatabaseAsync();

        await using var host = _factory.CreateHost(new() { [StoreasyApiFactory.ConnectionStringKey] = connectionString });

        // Act
        var withoutHeader = await host.CreateClientWithAddress().PostAsync(_initPath, null, TestContext.Current.CancellationToken);
        var (withHeaderHttp, withHeader) = await PostInitAsync(host.CreateClientWithAddress(), NewKey());

        // Assert
        withoutHeader.StatusCode.Should().Be(HttpStatusCode.NotFound);
        withHeaderHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        withHeader.IsSuccess.Should().BeFalse();
        withHeader.Data.Should().BeNull();
        (await RootUserExistsAsync(host)).Should().BeFalse();
    }

    [Fact]
    public async Task InitDatabaseAsync_WithAKeyShorterThan32Characters_ShouldBe404()
    {
        // Arrange
        var shortKey = new string('k', BootstrapOptions.MinimumInitKeyLength - 1);

        var connectionString = await _factory.CreateEmptyDatabaseAsync();

        await using var host = CreateInitHost(connectionString, shortKey);

        // Act
        var (http, body) = await PostInitAsync(host.CreateClientWithAddress(), shortKey);

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.IsSuccess.Should().BeFalse();
        (await RootUserExistsAsync(host)).Should().BeFalse();
    }

    [Fact]
    public async Task InitDatabaseAsync_WithAMissingOrWrongKeyHeader_ShouldBe401()
    {
        // Arrange
        var initKey = NewKey();

        var connectionString = await _factory.CreateEmptyDatabaseAsync();

        await using var host = CreateInitHost(connectionString, initKey);

        // Act
        var missing = await host.CreateClientWithAddress().PostAsync(_initPath, null, TestContext.Current.CancellationToken);
        var (wrongHttp, wrongBody) = await PostInitAsync(host.CreateClientWithAddress(), NewKey());
        var (prefixHttp, _) = await PostInitAsync(host.CreateClientWithAddress(), initKey[..^1]);

        // Assert: nothing was initialized.
        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongBody.IsSuccess.Should().BeFalse();
        wrongBody.Data.Should().BeNull();
        prefixHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await RootUserExistsAsync(host)).Should().BeFalse();
    }

    [Fact]
    public async Task InitDatabaseAsync_WithTheRightKeyOnAnEmptyDatabase_ShouldReturnTheRootPasswordOnceAndThenRefuse()
    {
        // Arrange
        var initKey = NewKey();

        var connectionString = await _factory.CreateEmptyDatabaseAsync();

        await using var host = CreateInitHost(connectionString, initKey);

        // Act
        var (firstHttp, first) = await PostInitAsync(host.CreateClientWithAddress(), initKey);
        var (secondHttp, second) = await PostInitAsync(host.CreateClientWithAddress(), initKey);
        var (loginHttp, login) = await host.CreateClientWithAddress().RequestLoginAsync(GlobalConstant.RootUsername, first?.Data);

        // Assert: the generated password signs the root user in; an initialized database is never initialized again.
        firstHttp.StatusCode.Should().Be(HttpStatusCode.OK, first?.MessagesToString());
        first.IsSuccess.Should().BeTrue();
        first.Data.Should().NotBeNullOrWhiteSpace();
        loginHttp.StatusCode.Should().Be(HttpStatusCode.OK, login?.MessagesToString());
        secondHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
        second.IsSuccess.Should().BeFalse();
        second.Data.Should().Be("Already initialized!");
    }

    [Fact]
    public async Task InitDatabaseAsync_OnTheInitializedSharedDatabase_ShouldRefuse()
    {
        // Arrange
        var initKey = NewKey();

        await using var host = CreateInitHost(_factory.ConnectionString, initKey);

        // Act
        var (http, body) = await PostInitAsync(host.CreateClientWithAddress(), initKey);

        // Assert
        http.StatusCode.Should().NotBe(HttpStatusCode.OK);
        body.IsSuccess.Should().BeFalse();
        body.Data.Should().Be("Already initialized!");
    }

    [Theory]
    [InlineData("test")]
    [InlineData("prod")]
    [InlineData("")]
    public async Task InitDatabaseAsync_OutsideDevelopment_ShouldBe404EvenWithTheRightKey(string milvaEnv)
    {
        // Arrange
        var initKey = NewKey();

        var connectionString = await _factory.CreateEmptyDatabaseAsync();

        await using var host = CreateInitHost(connectionString, initKey);

        // Act: the developer service reads MILVA_ENV on every call.
        (HttpResponseMessage Http, Response<string> Body) result;

        using (StoreasyApiFactory.UseMilvaEnvironment(milvaEnv))
            result = await PostInitAsync(host.CreateClientWithAddress(), initKey);

        // Assert
        result.Http.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.Body.IsSuccess.Should().BeFalse();
        result.Body.Data.Should().BeNull();
        (await RootUserExistsAsync(host)).Should().BeFalse();
    }

    #endregion

    #region Other developer endpoints

    [Fact]
    public async Task SeedDataAsync_AfterInit_ShouldCreateTheDevelopmentUsersWithAPasswordThatSignsIn()
    {
        // Arrange: a database initialized with the init key like on a developer machine, then the root user's token.
        var initKey = NewKey();

        var connectionString = await _factory.CreateEmptyDatabaseAsync();

        await using var freshHost = CreateInitHost(connectionString, initKey);

        var (_, init) = await PostInitAsync(freshHost.CreateClientWithAddress(), initKey);

        var root = await freshHost.LoginAsync(GlobalConstant.RootUsername, init.Data);

        // Act
        var seedResponse = await root.Client.PostAsync($"{TestApi.Base}/developer/database/seed", null, TestContext.Current.CancellationToken);
        var seedResult = await TestApi.ReadAsync<Response>(seedResponse);

        var (adminLoginResponse, adminLogin) = await freshHost.CreateClientWithAddress().RequestLoginAsync("admin", DeveloperService.DevelopmentPassword);
        var (viewerLoginResponse, viewerLogin) = await freshHost.CreateClientWithAddress().RequestLoginAsync("viewer", DeveloperService.DevelopmentPassword);

        // Assert: the seed reports success only when every user was created, and each one signs in with the documented password.
        seedResponse.StatusCode.Should().Be(HttpStatusCode.OK, seedResult?.MessagesToString());
        seedResult.IsSuccess.Should().BeTrue();
        adminLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK, adminLogin?.MessagesToString());
        viewerLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK, viewerLogin?.MessagesToString());

        using var scope = freshHost.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<StoreasyDbContext>();

        var users = await dbContext.Users.Where(u => u.UserName == "admin" || u.UserName == "viewer")
                                         .OrderBy(u => u.UserName)
                                         .Select(u => new { u.UserName, u.Email, u.UserType })
                                         .ToListAsync(TestContext.Current.CancellationToken);

        var viewerPermissionNames = await dbContext.RolePermissionRelations.Where(r => r.Role.Name == "Viewer")
                                                                           .Select(r => r.Permission.Name)
                                                                           .Distinct()
                                                                           .ToListAsync(TestContext.Current.CancellationToken);

        users.Should().BeEquivalentTo([new { UserName = "admin", Email = "admin@example.com", UserType = UserType.Manager },
                                       new { UserName = "viewer", Email = "viewer@example.com", UserType = UserType.AppUser }]);
        viewerPermissionNames.Should().Equal(nameof(PermissionCatalog.UserManagement.List));
    }

    [Fact]
    public async Task DeveloperEndpoints_InTestEnvironment_ShouldRefuseEvenASuperAdmin()
    {
        // Arrange: a separate database (a refusal that failed must never reset or seed the shared one) and its root user.
        var initKey = NewKey();

        var connectionString = await _factory.CreateEmptyDatabaseAsync();

        await using var host = CreateInitHost(connectionString, initKey);

        var (_, init) = await PostInitAsync(host.CreateClientWithAddress(), initKey);

        var root = await host.LoginAsync(GlobalConstant.RootUsername, init.Data);

        var calls = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Post, "developer/database/reset"),
            (HttpMethod.Post, "developer/database/seed"),
            (HttpMethod.Post, "developer/database/seed/fake"),
            (HttpMethod.Get, "developer/export/productRelatedData"),
            (HttpMethod.Get, "developer/import/productRelatedData"),
        };

        var statusCodes = new List<string>();

        // Act
        using (StoreasyApiFactory.UseMilvaEnvironment("test"))
        {
            foreach (var (method, path) in calls)
            {
                using var request = new HttpRequestMessage(method, $"{TestApi.Base}/{path}");

                var response = await root.Client.SendAsync(request, TestContext.Current.CancellationToken);

                statusCodes.Add($"{path}: {(int)response.StatusCode}");
            }
        }

        // Assert: every call refused, and the database still holds only the root user.
        statusCodes.Should().Equal(calls.Select(c => $"{c.Path}: 404"));

        using var scope = host.Services.CreateScope();

        var userNames = await scope.ServiceProvider.GetRequiredService<StoreasyDbContext>().Users.Select(u => u.UserName).ToListAsync(TestContext.Current.CancellationToken);

        userNames.Should().Equal(GlobalConstant.RootUsername);
    }

    #endregion

    private WebApplicationFactory<IApiMarker> CreateInitHost(string connectionString, string initKey)
        => _factory.CreateHost(new()
        {
            [StoreasyApiFactory.ConnectionStringKey] = connectionString,
            [StoreasyApiFactory.BootstrapInitKeyKey] = initKey
        });

    private static string NewKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static async Task<(HttpResponseMessage Http, Response<string> Body)> PostInitAsync(HttpClient client, string initKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _initPath);

        request.Headers.Add(BootstrapOptions.InitKeyHeaderName, initKey);

        var http = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (http, await TestApi.ReadAsync<Response<string>>(http));
    }

    private static async Task<bool> RootUserExistsAsync(WebApplicationFactory<IApiMarker> host)
    {
        using var scope = host.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<StoreasyDbContext>().Users.AnyAsync(u => u.Id == 1, TestContext.Current.CancellationToken);
    }
}
