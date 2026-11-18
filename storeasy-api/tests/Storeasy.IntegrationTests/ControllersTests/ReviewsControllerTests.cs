using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Review inbox: sync of both stores, replies, read marks, saved replies.")]
public class ReviewsControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static StoreReviewRecord Review(string id, int rating, string body, string locale = "tr", string reply = null)
        => new(id, "Ayşe K.", rating, null, body, locale, "TR", "3.1.2", "Pixel 8", DateTime.UtcNow.AddHours(-1), null, reply, reply == null ? null : DateTime.UtcNow);

    private async Task<List<ReviewListDto>> SyncAndListAsync(EventOrg org, object filter = null)
    {
        var (_, jobs) = await org.Owner.Client.PostAsync<List<long>>("reviews/sync", new { organizationId = org.OrganizationId });

        foreach (var jobId in jobs.Data)
            (await _factory.WaitForJobAsync(jobId)).Status.Should().Be(StoreJobStatus.Succeeded);

        var (_, list) = await org.Owner.Client.PatchListAsync<ReviewListDto>("reviews", filter ?? new { organizationId = org.OrganizationId, pageNumber = 1, rowCount = 50 });

        return list.Data;
    }

    [Fact]
    public async Task Sync_ShouldKeepReviewsOfBothStoresAndAnnounceNewOnes()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (_, _, appleId) = await _factory.AddAppAsync(org, StoreKind.Apple, "Mola");
        var (_, _, packageName) = await _factory.AddAppAsync(org, StoreKind.Google, "Mola Android");
        _factory.StoreEvents.Reviews[appleId] = [Review($"a1-{org.Suffix}", 5, "Ruhig und klar", "de-DE")];
        _factory.StoreEvents.Reviews[packageName] = [Review($"g1-{org.Suffix}", 2, "Hatırlatıcılar gelmiyor", reply: "Teşekkürler")];

        // Act
        var first = await SyncAndListAsync(org);
        _factory.StoreEvents.Reviews[packageName] = [Review($"g2-{org.Suffix}", 1, "Çöküyor")];
        var second = await SyncAndListAsync(org);
        var (_, unreplied) = await org.Owner.Client.PatchListAsync<ReviewListDto>("reviews", new { organizationId = org.OrganizationId, hasReply = false, store = StoreKind.Google, pageNumber = 1, rowCount = 50 });
        var announcements = await _factory.DbAsync(db => db.InternalNotifications.CountAsync(n => n.OrganizationId == org.OrganizationId && n.Type == NotificationType.NewReview));

        // Assert
        first.Should().HaveCount(2);
        first.Should().ContainSingle(r => r.Store == StoreKind.Google && r.ReplyState == ReviewReplyState.Published && r.ReplyMaxLength == 350);
        first.Should().ContainSingle(r => r.Store == StoreKind.Apple && r.ReplyMaxLength == 5970 && r.AppName == "Mola");
        second.Should().HaveCount(3, "reviews the store stops returning stay in storeasy");
        unreplied.Data.Should().ContainSingle(r => r.Body == "Çöküyor");
        announcements.Should().Be(1, "the first import is not announced");
    }

    [Fact]
    public async Task Reply_ShouldRespectTheStoreLimitPublishAndReportFailures()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (_, _, packageName) = await _factory.AddAppAsync(org, StoreKind.Google);
        _factory.StoreEvents.Reviews[packageName] = [Review($"g1-{org.Suffix}", 2, "Bad"), Review($"g2-{org.Suffix}", 1, "Worse")];
        _factory.StoreEvents.ReplyFailures[$"g2-{org.Suffix}"] = new StoreCallException("403: The caller does not have permission", 403, false);
        var reviews = await SyncAndListAsync(org);
        var good = reviews.Single(r => r.ExternalReviewId == $"g1-{org.Suffix}");
        var failing = reviews.Single(r => r.ExternalReviewId == $"g2-{org.Suffix}");
        var viewer = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Viewer);

        // Act
        var (tooLong, _) = await org.Owner.Client.PostAsync<long>("reviews/reply", new { organizationId = org.OrganizationId, reviewId = good.Id, body = new string('x', 351) });
        var (viewerReply, _) = await viewer.Client.PostAsync<long>("reviews/reply", new { organizationId = org.OrganizationId, reviewId = good.Id, body = "Thanks" });
        var (_, job) = await org.Owner.Client.PostAsync<long>("reviews/reply", new { organizationId = org.OrganizationId, reviewId = good.Id, body = "Teşekkürler, 3.1.3 ile düzeldi." });
        await _factory.WaitForJobAsync(job.Data);
        var (_, failedJob) = await org.Owner.Client.PostAsync<long>("reviews/reply", new { organizationId = org.OrganizationId, reviewId = failing.Id, body = "Sorry" });
        (await _factory.WaitForJobAsync(failedJob.Data)).Status.Should().Be(StoreJobStatus.Failed);
        var rows = await _factory.DbAsync(db => db.StoreReviews.AsNoTracking().Where(r => r.OrganizationId == org.OrganizationId).ToListAsync());
        var failureNotice = await _factory.DbAsync(db => db.InternalNotifications.AnyAsync(n => n.RecipientUserId == org.Owner.Id && n.Type == NotificationType.ReviewReplyFailed));

        // Assert
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        viewerReply.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _factory.StoreEvents.Replies.Should().Contain(r => r.ReviewId == good.ExternalReviewId && r.Body == "Teşekkürler, 3.1.3 ile düzeldi.");
        rows.Single(r => r.Id == good.Id).ReplyState.Should().Be(ReviewReplyState.Published);
        rows.Single(r => r.Id == failing.Id).Should().Match<Domain.Feedback.StoreReview>(r => r.ReplyState == ReviewReplyState.Failed && r.ReplyError.Contains("403"));
        failureNotice.Should().BeTrue();
    }

    [Fact]
    public async Task ReadMarksAndSavedReplies_ShouldWorkPerUserAndPerOrganization()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var other = await _factory.NewOrgAsync();
        var (appId, _, appleId) = await _factory.AddAppAsync(org);
        _factory.StoreEvents.Reviews[appleId] = [Review($"a1-{org.Suffix}", 4, "Nice")];
        var review = (await SyncAndListAsync(org)).Single();

        // Act
        await org.Owner.Client.PutAsync<int>("reviews/read", new { organizationId = org.OrganizationId, reviewIds = new[] { review.Id }, isRead = true });
        var (_, read) = await org.Owner.Client.PatchListAsync<ReviewListDto>("reviews", new { organizationId = org.OrganizationId, isRead = true, pageNumber = 1, rowCount = 10 });
        var (foreignRead, _) = await other.Owner.Client.PutAsync<int>("reviews/read", new { organizationId = other.OrganizationId, reviewIds = new[] { review.Id }, isRead = true });

        var (_, savedId) = await org.Owner.Client.PostAsync<int>("reviews/saved-reply", new { organizationId = org.OrganizationId, name = "Thanks", variants = new[] { new { locale = "tr", text = "Merhaba {ad}, {uygulama} için teşekkürler." }, new { locale = "en-US", text = "Hi {name}, thanks for using {app}." } }, appIds = new[] { appId } });
        var (badVariable, _) = await org.Owner.Client.PostAsync<int>("reviews/saved-reply", new { organizationId = org.OrganizationId, name = "Bad", variants = new[] { new { locale = "tr", text = "Merhaba {isim}" } } });
        var (_, updated) = await org.Owner.Client.PutAsync<int>("reviews/saved-reply", new { organizationId = org.OrganizationId, savedReplyId = savedId.Data, name = "Thank you" });
        var (_, replies) = await org.Owner.Client.GetAsync<List<SavedReplyDto>>($"reviews/saved-replies?organizationId={org.OrganizationId}&locale=en-GB&appId={appId}");
        var (foreignUpdate, _) = await other.Owner.Client.PutAsync<int>("reviews/saved-reply", new { organizationId = other.OrganizationId, savedReplyId = savedId.Data, name = "Hijack" });
        var (deleted, _) = await org.Owner.Client.SendAsync<int>(HttpMethod.Delete, "reviews/saved-reply", new { organizationId = org.OrganizationId, savedReplyId = savedId.Data });
        var (_, afterDelete) = await org.Owner.Client.GetAsync<List<SavedReplyDto>>($"reviews/saved-replies?organizationId={org.OrganizationId}");

        // Assert
        read.Data.Should().ContainSingle(r => r.Id == review.Id && r.IsRead);
        foreignRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
        badVariable.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        updated.IsSuccess.Should().BeTrue();
        replies.Data.Should().ContainSingle().Which.Name.Should().Be("Thank you");
        foreignUpdate.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        afterDelete.Data.Should().BeEmpty();
    }
}
