using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.AppStartup;
using Storeasy.Application.Dtos.AccountDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Dtos.SettingsDtos;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;
using System.Web;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Sign in with Google: PKCE flow against a fake Google, linking, registration modes and configuration.")]
public class GoogleSignInTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static string NewEmail() => $"g{TestApi.Unique()}@example.com";

    private static string NewSubject() => $"10{TestApi.Unique()}";

    private async Task<(HttpResponseMessage Http, Response<LoginResponseDto> Body)> SignInAsync(WebApplicationFactory<IApiMarker> host, string subject, string email, bool emailVerified = true, string invitationToken = null)
    {
        var client = host.CreateClientWithAddress();
        var (startHttp, start) = await client.PostAsync<GoogleSignInStartDto>("account/login/google/start", new { });

        startHttp.StatusCode.Should().Be(HttpStatusCode.OK);

        var code = _factory.Google.Authorize(start.Data.AuthorizationUrl, subject, email, emailVerified);

        return await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("account/login/google", new { code, state = start.Data.State, deviceId = "browser", invitationToken });
    }

    [Fact]
    public async Task Start_ShouldReturnAPkceAuthorizationAddress()
    {
        var (http, start) = await _factory.CreateClientWithAddress().PostAsync<GoogleSignInStartDto>("account/login/google/start", new { });
        var query = HttpUtility.ParseQueryString(new Uri(start.Data.AuthorizationUrl).Query);
        var (_, settings) = await _factory.CreateClientWithAddress().GetAsync<PublicSettingsDto>("system/public-settings");

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        start.Data.AuthorizationUrl.Should().StartWith(TestBase.Fakes.FakeGoogle.Authority + "/o/oauth2/v2/auth?");
        query["code_challenge_method"].Should().Be("S256");
        query["state"].Should().Be(start.Data.State);
        query["redirect_uri"].Should().EndWith("/auth/google/callback");
        query["scope"].Should().Be("openid email profile");
        query.AllKeys.Should().NotContain("code_verifier");
        settings.Data.GoogleSignInEnabled.Should().BeTrue();
        settings.Data.WebPushPublicKey.Should().Be(StoreasyApiFactory.VapidKeys.PublicKey);
    }

    [Fact]
    public async Task NewAccount_ShouldBeCreatedVerified_AndFoundBySubjectNextTime()
    {
        // Arrange
        var email = NewEmail();
        var subject = NewSubject();

        // Act
        var (first, firstBody) = await SignInAsync(_factory, subject, email);
        var (second, secondBody) = await SignInAsync(_factory, subject, $"changed{TestApi.Unique()}@example.com");
        var user = await _factory.DbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == firstBody.Data.Id));
        var links = await _factory.DbAsync(db => db.UserExternalLogins.AsNoTracking().Where(l => l.UserId == user.Id).ToListAsync());
        var (_, detail) = await _factory.CreateClientWithAddress().UseAccessToken(secondBody.Data.Token.AccessToken).GetAsync<AccountDetailDto>("account/detail");

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        secondBody.Data.Id.Should().Be(firstBody.Data.Id, "the Google subject finds the account even with another address");
        user.Email.Should().Be(email);
        user.EmailConfirmed.Should().BeTrue();
        user.Name.Should().Be("Grace");
        links.Should().ContainSingle(l => l.Provider == ExternalLoginProvider.Google && l.Subject == subject);
        detail.Data.ExternalLogins.Should().Equal(ExternalLoginProvider.Google);
    }

    [Fact]
    public async Task VerifiedExistingAccount_ShouldBeLinked_AndKeepItsPassword()
    {
        // Arrange
        var email = NewEmail();
        var existing = await _factory.CreateUserAsync(email: email);

        // Act
        var (http, body) = await SignInAsync(_factory, NewSubject(), email.ToUpperInvariant());
        var passwordLogin = await _factory.LoginAsync(existing.UserName, TestApi.DefaultPassword);

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data.Id.Should().Be(existing.Id);
        passwordLogin.Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task UnverifiedExistingAccount_ShouldBeTakenOverByGooglesProof()
    {
        // Arrange: someone registered the address without owning it.
        var email = NewEmail();
        var squatter = await _factory.CreateUserAsync(email: email, emailConfirmed: false);
        var squatterSession = await _factory.LoginAsync(squatter.UserName, TestApi.DefaultPassword);

        // Act
        var (http, body) = await SignInAsync(_factory, NewSubject(), email);
        var (oldPassword, _) = await _factory.CreateClientWithAddress().RequestLoginAsync(squatter.UserName, TestApi.DefaultPassword);
        var oldSession = await squatterSession.Client.GetAsync($"{TestApi.Base}/account/detail", TestContext.Current.CancellationToken);
        var row = await _factory.DbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == squatter.Id));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data.Id.Should().Be(squatter.Id);
        oldPassword.StatusCode.Should().NotBe(HttpStatusCode.OK);
        oldSession.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        row.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task AddressGoogleDidNotVerify_ShouldBe403()
    {
        var (http, body) = await SignInAsync(_factory, NewSubject(), NewEmail(), emailVerified: false);

        http.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Messages.Should().Contain(m => m.Key == MessageKey.GoogleEmailNotVerified);
    }

    [Fact]
    public async Task State_ShouldWorkOnce()
    {
        // Arrange
        var client = _factory.CreateClientWithAddress();
        var (_, start) = await client.PostAsync<GoogleSignInStartDto>("account/login/google/start", new { });
        var code = _factory.Google.Authorize(start.Data.AuthorizationUrl, NewSubject(), NewEmail());

        // Act
        var (first, _) = await client.PostAsync<LoginResponseDto>("account/login/google", new { code, state = start.Data.State });
        var (replay, replayBody) = await client.PostAsync<LoginResponseDto>("account/login/google", new { code, state = start.Data.State });
        var (unknown, _) = await client.PostAsync<LoginResponseDto>("account/login/google", new { code = "x", state = "never-issued" });

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        replayBody.Messages.Should().Contain(m => m.Key == MessageKey.GoogleSignInStateInvalid);
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InviteOnly_ShouldNeedAnInvitationForTheSameAddress_AndJoinIt()
    {
        // Arrange
        await using var host = _factory.CreateHost(new() { ["Storeasy:Registration:Mode"] = nameof(RegistrationMode.InviteOnly) });
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var invitee = NewEmail();
        var (_, invitation) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = organization.Data, email = invitee, role = OrgRole.Editor });

        // Act
        var (withoutInvitation, withoutBody) = await SignInAsync(host, NewSubject(), NewEmail());
        var (withInvitation, withBody) = await SignInAsync(host, NewSubject(), invitee, invitationToken: invitation.Data.Token);
        var membership = await _factory.DbAsync(db => db.OrganizationMembers.AsNoTracking().SingleOrDefaultAsync(m => m.OrganizationId == organization.Data && m.UserId == withBody.Data.Id));

        // Assert
        withoutInvitation.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        withoutBody.Messages.Should().Contain(m => m.Key == MessageKey.RegistrationInvitationRequired);
        withInvitation.StatusCode.Should().Be(HttpStatusCode.OK);
        membership.Role.Should().Be(OrgRole.Editor);
    }

    [Fact]
    public async Task Closed_ShouldCreateNoAccountButLetExistingAccountsIn()
    {
        await using var host = _factory.CreateHost(new() { ["Storeasy:Registration:Mode"] = nameof(RegistrationMode.Closed) });
        var email = NewEmail();
        var existing = await _factory.CreateUserAsync(email: email);

        var (newcomer, newcomerBody) = await SignInAsync(host, NewSubject(), NewEmail());
        var (member, memberBody) = await SignInAsync(host, NewSubject(), email);

        newcomer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        newcomerBody.Messages.Should().Contain(m => m.Key == MessageKey.RegistrationClosed);
        member.StatusCode.Should().Be(HttpStatusCode.OK);
        memberBody.Data.Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task NotConfigured_ShouldBe404WithoutCallingGoogle()
    {
        await using var host = _factory.CreateHost(new() { ["Storeasy:Auth:Google:ClientId"] = "", ["Storeasy:Auth:Google:ClientSecret"] = "" });
        var requests = _factory.Google.TokenRequests;

        var (start, startBody) = await host.CreateClientWithAddress().PostAsync<GoogleSignInStartDto>("account/login/google/start", new { });
        var (complete, _) = await host.CreateClientWithAddress().PostAsync<LoginResponseDto>("account/login/google", new { code = "c", state = "s" });
        var (_, settings) = await host.CreateClientWithAddress().GetAsync<PublicSettingsDto>("system/public-settings");

        start.StatusCode.Should().Be(HttpStatusCode.NotFound);
        startBody.Messages.Should().Contain(m => m.Key == MessageKey.GoogleSignInDisabled);
        complete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        settings.Data.GoogleSignInEnabled.Should().BeFalse();
        _factory.Google.TokenRequests.Should().Be(requests);
    }
}
