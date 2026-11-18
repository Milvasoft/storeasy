using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.IntegrationTests.TestBase;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "TestFlight: sync, groups, testers, beta texts, beta review (fake store, nothing is submitted), expiry reminders.")]
public class TestFlightControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private sealed record Setup(EventOrg Org, int AppId, int StoreAppId, string ExternalId, long ValidBuildId, long ExpiredBuildId);

    private async Task<Setup> NewSetupAsync()
    {
        var org = await _factory.NewOrgAsync();
        var (appId, storeAppId, externalId) = await _factory.AddAppAsync(org, name: "Halka");
        var valid = await _factory.AddBuildAsync(org.OrganizationId, storeAppId, $"b37-{org.Suffix}", "1.0", "37", DateTime.UtcNow.AddDays(6));
        var expired = await _factory.AddBuildAsync(org.OrganizationId, storeAppId, $"b35-{org.Suffix}", "1.0", "35", DateTime.UtcNow.AddDays(-9), BuildProcessingState.Expired);

        _factory.StoreEvents.TestFlight[externalId] = new TestFlightSnapshot(
            [new BetaGroupRecord($"g-in-{org.Suffix}", "Kuzey içi", true, false, null, null, false, [$"b37-{org.Suffix}"]),
             new BetaGroupRecord($"g-ex-{org.Suffix}", "Erken erişim", false, true, "https://testflight.apple.com/join/abc", 100, true, [$"b37-{org.Suffix}"])],
            [new BetaTesterRecord($"t1-{org.Suffix}", "Aylin", "Çelik", "aylin@example.com", BetaInviteType.Email, BetaTesterState.Installed, [$"g-ex-{org.Suffix}"]),
             new BetaTesterRecord($"t2-{org.Suffix}", "Burak", "Şahin", null, BetaInviteType.PublicLink, BetaTesterState.Accepted, [$"g-ex-{org.Suffix}", $"g-in-{org.Suffix}"])],
            [new BetaBuildRecord($"b37-{org.Suffix}", BetaReviewState.Approved, [new LocalizedText { Locale = "tr", Text = "Davet akışını dene" }], DateTime.UtcNow.AddDays(6), false)],
            [new BetaLocalizationRecord($"l-{org.Suffix}", "tr", "Halka beta", "beta@example.com", null, null)]);

        var (_, job) = await org.Owner.Client.PostAsync<long>("testflight/sync", new { organizationId = org.OrganizationId, storeAppId });
        (await _factory.WaitForJobAsync(job.Data)).Status.Should().Be(StoreJobStatus.Succeeded);

        return new Setup(org, appId, storeAppId, externalId, valid, expired);
    }

    [Fact]
    public async Task Sync_ShouldBringGroupsTestersBuildsAndTexts()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var org = setup.Org;

        // Act
        var (_, builds) = await org.Owner.Client.GetAsync<List<BetaBuildDto>>($"testflight/builds?organizationId={org.OrganizationId}&storeAppId={setup.StoreAppId}");
        var (_, groups) = await org.Owner.Client.GetAsync<List<BetaGroupDto>>($"testflight/groups?organizationId={org.OrganizationId}&storeAppId={setup.StoreAppId}");
        var external = groups.Data.Single(g => !g.IsInternal);
        var (_, testers) = await org.Owner.Client.PatchListAsync<BetaTesterDto>("testflight/testers", new { organizationId = org.OrganizationId, storeAppId = setup.StoreAppId, groupId = groups.Data.Single(g => g.IsInternal).Id, pageNumber = 1, rowCount = 10 });
        var (_, texts) = await org.Owner.Client.GetAsync<List<BetaLocalizationDto>>($"testflight/localizations?organizationId={org.OrganizationId}&storeAppId={setup.StoreAppId}");

        // Assert
        builds.Data.Should().HaveCount(2);
        builds.Data.Single(b => b.BuildId == setup.ValidBuildId).Should().Match<BetaBuildDto>(b => !b.Expired && b.DaysLeft == 5 && b.BetaReviewState == BetaReviewState.Approved && b.GroupIds.Count == 2 && b.WhatToTest.Count == 1);
        builds.Data.Single(b => b.BuildId == setup.ExpiredBuildId).Expired.Should().BeTrue();
        external.Should().Match<BetaGroupDto>(g => g.TesterCount == 2 && g.PublicLinkEnabled && g.BuildIds.Contains(setup.ValidBuildId));
        testers.Data.Should().ContainSingle(t => t.FirstName == "Burak" && t.GroupExternalIds.Count == 2);
        texts.Data.Should().ContainSingle(t => t.Locale == "tr" && t.FeedbackEmail == "beta@example.com");
    }

    [Fact]
    public async Task Writes_ShouldBeStoreJobsAndBetaReviewShouldNeedAConfirmation()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var org = setup.Org;
        var (_, groups) = await org.Owner.Client.GetAsync<List<BetaGroupDto>>($"testflight/groups?organizationId={org.OrganizationId}&storeAppId={setup.StoreAppId}");
        var external = groups.Data.Single(g => !g.IsInternal);
        var internalGroup = groups.Data.Single(g => g.IsInternal);

        // Act
        var (_, create) = await org.Owner.Client.PostAsync<long>("testflight/group", new { organizationId = org.OrganizationId, storeAppId = setup.StoreAppId, name = "QA", isInternal = false, publicLinkEnabled = true });
        var (_, testers) = await org.Owner.Client.PutAsync<long>("testflight/group/testers", new { organizationId = org.OrganizationId, groupId = external.Id, add = new[] { new { email = "cem@example.com", firstName = "Cem" } } });
        var (linkOnInternal, _) = await org.Owner.Client.PutAsync<long>("testflight/group", new { organizationId = org.OrganizationId, groupId = internalGroup.Id, publicLinkEnabled = true });
        var (_, whatToTest) = await org.Owner.Client.PutAsync<long>("testflight/build/what-to-test", new { organizationId = org.OrganizationId, buildId = setup.ValidBuildId, locale = "en-US", whatsNew = "Try the invite flow." });
        var (expiredText, _) = await org.Owner.Client.PutAsync<long>("testflight/build/what-to-test", new { organizationId = org.OrganizationId, buildId = setup.ExpiredBuildId, locale = "en-US", whatsNew = "x" });
        var (unconfirmed, _) = await org.Owner.Client.PostAsync<long>("testflight/build/beta-review", new { organizationId = org.OrganizationId, buildId = setup.ValidBuildId, confirm = true, confirmVersion = "1.0" });
        var (approvedAgain, _) = await org.Owner.Client.PostAsync<long>("testflight/build/beta-review", new { organizationId = org.OrganizationId, buildId = setup.ValidBuildId, confirm = true, confirmVersion = "1.0 (37)" });

        foreach (var jobId in new[] { create.Data, testers.Data, whatToTest.Data })
            (await _factory.WaitForJobAsync(jobId)).Status.Should().Be(StoreJobStatus.Succeeded);

        var changes = _factory.StoreEvents.Changes.Where(c => c.AppId == setup.ExternalId).Select(c => c.Change).ToList();

        // Assert
        changes.Should().Contain(c => c.Operation == TestFlightOperation.CreateGroup && c.Name == "QA" && c.PublicLinkEnabled == true);
        changes.Should().Contain(c => c.Operation == TestFlightOperation.UpdateTesters && c.AddTesters.Single().Email == "cem@example.com" && c.GroupExternalId == $"g-ex-{org.Suffix}");
        changes.Should().Contain(c => c.Operation == TestFlightOperation.WhatToTest && c.BuildExternalId == $"b37-{org.Suffix}");
        changes.Should().NotContain(c => c.Operation == TestFlightOperation.SubmitBetaReview);
        linkOnInternal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        expiredText.StatusCode.Should().Be(HttpStatusCode.Conflict);
        unconfirmed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        approvedAgain.StatusCode.Should().Be(HttpStatusCode.Conflict, "an approved build is not submitted again");
    }

    [Fact]
    public async Task BetaReview_ShouldSubmitThroughTheFakeOnly()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (_, storeAppId, externalId) = await _factory.AddAppAsync(org);
        var buildId = await _factory.AddBuildAsync(org.OrganizationId, storeAppId, $"b9-{org.Suffix}", "2.0", "9", DateTime.UtcNow.AddDays(80));

        // Act
        var (_, job) = await org.Owner.Client.PostAsync<long>("testflight/build/beta-review", new { organizationId = org.OrganizationId, buildId, confirm = true, confirmVersion = "2.0 (9)" });
        (await _factory.WaitForJobAsync(job.Data)).Status.Should().Be(StoreJobStatus.Succeeded);
        var beta = await _factory.DbAsync(db => db.BetaBuilds.AsNoTracking().SingleAsync(b => b.BuildId == buildId));

        // Assert
        _factory.StoreEvents.Changes.Should().Contain(c => c.AppId == externalId && c.Change.Operation == TestFlightOperation.SubmitBetaReview && c.Change.BuildExternalId == $"b9-{org.Suffix}");
        beta.BetaReviewState.Should().Be(BetaReviewState.WaitingForReview);
        beta.BetaReviewSubmittedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task OtherOrganization_ShouldGet404()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var other = await _factory.NewOrgAsync();

        // Act
        var (builds, _) = await other.Owner.Client.GetAsync<List<BetaBuildDto>>($"testflight/builds?organizationId={other.OrganizationId}&storeAppId={setup.StoreAppId}");
        var (review, _) = await other.Owner.Client.PostAsync<long>("testflight/build/beta-review", new { organizationId = other.OrganizationId, buildId = setup.ValidBuildId, confirm = true, confirmVersion = "1.0 (37)" });
        var (group, _) = await other.Owner.Client.PostAsync<long>("testflight/group", new { organizationId = other.OrganizationId, storeAppId = setup.StoreAppId, name = "x" });

        // Assert
        builds.StatusCode.Should().Be(HttpStatusCode.NotFound);
        review.StatusCode.Should().Be(HttpStatusCode.NotFound);
        group.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
