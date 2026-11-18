using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Rule watcher: sources, change detection, proposals, decisions (platform admins).")]
public class RuleWatcherControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private const string _upcoming = "https://developer.apple.com/news/upcoming-requirements/";

    [Fact]
    public async Task Watcher_ShouldProposeChangesAndNotifyAffectedAppsOnApproval()
    {
        // Arrange
        var root = await _factory.LoginAsRootAsync();
        var org = await _factory.NewOrgAsync();
        var (appId, _, _) = await _factory.AddAppAsync(org, name: "Fenpo");
        const string ruleId = "apple.age-rating.shared-location-dating";

        foreach (var source in (await root.Client.GetAsync<List<RuleSourceDto>>("rules/sources")).Body.Data)
            _factory.RulePages.Pages[source.Url] = "<html><body><p>Same</p></body></html>";

        _factory.RulePages.Pages[_upcoming] = "<html><script>x()</script><body><h1>Upcoming</h1><p>Duo screenshots from April 2027.</p></body></html>";

        // Act
        var (_, baseline) = await root.Client.PostAsync<RuleWatchResultDto>("rules/sources/fetch", new { });
        _factory.RulePages.Pages[_upcoming] = "<html><body><h1>Upcoming</h1><p>Duo screenshots from May 2027.</p></body></html>";
        var (_, changed) = await root.Client.PostAsync<RuleWatchResultDto>("rules/sources/fetch", new { });
        var (_, again) = await root.Client.PostAsync<RuleWatchResultDto>("rules/sources/fetch", new { });
        var (_, proposals) = await root.Client.PatchListAsync<RuleProposalListDto>("rules/proposals", new { pageNumber = 1, rowCount = 50 });
        var proposal = proposals.Data.First(p => p.Title.StartsWith("Apple upcoming requirements") && p.Status == RuleProposalStatus.Pending);
        await root.Client.PutAsync<int>("rules/proposal/prepare", new { proposalId = proposal.Id, ruleIds = new[] { ruleId }, catalogPatch = "{\"from\":\"2027-05-01\"}", note = "Date moved" });
        var (_, detail) = await root.Client.GetAsync<RuleProposalDetailDto>($"rules/proposals/proposal?proposalId={proposal.Id}");
        var (_, decision) = await root.Client.PutAsync<RuleProposalDecisionDto>("rules/proposal/approve", new { proposalId = proposal.Id, notify = true });
        var (twice, _) = await root.Client.PutAsync<RuleProposalDecisionDto>("rules/proposal/approve", new { proposalId = proposal.Id, notify = true });
        var notified = await _factory.DbAsync(db => db.InternalNotifications.AnyAsync(n => n.RecipientUserId == org.Owner.Id && n.Type == NotificationType.RuleChanged));

        // Assert
        changed.Data.Changed.Should().BeGreaterThanOrEqualTo(1);
        again.Data.ProposalsOpened.Should().Be(0, "the same content never opens a second proposal");
        detail.Data.SourceExcerptBefore.Should().Contain("April 2027").And.NotContain("x()");
        detail.Data.SourceExcerptAfter.Should().Contain("May 2027");
        detail.Data.PreparedBy.Should().Be(RuleProposalPreparer.Mcp);
        detail.Data.Impact.Should().Contain(i => i.AppId == appId && i.RuleId == ruleId);
        decision.Data.CatalogVersion.Should().NotBeNullOrEmpty();
        decision.Data.NotifiedOrganizationCount.Should().BeGreaterThanOrEqualTo(1);
        twice.StatusCode.Should().Be(HttpStatusCode.Conflict);
        notified.Should().BeTrue();
        baseline.Data.Checked.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Reject_ShouldNeedAReasonAndOrganizationUsersShouldBeForbidden()
    {
        // Arrange
        var root = await _factory.LoginAsRootAsync();
        var user = await _factory.CreateAndLoginUserAsync();
        var proposalId = await _factory.DbAsync(async db =>
        {
            var proposal = new Domain.Rules.RuleProposal { Title = $"Manual {TestApi.Unique()}", Store = StoreKind.Google, ContentHash = TestApi.Unique(), CreationDate = DateTime.UtcNow };
            db.RuleProposals.Add(proposal);
            await db.SaveChangesAsync();
            return proposal.Id;
        });

        // Act
        var (forbidden, _) = await user.Client.GetAsync<List<RuleSourceDto>>("rules/sources");
        var (rejected, _) = await root.Client.PutAsync<int>("rules/proposal/reject", new { proposalId, reason = RuleProposalRejectReason.FormattingOnly, note = "Layout only" });
        var row = await _factory.DbAsync(db => db.RuleProposals.AsNoTracking().SingleAsync(p => p.Id == proposalId));

        // Assert
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        rejected.StatusCode.Should().Be(HttpStatusCode.OK);
        row.Status.Should().Be(RuleProposalStatus.Rejected);
        row.RejectReason.Should().Be(RuleProposalRejectReason.FormattingOnly);
    }
}
