using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.AccountDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Registration, e-mail verification and password reset.")]
public class AccountRegistrationTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private const string _registrationModeKey = "Storeasy:Registration:Mode";

    private static string NewEmail() => $"Reg{TestApi.Unique()}@Example.com";

    private static object RegisterBody(string email, string invitationToken = null) => new { email, password = TestApi.DefaultPassword, name = "Ada", surname = "Lovelace", invitationToken };

    private async Task<(int UserId, TestUser User)> RegisterAndLoginAsync(string email)
    {
        var (http, body) = await _factory.CreateClientWithAddress().PostAsync<int>("account/register", RegisterBody(email));

        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.Messages?.FirstOrDefault()?.Message);

        return (body.Data, await _factory.LoginAsync(email, TestApi.DefaultPassword));
    }

    [Fact]
    public async Task Register_ShouldCreateAnUnverifiedAccountThatSignsInWithItsEmail()
    {
        // Arrange
        var email = NewEmail();

        // Act
        var (userId, user) = await RegisterAndLoginAsync(email);
        var upperCaseLogin = await _factory.LoginAsync(email.ToUpperInvariant(), TestApi.DefaultPassword);
        var (_, detail) = await user.Client.GetAsync<AccountDetailDto>("account/detail");
        var row = await _factory.DbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == userId));

        // Assert
        upperCaseLogin.Id.Should().Be(userId);
        row.UserName.Should().Be(email.ToLowerInvariant());
        row.Email.Should().Be(email);
        row.UserType.Should().Be(UserType.AppUser);
        detail.Data.EmailConfirmed.Should().BeFalse();
        (await _factory.EmailSender.LastTokenAsync(email, AppOptions.VerifyEmailPath)).Should().StartWith("evt_");
    }

    [Fact]
    public async Task Register_WithAnEmailInUse_ShouldBe409()
    {
        var email = NewEmail();

        await RegisterAndLoginAsync(email);

        var (http, _) = await _factory.CreateClientWithAddress().PostAsync<int>("account/register", RegisterBody(email.ToUpperInvariant()));

        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithAWeakPassword_ShouldBe400()
    {
        var (http, _) = await _factory.CreateClientWithAddress().PostAsync<int>("account/register", new { email = NewEmail(), password = "weak", name = "Ada" });

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnverifiedUser_ShouldNotCreateAnOrganizationUntilTheEmailIsVerified()
    {
        // Arrange
        var email = NewEmail();
        var (_, user) = await RegisterAndLoginAsync(email);
        var token = await _factory.EmailSender.LastTokenAsync(email, AppOptions.VerifyEmailPath);

        // Act
        var (blocked, _) = await user.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var (verify, _) = await _factory.CreateClientWithAddress().PostAsync("account/email/verify", new { token });
        var (verifyAgain, _) = await _factory.CreateClientWithAddress().PostAsync("account/email/verify", new { token });
        var (allowed, _) = await user.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var (_, detail) = await user.Client.GetAsync<AccountDetailDto>("account/detail");

        // Assert
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        verifyAgain.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a verification token works once");
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        detail.Data.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ResendVerification_ShouldRetireTheEarlierLink()
    {
        // Arrange
        var email = NewEmail();
        var (_, user) = await RegisterAndLoginAsync(email);
        var firstToken = await _factory.EmailSender.LastTokenAsync(email, AppOptions.VerifyEmailPath);

        // Act
        var (resend, _) = await user.Client.PostAsync("account/email/verify/resend", new { });
        var secondToken = await _factory.EmailSender.LastTokenAsync(email, AppOptions.VerifyEmailPath, firstToken);
        var (oldLink, _) = await _factory.CreateClientWithAddress().PostAsync("account/email/verify", new { token = firstToken });
        var (newLink, _) = await _factory.CreateClientWithAddress().PostAsync("account/email/verify", new { token = secondToken });
        var (resendVerified, _) = await user.Client.PostAsync("account/email/verify/resend", new { });

        // Assert
        resend.StatusCode.Should().Be(HttpStatusCode.OK);
        secondToken.Should().NotBe(firstToken);
        oldLink.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        newLink.StatusCode.Should().Be(HttpStatusCode.OK);
        resendVerified.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ForgotPassword_ShouldAnswerTheSameForUnknownAndKnownAddresses()
    {
        // Arrange
        var email = NewEmail();
        var unknown = NewEmail();
        await RegisterAndLoginAsync(email);

        // Act
        var (knownHttp, knownBody) = await _factory.CreateClientWithAddress().PostAsync("account/password/forgot", new { email });
        var (unknownHttp, unknownBody) = await _factory.CreateClientWithAddress().PostAsync("account/password/forgot", new { email = unknown });

        // Assert
        knownHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        unknownHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        unknownBody.IsSuccess.Should().Be(knownBody.IsSuccess);
        (await _factory.EmailSender.LastTokenAsync(email, AppOptions.ResetPasswordPath)).Should().StartWith("pwr_");
        await EventTestKit.EventuallyAsync(async () => !await _factory.DbAsync(db => db.EmailOutbox.AnyAsync()), "the outbox was not drained");
        _factory.EmailSender.To(unknown).Should().BeEmpty();
    }

    [Fact]
    public async Task ResetPassword_ShouldRevokeOldTokensAndWorkOnlyOnce()
    {
        // Arrange
        var email = NewEmail();
        var (_, user) = await RegisterAndLoginAsync(email);
        await _factory.CreateClientWithAddress().PostAsync("account/password/forgot", new { email });
        var token = await _factory.EmailSender.LastTokenAsync(email, AppOptions.ResetPasswordPath);
        const string newPassword = "N3w!reset";

        // Act
        var (reset, _) = await _factory.CreateClientWithAddress().PostAsync("account/password/reset", new { token, newPassword });
        var oldAccess = await user.Client.GetAsync($"{TestApi.Base}/account/detail", TestContext.Current.CancellationToken);
        var (oldPassword, _) = await _factory.CreateClientWithAddress().RequestLoginAsync(email, TestApi.DefaultPassword);
        var newLogin = await _factory.LoginAsync(email, newPassword);
        var (reuse, _) = await _factory.CreateClientWithAddress().PostAsync("account/password/reset", new { token, newPassword = "An0ther!pass" });
        var (_, detail) = await newLogin.Client.GetAsync<AccountDetailDto>("account/detail");

        // Assert
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        oldAccess.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        oldPassword.StatusCode.Should().NotBe(HttpStatusCode.OK);
        reuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        detail.Data.EmailConfirmed.Should().BeTrue("the reset link reached the address");
    }

    [Fact]
    public async Task ResetPassword_WithAWeakPassword_ShouldNotBurnTheToken()
    {
        var email = NewEmail();
        await RegisterAndLoginAsync(email);
        await _factory.CreateClientWithAddress().PostAsync("account/password/forgot", new { email });
        var token = await _factory.EmailSender.LastTokenAsync(email, AppOptions.ResetPasswordPath);

        var (weak, _) = await _factory.CreateClientWithAddress().PostAsync("account/password/reset", new { token, newPassword = "weak" });
        var (strong, _) = await _factory.CreateClientWithAddress().PostAsync("account/password/reset", new { token, newPassword = "Str0ng!pass" });

        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        strong.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_OverTheRateLimit_ShouldBe429()
    {
        var client = _factory.CreateClientWithAddress();
        HttpResponseMessage last = null;

        for (var i = 0; i < 6; i++)
            (last, _) = await client.PostAsync("account/password/forgot", new { email = NewEmail() });

        last.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task InviteMember_ShouldEmailTheInvitationLink()
    {
        // Arrange
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organizationId) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var invitee = NewEmail();

        // Act
        var (_, invitation) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = organizationId.Data, email = invitee, role = OrgRole.Editor });

        // Assert
        var mail = (await _factory.EmailSender.WaitForAsync(invitee)).Should().ContainSingle().Subject;

        _factory.EmailSender.LastToken(invitee, AppOptions.InvitationPath).Should().Be(invitation.Data.Token);
        mail.HtmlBody.Should().Contain(invitation.Data.Token);
    }

    [Fact]
    public async Task InviteOnly_ShouldRequireAMatchingInvitationAndAcceptIt()
    {
        // Arrange
        await using var host = _factory.CreateHost(new() { [_registrationModeKey] = nameof(RegistrationMode.InviteOnly) });

        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var invitee = NewEmail();
        var (_, invitation) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = organization.Data, email = invitee, role = OrgRole.Viewer });

        // Act
        var (withoutToken, _) = await host.CreateClientWithAddress().PostAsync<int>("account/register", RegisterBody(invitee));
        var (otherEmail, _) = await host.CreateClientWithAddress().PostAsync<int>("account/register", RegisterBody(NewEmail(), invitation.Data.Token));
        var (withToken, registered) = await host.CreateClientWithAddress().PostAsync<int>("account/register", RegisterBody(invitee, invitation.Data.Token));
        var (reused, _) = await host.CreateClientWithAddress().PostAsync<int>("account/register", RegisterBody(NewEmail(), invitation.Data.Token));

        var membership = await _factory.DbAsync(db => db.OrganizationMembers.AsNoTracking().SingleOrDefaultAsync(m => m.OrganizationId == organization.Data && m.UserId == registered.Data));

        // Assert
        withoutToken.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        otherEmail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        withToken.StatusCode.Should().Be(HttpStatusCode.OK);
        reused.StatusCode.Should().Be(HttpStatusCode.NotFound);
        membership.Should().NotBeNull();
        membership.Role.Should().Be(OrgRole.Viewer);
    }

    [Fact]
    public async Task Closed_ShouldRefuseEveryRegistration()
    {
        await using var host = _factory.CreateHost(new() { [_registrationModeKey] = nameof(RegistrationMode.Closed) });

        var (http, body) = await host.CreateClientWithAddress().PostAsync<int>("account/register", RegisterBody(NewEmail()));

        http.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Messages.Should().Contain(m => m.Key == MessageKey.RegistrationClosed);
    }

    [Fact]
    public async Task Register_WhenTheMailProviderIsDown_ShouldStillCreateTheAccount()
    {
        _factory.EmailSender.Fail = true;

        try
        {
            var email = NewEmail();
            var (http, _) = await _factory.CreateClientWithAddress().PostAsync<int>("account/register", RegisterBody(email));

            http.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            _factory.EmailSender.Fail = false;
        }
    }
}
