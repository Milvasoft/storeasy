using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Rejection flow: cases from the version state sync, pasted text, checklist, reply draft, close, history.")]
public class RejectionsControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private const string _reviewText = """
        Guideline 2.1 - Performance - App Completeness
        We were unable to sign in with the demo account you provided.

        Guideline 5.1.1(v) - Legal - Privacy - Data Collection and Storage
        The app supports account creation but does not include an option to initiate account deletion.
        """;

    private async Task SyncVersionsAsync(EventOrg org)
    {
        var jobIds = await _factory.EnqueueAsync(new StoreJobRequest(org.OrganizationId, StoreJobKind.SyncVersionStates, new SyncVersionStatesPayload(org.AppleConnectionId), StoreKind.Apple));

        (await _factory.WaitForJobAsync(jobIds[0])).Status.Should().Be(StoreJobStatus.Succeeded);
    }

    [Fact]
    public async Task Sync_ShouldOpenACaseOnRejectionAndCloseItOnResubmission()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (appId, storeAppId, externalId) = await _factory.AddAppAsync(org);
        _factory.StoreEvents.VersionLists[externalId] = [new StoreVersionRecord($"v1-{org.Suffix}", externalId, "1.3", "IOS", "READY_FOR_SALE", "40", DateTime.UtcNow.AddDays(-20)), new StoreVersionRecord($"v2-{org.Suffix}", externalId, "1.4", "IOS", "REJECTED", "52", DateTime.UtcNow)];

        // Act
        await SyncVersionsAsync(org);
        var (_, opened) = await org.Owner.Client.PatchListAsync<RejectionCaseListDto>("rejections", new { organizationId = org.OrganizationId, appId, pageNumber = 1, rowCount = 10 });

        _factory.StoreEvents.VersionLists[externalId] = [new StoreVersionRecord($"v2-{org.Suffix}", externalId, "1.4", "IOS", "WAITING_FOR_REVIEW", "53", DateTime.UtcNow)];
        await SyncVersionsAsync(org);
        await SyncVersionsAsync(org);

        var (_, closed) = await org.Owner.Client.GetAsync<RejectionCaseDto>($"rejections/case?organizationId={org.OrganizationId}&caseId={opened.Data.Single().Id}");
        var (_, history) = await org.Owner.Client.GetAsync<RejectionHistoryDto>($"rejections/history?organizationId={org.OrganizationId}&appId={appId}");
        var caseCount = await _factory.DbAsync(db => db.RejectionCases.CountAsync(c => c.StoreAppId == storeAppId));

        // Assert
        opened.Data.Should().ContainSingle().Which.Should().Match<RejectionCaseListDto>(c => c.VersionString == "1.4" && c.State == RejectionState.Open && c.OpenedVia == RejectionSource.Sync);
        closed.Data.State.Should().Be(RejectionState.Closed);
        closed.Data.ClosedVia.Should().Be(RejectionCloseReason.Submission);
        closed.Data.ResolutionCenterUrl.Should().Contain(externalId);
        caseCount.Should().Be(1, "a state seen twice is applied once");
        history.Data.Submissions.Select(s => s.VersionString).Should().BeEquivalentTo(["1.3", "1.4"]);
        history.Data.RejectionCount.Should().Be(1);
    }

    [Fact]
    public async Task Case_ShouldParseGuidelinesKeepTheDraftAndOnlyMarkFittingRepliesSent()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (appId, _, _) = await _factory.AddAppAsync(org);
        var (_, caseId) = await org.Owner.Client.PostAsync<int>("rejections/case", new { organizationId = org.OrganizationId, appId, versionString = "1.4", buildNumber = "52" });

        // Act
        var (_, withText) = await org.Owner.Client.PutAsync<RejectionCaseDto>("rejections/case/text", new { organizationId = org.OrganizationId, caseId = caseId.Data, rawText = _reviewText });
        var (_, noGuideline) = await org.Owner.Client.PutAsync<RejectionCaseDto>("rejections/case/text", new { organizationId = org.OrganizationId, caseId = caseId.Data, rawText = "There's an issue with your submission. Please visit App Store Connect." });
        await org.Owner.Client.PutAsync<RejectionCaseDto>("rejections/case/text", new { organizationId = org.OrganizationId, caseId = caseId.Data, rawText = _reviewText });

        var items = withText.Data.Checklist.Select(i => new { i.Id, i.GuidelineCode, i.Text, Status = i.GuidelineCode == "2.1" ? ChecklistItemStatus.Done : i.Status }).ToList<object>();
        items.Add(new { GuidelineCode = (string)null, Text = "Add account deletion to the privacy policy", Status = ChecklistItemStatus.Todo });
        var (_, checklist) = await org.Owner.Client.PutAsync<RejectionCaseDto>("rejections/case/checklist", new { organizationId = org.OrganizationId, caseId = caseId.Data, items });

        var (_, longDraft) = await org.Owner.Client.PutAsync<RejectionReplyDraftDto>("rejections/case/reply-draft", new { organizationId = org.OrganizationId, caseId = caseId.Data, body = new string('a', 4001) });
        var (tooLong, _) = await org.Owner.Client.PutAsync<RejectionReplyDraftDto>("rejections/case/reply-sent", new { organizationId = org.OrganizationId, caseId = caseId.Data });
        await org.Owner.Client.PutAsync<RejectionReplyDraftDto>("rejections/case/reply-draft", new { organizationId = org.OrganizationId, caseId = caseId.Data, body = "Hello App Review team,\r\nwe fixed both issues." });
        var (_, sent) = await org.Owner.Client.PutAsync<RejectionReplyDraftDto>("rejections/case/reply-sent", new { organizationId = org.OrganizationId, caseId = caseId.Data });
        var (_, closed) = await org.Owner.Client.PutAsync<int>("rejections/case/close", new { organizationId = org.OrganizationId, caseId = caseId.Data, note = "Version withdrawn" });
        var (afterClose, _) = await org.Owner.Client.PutAsync<RejectionReplyDraftDto>("rejections/case/reply-draft", new { organizationId = org.OrganizationId, caseId = caseId.Data, body = "late" });
        var (_, detail) = await org.Owner.Client.GetAsync<RejectionCaseDto>($"rejections/case?organizationId={org.OrganizationId}&caseId={caseId.Data}");

        // Assert
        withText.Data.Guidelines.Select(g => g.Code).Should().Equal("2.1", "5.1.1");
        withText.Data.Guidelines[1].Title.Should().Be("Legal - Privacy - Data Collection and Storage");
        withText.Data.Checklist.Should().HaveCount(2);
        noGuideline.Messages.Should().Contain(m => m.Type == Milvasoft.Components.Rest.Enums.MessageType.Warning);
        noGuideline.Data.Guidelines.Should().BeEmpty();
        checklist.Data.Checklist.Should().HaveCount(3).And.ContainSingle(i => i.Status == ChecklistItemStatus.Done && i.DoneDate != null);
        longDraft.Data.OverLimit.Should().BeTrue();
        longDraft.Data.Length.Should().Be(4001);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        sent.Data.SentDate.Should().NotBeNull();
        sent.Data.Length.Should().Be("Hello App Review team,\nwe fixed both issues.".Length, "a line break counts once");
        closed.IsSuccess.Should().BeTrue();
        afterClose.StatusCode.Should().Be(HttpStatusCode.Conflict);
        detail.Data.Timeline.Select(t => t.Kind).Should().Contain(["opened", "text", "checklist", "draft", "sent", "closed"]);
    }

    [Fact]
    public async Task Cases_ShouldStayInTheirOrganizationAndViewersShouldOnlyRead()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var other = await _factory.NewOrgAsync();
        var (appId, _, _) = await _factory.AddAppAsync(org);
        var (_, caseId) = await org.Owner.Client.PostAsync<int>("rejections/case", new { organizationId = org.OrganizationId, appId, versionString = "2.0" });
        var viewer = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Viewer);

        // Act
        var (foreign, _) = await other.Owner.Client.GetAsync<RejectionCaseDto>($"rejections/case?organizationId={other.OrganizationId}&caseId={caseId.Data}");
        var (foreignText, _) = await other.Owner.Client.PutAsync<RejectionCaseDto>("rejections/case/text", new { organizationId = other.OrganizationId, caseId = caseId.Data, rawText = _reviewText });
        var (foreignHistory, _) = await other.Owner.Client.GetAsync<RejectionHistoryDto>($"rejections/history?organizationId={other.OrganizationId}&appId={appId}");
        var (viewerRead, _) = await viewer.Client.GetAsync<RejectionCaseDto>($"rejections/case?organizationId={org.OrganizationId}&caseId={caseId.Data}");
        var (viewerWrite, _) = await viewer.Client.PutAsync<RejectionCaseDto>("rejections/case/text", new { organizationId = org.OrganizationId, caseId = caseId.Data, rawText = _reviewText });
        var duplicate = await org.Owner.Client.PostAsync<int>("rejections/case", new { organizationId = org.OrganizationId, appId, versionString = "2.0" });

        // Assert
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignText.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignHistory.StatusCode.Should().Be(HttpStatusCode.NotFound);
        viewerRead.StatusCode.Should().Be(HttpStatusCode.OK);
        viewerWrite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        duplicate.Http.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
