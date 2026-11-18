using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;

namespace Storeasy.IntegrationTests.ServicesTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Service Integration Tests", "Deadline reminders: TestFlight build expiry (14/7/1) and compliance deadlines (30/7/1), once per threshold.")]
public class DeadlineNotifierTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task BuildExpiry_ShouldSendTheNearestThresholdOnce()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (_, storeAppId, _) = await _factory.AddAppAsync(org, name: "Halka");
        var now = DateTime.UtcNow;
        var buildId = await _factory.AddBuildAsync(org.OrganizationId, storeAppId, $"b-{org.Suffix}", "1.0", "37", now.AddDays(5).AddHours(1));

        // Act
        var first = await _factory.WithServiceAsync<IDeadlineNotifier, int>(n => n.NotifyBuildExpiryAsync(now, CancellationToken.None));
        var again = await _factory.WithServiceAsync<IDeadlineNotifier, int>(n => n.NotifyBuildExpiryAsync(now.AddHours(1), CancellationToken.None));
        var lastDay = await _factory.WithServiceAsync<IDeadlineNotifier, int>(n => n.NotifyBuildExpiryAsync(now.AddDays(5), CancellationToken.None));
        var reminders = await _factory.DbAsync(db => db.DeadlineReminders.AsNoTracking().Where(r => r.Subject == $"build:{buildId}").Select(r => r.ThresholdDays).ToListAsync());
        var notices = await _factory.DbAsync(db => db.InternalNotifications.AsNoTracking().Where(n => n.OrganizationId == org.OrganizationId && n.Type == NotificationType.Deadline).ToListAsync());

        // Assert
        first.Should().BeGreaterThanOrEqualTo(1);
        reminders.Should().BeEquivalentTo([7, 1]);
        notices.Should().HaveCount(2).And.OnlyContain(n => n.RecipientUserId == org.Owner.Id && n.Text.Contains("Halka"));
        again.Should().Be(0);
        lastDay.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ComplianceDeadline_ShouldComeFromTheCatalogAt30Days()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (appId, _, _) = await _factory.AddAppAsync(org, name: "Pusula");
        var now = DateTime.UtcNow;
        _factory.Catalog.Deadlines[appId] = [new CatalogDeadline(appId, "apple.age-rating.shared-location-dating", StoreKind.Apple, "Age rating: new questions", now.AddDays(20), "https://developer.apple.com/news/", "Runs on iOS")];

        // Act
        await _factory.WithServiceAsync<IDeadlineNotifier, int>(n => n.NotifyComplianceDeadlinesAsync(now, CancellationToken.None));
        var reminder = await _factory.DbAsync(db => db.DeadlineReminders.AsNoTracking().SingleAsync(r => r.Subject.StartsWith($"rule:apple.age-rating.shared-location-dating:app:{appId}:")));
        var notice = await _factory.DbAsync(db => db.InternalNotifications.AsNoTracking().SingleAsync(n => n.OrganizationId == org.OrganizationId && n.Type == NotificationType.Deadline));

        // Assert
        reminder.ThresholdDays.Should().Be(30);
        notice.Text.Should().Contain("Pusula").And.Contain("Age rating");
        notice.RelatedEntityId.Should().Be("apple.age-rating.shared-location-dating");
    }
}
