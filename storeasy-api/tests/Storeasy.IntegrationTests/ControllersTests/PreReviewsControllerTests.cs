using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Dtos.PreReviewDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "AI pre-reviews: the job against the fake App Store Connect and a fake Messages API, history, permissions, models.")]
public class PreReviewsControllerTests(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    private const string _report = """
        {
          "verdict": "at_risk",
          "approval_likelihood": 62,
          "summary": "The listing is mostly fine, but the review notes give no demo account.",
          "findings": [
            { "severity": "minor", "area": "metadata", "guideline": "App Store Review Guideline 2.3.7 (Accurate Metadata)", "title": "Keyword list in subtitle", "evidence": "subtitle, en-US", "fix": "Write a short sentence instead." },
            { "severity": "major", "area": "functionality", "guideline": "App Store Review Guideline 2.1 (App Completeness)", "title": "No demo account", "evidence": "review_information", "fix": "Add a demo account in the review information." }
          ],
          "runtime_checks": ["Sign in with the demo account", "Restore purchases"],
          "positive_notes": ["Privacy policy URL is set"]
        }
        """;

    private async Task<(Org Org, Record Record, string Key)> NewOrgWithAppAndKeyAsync(bool saveKey = true)
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";

        _factory.Stores.Apple.AddApp(appleId);

        var (record, _) = await NewAppAsync(org, appleId);
        var key = FakeAnthropic.Key(org.Suffix);

        if (saveKey)
            await org.Owner.Client.PutAsync<AiSettingsDto>("organizations/ai-settings/key", new { organizationId = org.OrganizationId, apiKey = key });

        return (org, record, key);
    }

    [Fact]
    public async Task Start_ShouldReviewTheSubmissionAsAJobAndKeepTheReportInTheHistory()
    {
        // Arrange
        var (org, record, key) = await NewOrgWithAppAndKeyAsync();
        _factory.Anthropic.Replies[key] = (HttpStatusCode.OK, "end_turn", _report);

        // Act
        var (http, started) = await org.Owner.Client.PostAsync<PreReviewDto>("pre-reviews/pre-review", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple });
        var job = await WaitForJobAsync(started.Data.JobId!.Value);
        var (_, detail) = await org.Owner.Client.GetAsync<PreReviewDto>($"pre-reviews/pre-review?organizationId={org.OrganizationId}&id={started.Data.Id}");
        var (_, history) = await org.Owner.Client.GetAsync<List<PreReviewListDto>>($"pre-reviews?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Apple}");
        var sent = _factory.Anthropic.Requests.Last(r => r.ApiKey == key && r.Path == "/v1/messages");
        var audits = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId && (a.Action == AuditAction.PreReviewStarted || a.Action == AuditAction.PreReviewCompleted)).ToListAsync());

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        started.Data.Status.Should().Be(PreReviewStatus.Queued);
        started.Data.Model.Should().Be("claude-sonnet-5-5", "the pre-review default is Claude Sonnet 5.5");
        job.Status.Should().Be(StoreJobStatus.Succeeded);

        detail.Data.Status.Should().Be(PreReviewStatus.Succeeded);
        detail.Data.Verdict.Should().Be(PreReviewVerdict.AtRisk);
        detail.Data.ApprovalLikelihood.Should().Be(62);
        detail.Data.Findings.Select(f => f.Severity).Should().Equal(PreReviewSeverity.Major, PreReviewSeverity.Minor);
        detail.Data.Findings[0].Guideline.Should().Contain("2.1");
        detail.Data.RuntimeChecks.Should().Equal("Sign in with the demo account", "Restore purchases");
        detail.Data.PositiveNotes.Should().ContainSingle();
        detail.Data.InputTokens.Should().Be(400);
        detail.Data.OutputTokens.Should().Be(60);
        detail.Data.EstimatedCostUsd.Should().Be(0.0014m, "400 input tokens at $2 and 60 output tokens at $10 per million");
        detail.Data.CompletedDate.Should().NotBeNull();

        history.Data.Should().ContainSingle().Which.Should().Match<PreReviewListDto>(h => h.Id == started.Data.Id && h.Verdict == PreReviewVerdict.AtRisk && h.MajorCount == 1 && h.MinorCount == 1 && h.BlockerCount == 0);

        var body = JsonNode.Parse(sent.Body)!.AsObject();
        body["model"]!.GetValue<string>().Should().Be("claude-sonnet-5-5");
        body["output_config"]!["effort"]!.GetValue<string>().Should().Be("medium");
        body["output_config"]!["format"]!["type"]!.GetValue<string>().Should().Be("json_schema");
        body["fallbacks"]!.GetValue<string>().Should().Be("default");
        body.Should().NotContainKeys("temperature", "top_p", "top_k");
        sent.Beta.Should().Contain("server-side-fallback-2026-07-01");
        body["messages"]![0]!["content"]![0]!["text"]!.GetValue<string>().Should().Contain("<submission>").And.Contain("Report language:");

        audits.Select(a => a.Action).Should().BeEquivalentTo([AuditAction.PreReviewStarted, AuditAction.PreReviewCompleted]);
        audits.Should().OnlyContain(a => a.Details == null || (!a.Details.Contains("sk-ant") && !a.Details.Contains("demo account")));
    }

    [Fact]
    public async Task Start_ShouldCheckTheKeyThePermissionAndOnePreReviewAtATime()
    {
        // Arrange
        var (org, record, key) = await NewOrgWithAppAndKeyAsync(saveKey: false);
        var viewer = await MemberAsync(org, OrgRole.Viewer);
        var foreign = await NewOrgAsync();
        var request = new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple };

        // Act
        var (noKey, noKeyBody) = await org.Owner.Client.PostAsync<PreReviewDto>("pre-reviews/pre-review", request);
        await org.Owner.Client.PutAsync<AiSettingsDto>("organizations/ai-settings/key", new { organizationId = org.OrganizationId, apiKey = key });
        var (viewerStart, _) = await viewer.Client.PostAsync<PreReviewDto>("pre-reviews/pre-review", request);
        var (viewerList, _) = await viewer.Client.GetAsync<List<PreReviewListDto>>($"pre-reviews?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Apple}");
        var (otherStore, _) = await org.Owner.Client.PostAsync<PreReviewDto>("pre-reviews/pre-review", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Google });

        // A pre-review that has not reached the queue yet counts as running.
        await _factory.DbAsync(async db =>
        {
            db.PreReviews.Add(new PreReview { OrganizationId = org.OrganizationId, AppId = record.AppId, StoreAppId = record.StoreAppId, Store = StoreKind.Apple, Status = PreReviewStatus.Queued, Model = "claude-sonnet-5-5", Language = "en-US", CreationDate = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        var (busy, busyBody) = await org.Owner.Client.PostAsync<PreReviewDto>("pre-reviews/pre-review", request);
        var queued = await _factory.DbAsync(db => db.PreReviews.AsNoTracking().SingleAsync(r => r.OrganizationId == org.OrganizationId));
        var (foreignDetail, _) = await foreign.Owner.Client.GetAsync<PreReviewDto>($"pre-reviews/pre-review?organizationId={foreign.OrganizationId}&id={queued.Id}");
        var (foreignList, foreignListBody) = await foreign.Owner.Client.GetAsync<List<PreReviewListDto>>($"pre-reviews?organizationId={foreign.OrganizationId}&appId={record.AppId}&store={StoreKind.Apple}");

        // Assert
        noKey.StatusCode.Should().Be(HttpStatusCode.Conflict);
        noKeyBody.Messages.Should().Contain(m => m.Key == "AiKeyNotSet");
        viewerStart.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        viewerList.StatusCode.Should().Be(HttpStatusCode.OK);
        otherStore.StatusCode.Should().Be(HttpStatusCode.NotFound, "the app has no Google Play record");
        busy.StatusCode.Should().Be(HttpStatusCode.Conflict);
        busyBody.Messages.Should().Contain(m => m.Key == "PreReviewInProgress");
        foreignDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignList.StatusCode.Should().Be(HttpStatusCode.OK);
        foreignListBody.Data.Should().BeEmpty();
        _factory.Anthropic.Requests.Should().NotContain(r => r.ApiKey == key && r.Path == "/v1/messages", "nothing was reviewed");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "refusal", "", "PreReviewRefused")]
    [InlineData(HttpStatusCode.OK, "end_turn", "not a report", "PreReviewFailed")]
    [InlineData(HttpStatusCode.OK, "max_tokens", "{\"verdict\":", "PreReviewFailed")]
    [InlineData(HttpStatusCode.TooManyRequests, null, null, "AiRateLimited")]
    [InlineData(HttpStatusCode.Unauthorized, null, null, "AiKeyRejected")]
    public async Task Start_WhenAnthropicFails_ShouldSaveTheFailureWithAClearMessage(HttpStatusCode status, string stopReason, string text, string errorKey)
    {
        // Arrange
        var (org, record, key) = await NewOrgWithAppAndKeyAsync();
        _factory.Anthropic.Replies[key] = (status, stopReason, text);

        // Act
        var (_, started) = await org.Owner.Client.PostAsync<PreReviewDto>("pre-reviews/pre-review", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple });
        var job = await WaitForJobAsync(started.Data.JobId!.Value);
        var (_, detail) = await org.Owner.Client.GetAsync<PreReviewDto>($"pre-reviews/pre-review?organizationId={org.OrganizationId}&id={started.Data.Id}");
        var (again, _) = await org.Owner.Client.PostAsync<PreReviewDto>("pre-reviews/pre-review", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple });

        // Assert
        job.Status.Should().Be(StoreJobStatus.Failed);
        job.MaxAttempts.Should().Be(1, "every attempt costs the organization money");
        detail.Data.Status.Should().Be(PreReviewStatus.Failed);
        detail.Data.ErrorKey.Should().Be(errorKey);
        detail.Data.ErrorMessage.Should().NotBeNullOrWhiteSpace().And.NotBe(errorKey).And.NotContain("raw provider message");
        detail.Data.Verdict.Should().BeNull();
        again.StatusCode.Should().Be(HttpStatusCode.OK, "a failed pre-review does not block the next one");
    }

    [Fact]
    public async Task Models_ShouldBeChosenPerFeatureWithAKey()
    {
        // Arrange
        var (org, record, key) = await NewOrgWithAppAndKeyAsync(saveKey: false);
        var viewer = await MemberAsync(org, OrgRole.Viewer);
        var models = new { organizationId = org.OrganizationId, replyDraftModel = "claude-haiku-5-5", preReviewModel = "claude-opus-5-5" };

        // Act
        var (_, defaults) = await org.Owner.Client.GetAsync<AiSettingsDto>($"organizations/ai-settings?organizationId={org.OrganizationId}");
        var (noKey, _) = await org.Owner.Client.PutAsync<AiSettingsDto>("organizations/ai-settings/models", models);
        await org.Owner.Client.PutAsync<AiSettingsDto>("organizations/ai-settings/key", new { organizationId = org.OrganizationId, apiKey = key });
        var (viewerSet, _) = await viewer.Client.PutAsync<AiSettingsDto>("organizations/ai-settings/models", models);
        var (invalid, _) = await org.Owner.Client.PutAsync<AiSettingsDto>("organizations/ai-settings/models", new { organizationId = org.OrganizationId, preReviewModel = "claude-made-up" });
        var (_, saved) = await org.Owner.Client.PutAsync<AiSettingsDto>("organizations/ai-settings/models", models);
        var (_, read) = await viewer.Client.GetAsync<AiSettingsDto>($"organizations/ai-settings?organizationId={org.OrganizationId}");
        _factory.Anthropic.Replies[key] = (HttpStatusCode.OK, "end_turn", _report);
        var (_, started) = await org.Owner.Client.PostAsync<PreReviewDto>("pre-reviews/pre-review", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple });
        await WaitForJobAsync(started.Data.JobId!.Value);
        var sent = JsonNode.Parse(_factory.Anthropic.Requests.Last(r => r.ApiKey == key && r.Path == "/v1/messages").Body)!;
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().SingleAsync(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.AiModelsChanged));

        // Assert
        defaults.Data.ReplyDraftModel.Should().Be("claude-haiku-5-5");
        defaults.Data.PreReviewModel.Should().Be("claude-sonnet-5-5");
        defaults.Data.Models.Select(m => m.Id).Should().Equal("claude-haiku-5-5", "claude-sonnet-5-5", "claude-opus-5-5");
        defaults.Data.Models.Single(m => m.Id == "claude-sonnet-5-5").Should().Match<AiModelOptionDto>(m => m.InputUsdPerMTok == 2m && m.OutputUsdPerMTok == 10m);
        noKey.StatusCode.Should().Be(HttpStatusCode.Conflict);
        viewerSet.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        saved.Data.PreReviewModel.Should().Be("claude-opus-5-5");
        read.Data.PreReviewModel.Should().Be("claude-opus-5-5");
        read.Data.ReplyDraftModel.Should().Be("claude-haiku-5-5");
        started.Data.Model.Should().Be("claude-opus-5-5");
        sent["model"]!.GetValue<string>().Should().Be("claude-opus-5-5");
        audit.Details.Should().Contain("claude-opus-5-5");
    }
}
