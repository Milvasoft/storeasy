using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.AccountDtos;
using Storeasy.Application.Dtos.FeatureFlagDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Account: profile, platform permissions, feature flags, sessions, web push, invitation preview and the e-mail outbox.")]
public class AccountPhase8Tests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static async Task<(HttpResponseMessage Http, Response<T> Body)> DeleteWithBodyAsync<T>(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{TestApi.Base}/{path}") { Content = JsonContent.Create(body, options: TestApi.Json) };

        var http = await client.SendAsync(request);

        return (http, await TestApi.ReadAsync<Response<T>>(http));
    }

    [Fact]
    public async Task UpdateProfile_ShouldChangeNameSurnameAndLanguage()
    {
        // Arrange
        var user = await _factory.CreateAndLoginUserAsync();

        // Act
        var (update, _) = await user.Client.PutAsync<int>("account/profile", new { name = "Ada", surname = "Byron", language = "en-us" });
        var (unsupported, _) = await user.Client.PutAsync<int>("account/profile", new { name = "Ada", language = "xx-YY" });
        var (empty, _) = await user.Client.PutAsync<int>("account/profile", new { name = "", language = "tr-TR" });
        var (_, detail) = await user.Client.GetAsync<AccountDetailDto>("account/detail");

        // Assert
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        unsupported.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        detail.Data.Name.Should().Be("Ada");
        detail.Data.Surname.Should().Be("Byron");
        detail.Data.PreferredLanguage.Should().Be("en-US");
        detail.Data.UserType.Should().Be(UserType.AppUser);
        detail.Data.Permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task AccountDetail_ShouldListThePlatformPermissions()
    {
        var root = await _factory.LoginAsRootAsync();

        var (_, detail) = await root.Client.GetAsync<AccountDetailDto>("account/detail");

        detail.Data.Permissions.Should().Contain(["App.SuperAdmin", "RuleCatalogManagement.List", "FeatureFlagManagement.Update"]);
    }

    [Fact]
    public async Task FeatureFlagStates_ShouldBeReadableByAppUsers()
    {
        var user = await _factory.CreateAndLoginUserAsync();

        var (http, flags) = await user.Client.GetAsync<List<FeatureFlagStateDto>>("system/feature-flags");
        var (admin, _) = await user.Client.PatchListAsync<FeatureFlagListDto>("featureFlags", new { pageNumber = 1, rowCount = 10 });
        var (anonymous, _) = await _factory.CreateClientWithAddress().GetAsync<List<FeatureFlagStateDto>>("system/feature-flags");

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        flags.Data.Select(f => f.Key).Should().BeEquivalentTo(FeatureFlagKeys.All);
        admin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sessions_ShouldListDevices_AndSignOutOneOrTheOthers()
    {
        // Arrange
        var user = await _factory.CreateUserAsync();
        var laptop = await _factory.LoginAsync(user.UserName, TestApi.DefaultPassword, "laptop");
        var phone = await _factory.LoginAsync(user.UserName, TestApi.DefaultPassword, "phone");
        var tablet = await _factory.LoginAsync(user.UserName, TestApi.DefaultPassword, "tablet");

        // Act
        var (_, sessions) = await laptop.Client.GetAsync<List<AccountSessionDto>>("account/sessions");
        var phoneSession = sessions.Data.Single(s => s.DeviceId == "phone");
        var (revoke, _) = await DeleteWithBodyAsync<long>(laptop.Client, "account/session", new { id = phoneSession.Id });
        var phoneAfter = await phone.Client.GetAsync($"{TestApi.Base}/account/detail", TestContext.Current.CancellationToken);
        var tabletAfter = await tablet.Client.GetAsync($"{TestApi.Base}/account/detail", TestContext.Current.CancellationToken);
        var (others, othersBody) = await DeleteWithBodyAsync<int>(laptop.Client, "account/sessions/others", new { });
        var tabletLast = await tablet.Client.GetAsync($"{TestApi.Base}/account/detail", TestContext.Current.CancellationToken);
        var (_, remaining) = await laptop.Client.GetAsync<List<AccountSessionDto>>("account/sessions");
        var stranger = await _factory.CreateAndLoginUserAsync();
        var (foreign, _) = await DeleteWithBodyAsync<long>(stranger.Client, "account/session", new { id = remaining.Data[0].Id });

        // Assert
        sessions.Data.Select(s => s.DeviceId).Should().BeEquivalentTo(["laptop", "phone", "tablet"]);
        sessions.Data.Should().ContainSingle(s => s.IsCurrent).Which.DeviceId.Should().Be("laptop");
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);
        phoneAfter.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        tabletAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        others.StatusCode.Should().Be(HttpStatusCode.OK);
        othersBody.Data.Should().Be(1);
        tabletLast.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        remaining.Data.Should().ContainSingle(s => s.DeviceId == "laptop" && s.IsCurrent);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WebPush_ShouldDeliverEncryptedNotificationsToSubscribedBrowsers()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var browser = _factory.PushService.Subscribe();
        var gone = _factory.PushService.Subscribe();
        _factory.PushService.Gone[gone.Endpoint] = 0;

        // Act
        var (save, saveBody) = await org.Owner.Client.PostAsync<int>("account/push/subscription", new { endpoint = browser.Endpoint, p256dh = browser.P256dh, auth = browser.Auth, userAgent = "Firefox" });
        var (saveGone, _) = await org.Owner.Client.PostAsync<int>("account/push/subscription", new { endpoint = gone.Endpoint, p256dh = gone.P256dh, auth = gone.Auth });
        var (invalid, _) = await org.Owner.Client.PostAsync<int>("account/push/subscription", new { endpoint = "http://127.0.0.1/push", p256dh = browser.P256dh, auth = browser.Auth });
        await _factory.WithServiceAsync<INotificationDispatcher, int>(dispatcher => dispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = org.OrganizationId,
            Kind = NotificationType.Rejection,
            Roles = NotificationAudiences.OwnersAndAdmins,
            TextKey = MessageKey.NotificationRejection,
            TextArgs = ["Defter", "1.4", "REJECTED"],
            ActionLink = "/rejections",
        }, CancellationToken.None));

        await EventTestKit.EventuallyAsync(() => Task.FromResult(_factory.PushService.For(browser.Endpoint).Count > 0 && _factory.PushService.For(gone.Endpoint).Count > 0), "the pushes did not arrive");
        await EventTestKit.EventuallyAsync(async () => !await _factory.DbAsync(db => db.WebPushSubscriptions.AnyAsync(s => s.Endpoint == gone.Endpoint)), "the expired subscription was not removed");

        var push = _factory.PushService.For(browser.Endpoint).Last();
        using var payload = JsonDocument.Parse(_factory.PushService.Decrypt(push));
        var (_, list) = await org.Owner.Client.GetAsync<List<WebPushSubscriptionDto>>("account/push/subscriptions");
        var (delete, _) = await DeleteWithBodyAsync<int>(org.Owner.Client, "account/push/subscription", new { endpoint = browser.Endpoint });

        // Assert
        save.StatusCode.Should().Be(HttpStatusCode.OK, saveBody?.Messages?.FirstOrDefault()?.Message);
        saveGone.StatusCode.Should().Be(HttpStatusCode.OK);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        push.Headers["Content-Encoding"].Should().Be("aes128gcm");
        push.Headers["Authorization"].Should().StartWith("vapid t=").And.EndWith($"k={StoreasyApiFactory.VapidKeys.PublicKey}");
        push.Headers.Should().ContainKey("TTL");
        payload.RootElement.GetProperty("body").GetString().Should().Contain("Defter");
        payload.RootElement.GetProperty("url").GetString().Should().EndWith("/rejections");
        payload.RootElement.GetProperty("kind").GetString().Should().Be(nameof(NotificationType.Rejection));
        list.Data.Should().ContainSingle().Which.PushServiceHost.Should().Be("push.example.test");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WebPushPreference_ShouldTurnThePushOff()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var browser = _factory.PushService.Subscribe();
        await org.Owner.Client.PostAsync<int>("account/push/subscription", new { endpoint = browser.Endpoint, p256dh = browser.P256dh, auth = browser.Auth });
        var (_, preferences) = await org.Owner.Client.GetAsync<List<NotificationPreferenceDto>>("account/preferences");

        // Act
        await org.Owner.Client.PutAsync<NotificationPreferenceDto>("account/preferences", new { kind = NotificationType.Rejection, inApp = true, email = true, webPush = false });
        await _factory.WithServiceAsync<INotificationDispatcher, int>(dispatcher => dispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = org.OrganizationId,
            Kind = NotificationType.Rejection,
            Roles = NotificationAudiences.OwnersAndAdmins,
            TextKey = MessageKey.NotificationRejection,
            TextArgs = ["Defter", "1.5", "REJECTED"],
        }, CancellationToken.None));
        var (test, _) = await org.Owner.Client.PostAsync<int>("account/push/test", new { });

        await EventTestKit.EventuallyAsync(() => Task.FromResult(_factory.PushService.For(browser.Endpoint).Count > 0), "the test push did not arrive");

        // Assert
        preferences.Data.Should().Contain(p => p.Kind == NotificationType.Rejection && p.WebPush);
        preferences.Data.Should().Contain(p => p.Kind == NotificationType.NewReview && !p.WebPush);
        test.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.PushService.For(browser.Endpoint).Select(p => _factory.PushService.Decrypt(p)).Should().ContainSingle().Which.Should().NotContain("1.5");
    }

    [Fact]
    public async Task InvitationPreview_ShouldShowOnlyWhatTheStateAllows()
    {
        // Arrange
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Preview {TestApi.Unique()}" });
        var email = $"carol{TestApi.Unique()}@example.com";
        var (_, revoked) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = organization.Data, email, role = OrgRole.Viewer });
        var (_, pending) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = organization.Data, email, role = OrgRole.Editor });
        var anonymous = _factory.CreateClientWithAddress();

        // Act
        var (_, pendingPreview) = await anonymous.GetAsync<InvitationPreviewDto>($"organizations/invitation/preview?token={Uri.EscapeDataString(pending.Data.Token)}");
        var (_, revokedPreview) = await anonymous.GetAsync<InvitationPreviewDto>($"organizations/invitation/preview?token={Uri.EscapeDataString(revoked.Data.Token)}");
        var (unknown, unknownBody) = await anonymous.GetAsync<InvitationPreviewDto>("organizations/invitation/preview?token=inv_unknown");

        await _factory.DbAsync(async db =>
        {
            await db.OrganizationInvitations.Where(i => i.Id == pending.Data.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.ExpiryDate, DateTime.UtcNow.AddMinutes(-1)));
        });

        var (_, expiredPreview) = await anonymous.GetAsync<InvitationPreviewDto>($"organizations/invitation/preview?token={Uri.EscapeDataString(pending.Data.Token)}");

        // Assert
        pendingPreview.Data.Status.Should().Be(InvitationStatus.Pending);
        pendingPreview.Data.OrganizationName.Should().StartWith("Preview");
        pendingPreview.Data.InviterName.Should().Be("Test User");
        pendingPreview.Data.Role.Should().Be(OrgRole.Editor);
        pendingPreview.Data.Permissions.Should().Contain(OrgPermission.EditContent);
        pendingPreview.Data.MaskedEmail.Should().StartWith("c").And.Contain("**@example.com").And.NotContain("carol");
        revokedPreview.Data.Status.Should().Be(InvitationStatus.Revoked);
        revokedPreview.Data.OrganizationName.Should().BeNull();
        revokedPreview.Data.InviterName.Should().BeNull();
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknownBody.Messages.Should().Contain(m => m.Key == MessageKey.InvitationInvalid);
        expiredPreview.Data.Status.Should().Be(InvitationStatus.Expired);
        expiredPreview.Data.InviterName.Should().Be("Test User");
        expiredPreview.Data.Role.Should().BeNull();
    }

    [Fact]
    public async Task InvitationPreview_OverTheRateLimit_ShouldBe429()
    {
        var client = _factory.CreateClientWithAddress();
        HttpResponseMessage last = null;

        for (var i = 0; i < 31; i++)
            (last, _) = await client.GetAsync<InvitationPreviewDto>("organizations/invitation/preview?token=inv_x");

        last.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Outbox_ShouldKeepTheBodySealedUntilItIsSent()
    {
        // Arrange
        await using var host = _factory.CreateHost();
        var email = $"sealed{TestApi.Unique()}@example.com";
        await _factory.CreateUserAsync(email: email);

        // Act: the extra host does not run the sender; read the row before the shared host sends it.
        var started = DateTime.UtcNow;
        var (http, _) = await host.CreateClientWithAddress().PostAsync("account/password/forgot", new { email });
        var rows = await _factory.DbAsync(db => db.EmailOutbox.AsNoTracking().Where(o => o.CreationDate >= started.AddSeconds(-5)).ToListAsync());
        var token = await _factory.EmailSender.LastTokenAsync(email, AppOptions.ResetPasswordPath);

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        token.Should().StartWith("pwr_");
        rows.Should().OnlyContain(r => !Encoding.UTF8.GetString(r.EncryptedPayload).Contains(email, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OrganizationSettings_ShouldBeReadByManagersAndChangedByOwners()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var admin = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Admin);
        var viewer = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Viewer);

        // Act
        var (_, defaults) = await admin.Client.GetAsync<OrganizationSettingsDto>($"organizations/settings?organizationId={org.OrganizationId}");
        var (viewerRead, _) = await viewer.Client.GetAsync<OrganizationSettingsDto>($"organizations/settings?organizationId={org.OrganizationId}");
        var (adminWrite, _) = await admin.Client.PutAsync<int>("organizations/settings", new { organizationId = org.OrganizationId, auditRetentionDays = 90 });
        var (invalid, _) = await org.Owner.Client.PutAsync<int>("organizations/settings", new { organizationId = org.OrganizationId, auditRetentionDays = 100 });
        var (ownerWrite, _) = await org.Owner.Client.PutAsync<int>("organizations/settings", new { organizationId = org.OrganizationId, auditRetentionDays = 90, defaultLocale = "tr-TR" });
        var (_, after) = await org.Owner.Client.GetAsync<OrganizationSettingsDto>($"organizations/settings?organizationId={org.OrganizationId}");

        // Assert
        defaults.Data.AuditRetentionDays.Should().Be(365);
        defaults.Data.ArtifactRetentionDays.Should().Be(30);
        defaults.Data.SeenNotificationRetentionDays.Should().Be(30);
        viewerRead.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        adminWrite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ownerWrite.StatusCode.Should().Be(HttpStatusCode.OK);
        after.Data.AuditRetentionDays.Should().Be(90);
        after.Data.DefaultLocale.Should().Be("tr-TR");
    }
}
