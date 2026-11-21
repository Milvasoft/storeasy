using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Jobs;
using Storeasy.IntegrationTests.TestBase;

namespace Storeasy.IntegrationTests.ServicesTests;

/// <summary>
/// The cleanup jobs log and swallow their own errors (a wrong column name in a SQL file only shows up in the log), so
/// every job is run here against the real database and the test asserts which rows are gone and which are kept.
/// </summary>
/// <param name="factory"></param>
[Collection(nameof(StoreasyApiCollection))]
[Trait("Service Integration Tests", "Integration tests for the cleanup jobs.")]
public class CleanupJobsTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task IdempotencyKeyCleanerJob_ShouldDeleteOnlyTheKeysOlderThanTheRetryWindow()
    {
        // Arrange: one key past the 24 hour window, one fresh key.
        var expiredKey = Guid.NewGuid().ToString("N");
        var freshKey = Guid.NewGuid().ToString("N");

        await _factory.DbAsync(async db =>
        {
            await db.IdempotencyKeys.AddRangeAsync([
                new IdempotencyKey { UserId = 1, Key = expiredKey, Endpoint = "POST test", RequestHash = new byte[32], CreationDate = DateTime.UtcNow.AddHours(-25) },
                new IdempotencyKey { UserId = 1, Key = freshKey, Endpoint = "POST test", RequestHash = new byte[32], CreationDate = DateTime.UtcNow }
            ], TestContext.Current.CancellationToken);

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        // Act
        await RunJobAsync<IdempotencyKeyCleanerJob>();

        // Assert
        var remainingKeys = await _factory.DbAsync(db => db.IdempotencyKeys.Where(k => k.Key == expiredKey || k.Key == freshKey).Select(k => k.Key).ToListAsync(TestContext.Current.CancellationToken));

        remainingKeys.Should().Equal(freshKey);
    }

    [Fact]
    public async Task ActivityLogCleanerJob_ShouldDeleteOnlyTheLogsOlderThanSixtyDays()
    {
        // Arrange: one log past the 60 day window, one recent log, both under a user name no other test uses.
        var userName = $"cleanup{TestApi.Unique()}";

        await _factory.DbAsync(async db =>
        {
            await db.ActivityLogs.AddRangeAsync([
                new ActivityLog { UserName = userName, Activity = UserActivity.CreateUser, ActivityDate = DateTimeOffset.UtcNow.AddDays(-61), RelatedId = "expired" },
                new ActivityLog { UserName = userName, Activity = UserActivity.CreateUser, ActivityDate = DateTimeOffset.UtcNow.AddDays(-1), RelatedId = "recent" }
            ], TestContext.Current.CancellationToken);

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        // Act
        await RunJobAsync<ActivityLogCleanerJob>();

        // Assert
        var remainingLogs = await _factory.DbAsync(db => db.ActivityLogs.Where(l => l.UserName == userName).Select(l => l.RelatedId).ToListAsync(TestContext.Current.CancellationToken));

        remainingLogs.Should().Equal("recent");
    }

    [Fact]
    public async Task NotificationCleanerJob_ShouldDeleteTheOldSeenAndTheExpiredUnseenNotifications()
    {
        // Arrange: seen 31 days ago (deleted), seen yesterday (kept), unseen and created 61 days ago (deleted), unseen and
        // created yesterday (kept). The creation date is audited on insert, so it is moved back with an update afterwards.
        var recipient = $"cleanup{TestApi.Unique()}";

        await _factory.DbAsync(async db =>
        {
            await db.InternalNotifications.AddRangeAsync([
                new InternalNotification { RecipientUserName = recipient, RecipientUserId = 1, Type = NotificationType.OrderApproved, Text = "seen-old", SeenDate = DateTime.UtcNow.AddDays(-31) },
                new InternalNotification { RecipientUserName = recipient, RecipientUserId = 1, Type = NotificationType.OrderApproved, Text = "seen-recent", SeenDate = DateTime.UtcNow.AddDays(-1) },
                new InternalNotification { RecipientUserName = recipient, RecipientUserId = 1, Type = NotificationType.OrderApproved, Text = "unseen-old" },
                new InternalNotification { RecipientUserName = recipient, RecipientUserId = 1, Type = NotificationType.OrderApproved, Text = "unseen-recent" }
            ], TestContext.Current.CancellationToken);

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await db.InternalNotifications.Where(n => n.RecipientUserName == recipient && n.Text == "unseen-old")
                                          .ExecuteUpdateAsync(s => s.SetProperty(n => n.CreationDate, DateTime.UtcNow.AddDays(-61)), TestContext.Current.CancellationToken);

            await db.InternalNotifications.Where(n => n.RecipientUserName == recipient && n.Text == "unseen-recent")
                                          .ExecuteUpdateAsync(s => s.SetProperty(n => n.CreationDate, DateTime.UtcNow.AddDays(-1)), TestContext.Current.CancellationToken);
        });

        // Act
        await RunJobAsync<NotificationCleanerJob>();

        // Assert
        var remainingNotifications = await _factory.DbAsync(db => db.InternalNotifications.Where(n => n.RecipientUserName == recipient)
                                                                                           .OrderBy(n => n.Text)
                                                                                           .Select(n => n.Text)
                                                                                           .ToListAsync(TestContext.Current.CancellationToken));

        remainingNotifications.Should().Equal("seen-recent", "unseen-recent");
    }

    [Fact]
    public async Task AuditLogCleanerJob_ShouldDeleteTheEntriesPastTheirOrganizationsRetention()
    {
        // Arrange: a 90 day organization and a default (365 day) one, each with an entry 100 days old and a recent one.
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, shortRetention) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var (_, longRetention) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        await owner.Client.PutAsync<int>("organizations/settings", new { organizationId = shortRetention.Data, auditRetentionDays = 90 });

        await _factory.DbAsync(async db =>
        {
            foreach (var organizationId in (int[])[shortRetention.Data, longRetention.Data])
            {
                db.StoreAuditLogs.AddRange(new Domain.Stores.StoreAuditLog { OrganizationId = organizationId, UserName = "cleanup", Action = AuditAction.ConnectionAdded, Summary = "old", Date = DateTime.UtcNow.AddDays(-100) },
                                           new Domain.Stores.StoreAuditLog { OrganizationId = organizationId, UserName = "cleanup", Action = AuditAction.ConnectionAdded, Summary = "recent", Date = DateTime.UtcNow.AddDays(-1) });
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        // Act
        await RunJobAsync<AuditLogCleanerJob>();

        // Assert
        var remaining = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.UserName == "cleanup" && (a.OrganizationId == shortRetention.Data || a.OrganizationId == longRetention.Data))
                                                                      .Select(a => new { a.OrganizationId, a.Summary })
                                                                      .ToListAsync(TestContext.Current.CancellationToken));

        remaining.Where(a => a.OrganizationId == shortRetention.Data).Select(a => a.Summary).Should().Equal("recent");
        remaining.Where(a => a.OrganizationId == longRetention.Data).Select(a => a.Summary).Should().BeEquivalentTo(["old", "recent"]);
    }

    [Fact]
    public async Task StoreMetricCleanerJob_ShouldDeleteOnlyTheRowsOlderThanTwentyFiveMonths()
    {
        // Arrange: one row past the retention, one inside it, on a store record of a new organization.
        var org = await _factory.NewOrgAsync();
        var (_, storeAppId, _) = await _factory.AddAppAsync(org);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await _factory.DbAsync(async db =>
        {
            db.StoreMetrics.AddRange(new Domain.Analytics.StoreMetric { OrganizationId = org.OrganizationId, StoreAppId = storeAppId, Store = StoreKind.Apple, Date = today.AddMonths(-26), Country = "", Source = TrafficSource.All, Downloads = 1, UpdatedDate = DateTime.UtcNow },
                                     new Domain.Analytics.StoreMetric { OrganizationId = org.OrganizationId, StoreAppId = storeAppId, Store = StoreKind.Apple, Date = today.AddMonths(-24), Country = "", Source = TrafficSource.All, Downloads = 2, UpdatedDate = DateTime.UtcNow });

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        // Act
        await RunJobAsync<StoreMetricCleanerJob>();

        // Assert
        var remaining = await _factory.DbAsync(db => db.StoreMetrics.Where(m => m.StoreAppId == storeAppId).Select(m => m.Downloads).ToListAsync(TestContext.Current.CancellationToken));

        remaining.Should().Equal(2L);
    }

    /// <summary>
    /// Runs a cleanup job now instead of waiting for its schedule (AddInternalJobs registers it as a hosted service).
    /// </summary>
    private async Task RunJobAsync<TJob>() where TJob : SqlCleanupJob
    {
        var job = _factory.Services.GetServices<IHostedService>().OfType<TJob>().Single();

        await job.ExecuteAsync(TestContext.Current.CancellationToken);
    }
}
