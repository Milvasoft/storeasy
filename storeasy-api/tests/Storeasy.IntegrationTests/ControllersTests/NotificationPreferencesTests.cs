using FluentAssertions;
using Storeasy.Application.Dtos.AccountDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Notification preferences and the organization label of notifications.")]
public class NotificationPreferencesTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Preferences_ShouldDecideTheChannelsAndLockedKindsCannotBeTurnedOff()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var admin = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Admin);
        var email = (await _factory.GetUserRowAsync(admin.UserName)).Email;

        // Act
        var (_, defaults) = await admin.Client.GetAsync<List<NotificationPreferenceDto>>("account/preferences");
        var (_, saved) = await admin.Client.PutAsync<NotificationPreferenceDto>("account/preferences", new { kind = NotificationType.Rejection, inApp = true, email = false });
        var (locked, _) = await admin.Client.PutAsync<NotificationPreferenceDto>("account/preferences", new { kind = NotificationType.ConnectionAlert, inApp = false, email = true });
        var reached = await _factory.WithServiceAsync<INotificationDispatcher, int>(d => d.DispatchAsync(new OrgNotification
        {
            OrganizationId = org.OrganizationId,
            Kind = NotificationType.Rejection,
            Roles = NotificationAudiences.OwnersAndAdmins,
            TextKey = Application.Utils.Constants.MessageKey.NotificationRejection,
            TextArgs = ["Defter", "1.4", "REJECTED"],
        }, CancellationToken.None));
        var (_, list) = await admin.Client.PatchListAsync<AccountNotificationDto>("account/notifications", new { pageNumber = 1, rowCount = 10 });

        // Assert
        defaults.Data.Should().Contain(p => p.Kind == NotificationType.NewReview && p.InApp && !p.Email);
        defaults.Data.Should().Contain(p => p.Kind == NotificationType.ConnectionAlert && p.Locked);
        saved.Data.Email.Should().BeFalse();
        locked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        reached.Should().Be(2);
        _factory.EmailSender.To(email).Should().BeEmpty("the admin turned the e-mail of rejections off");
        list.Data.Should().ContainSingle(n => n.OrganizationId == org.OrganizationId && n.Type == NotificationType.Rejection && n.Text.Contains("Defter"));
    }

    [Fact]
    public async Task Notifications_FilteredByIsSeen_ShouldBeFilteredOnTheSeenDate()
    {
        // Arrange: two notifications, the first one marked seen.
        var org = await _factory.NewOrgAsync();
        var admin = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Admin);

        foreach (var app in new[] { "Defter", "Kalem" })
        {
            await _factory.WithServiceAsync<INotificationDispatcher, int>(d => d.DispatchAsync(new OrgNotification
            {
                OrganizationId = org.OrganizationId,
                Kind = NotificationType.Rejection,
                Roles = NotificationAudiences.OwnersAndAdmins,
                TextKey = Application.Utils.Constants.MessageKey.NotificationRejection,
                TextArgs = [app, "1.4", "REJECTED"],
            }, CancellationToken.None));
        }

        var (_, all) = await admin.Client.PatchListAsync<AccountNotificationDto>("account/notifications", new { pageNumber = 1, rowCount = 10 });
        var seenId = all.Data.Single(n => n.Text.Contains("Defter")).Id;
        await admin.Client.PutAsync<object>("account/notifications/seen", new { notificationIdList = new[] { seenId }, markAll = false });

        // Act: the bell's unread counter (IsSeen = false), and the seen ones.
        var (unreadHttp, unread) = await admin.Client.PatchListAsync<AccountNotificationDto>("account/notifications", new
        {
            pageNumber = 1,
            rowCount = 1,
            filtering = new { criterias = new[] { new { filterBy = "IsSeen", value = (object)false, type = 5 } } },
        });
        var (seenHttp, seen) = await admin.Client.PatchListAsync<AccountNotificationDto>("account/notifications", new
        {
            pageNumber = 1,
            rowCount = 10,
            filtering = new { criterias = new[] { new { filterBy = "isSeen", value = (object)true, type = 5 } } },
        });

        // Assert
        unreadHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        unread.TotalDataCount.Should().Be(1);
        unread.Data.Should().ContainSingle(n => n.Text.Contains("Kalem") && !n.IsSeen);
        seenHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        seen.Data.Should().ContainSingle(n => n.Id == seenId && n.IsSeen);
    }
}
