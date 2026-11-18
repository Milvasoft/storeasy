using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.AnswerDtos;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Saved answers, assisted steps, rules catalog and the compliance calendar.")]
public class AnswersStepsRulesTests(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    [Fact]
    public async Task SavedAnswers_ShouldBeTypedSealAndNeverReturnOrCopyThePassword()
    {
        var org = await NewOrgAsync();
        var (source, _) = await NewAppAsync(org, $"a{org.Suffix}");
        var (target, _) = await NewAppAsync(org, $"b{org.Suffix}");
        var other = await NewOrgAsync();

        var (http, _) = await org.Owner.Client.PutAsync<int>("apps/answers", new
        {
            organizationId = org.OrganizationId,
            appId = source.AppId,
            key = SavedAnswerKind.ReviewLogin,
            answers = new { contactFirstName = "Ada", contactEmail = "ada@example.test", demoAccountRequired = true, demoAccountName = "demo" },
            demoPassword = "very-secret-1",
        });

        var (invalid, invalidBody) = await org.Owner.Client.PutAsync<int>("apps/answers", new { organizationId = org.OrganizationId, appId = source.AppId, key = SavedAnswerKind.ReviewNotes, answers = new { note = "typo" } });
        var (_, read) = await org.Owner.Client.GetAsync<SavedAnswerSetDto>($"apps/answers?organizationId={org.OrganizationId}&appId={source.AppId}");
        var raw = await org.Owner.Client.GetStringAsync($"{TestApi.Base}/apps/answers?organizationId={org.OrganizationId}&appId={source.AppId}", TestContext.Current.CancellationToken);
        var (_, copied) = await org.Owner.Client.PostAsync<int>("apps/answers/copy", new { organizationId = org.OrganizationId, sourceAppId = source.AppId, targetAppId = target.AppId });
        var (_, targetRead) = await org.Owner.Client.GetAsync<SavedAnswerSetDto>($"apps/answers?organizationId={org.OrganizationId}&appId={target.AppId}");
        var (foreign, _) = await other.Owner.Client.GetAsync<SavedAnswerSetDto>($"apps/answers?organizationId={other.OrganizationId}&appId={source.AppId}");
        var row = await _factory.DbAsync(db => db.SavedAnswers.AsNoTracking().SingleAsync(a => a.AppId == source.AppId && a.Kind == SavedAnswerKind.ReviewLogin));

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidBody.Messages.Should().Contain(m => m.Key == "AnswersInvalid");
        read.Data.DemoPasswordSet.Should().BeTrue();
        read.Data.Sections.Single().Answers.GetProperty("contactFirstName").GetString().Should().Be("Ada");
        raw.Should().NotContain("very-secret-1");
        row.Answers.Should().NotContain("very-secret-1");
        row.EncryptedSecret.Should().NotBeNullOrEmpty();
        copied.Data.Should().Be(1);
        targetRead.Data.Sections.Should().ContainSingle(s => s.Key == SavedAnswerKind.ReviewLogin);
        targetRead.Data.DemoPasswordSet.Should().BeFalse("the demo password is never copied");
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssistedSteps_ShouldBeCreatedHandedOffWithoutPasswordsAndCompleted()
    {
        var org = await NewOrgAsync();
        var (record, _) = await NewAppAsync(org, $"a{org.Suffix}", null, new Storeasy.Domain.JsonModels.AppProfile { Regions = ["TR"], DeveloperRegion = "TR", Features = [] });

        await org.Owner.Client.PutAsync<int>("apps/answers", new
        {
            organizationId = org.OrganizationId,
            appId = record.AppId,
            key = SavedAnswerKind.PrivacyLabels,
            answers = new { collectsData = true, privacyPolicyUrl = "https://example.test/privacy", dataTypes = new[] { new { category = "Contact Info", type = "Email Address", purposes = new[] { "App Functionality" }, linkedToUser = true, usedForTracking = false } } },
        });

        var (_, steps) = await org.Owner.Client.PatchAsync<List<AssistedStepListDto>>("assisted-steps", new { organizationId = org.OrganizationId, appId = record.AppId });
        var (_, again) = await org.Owner.Client.PatchAsync<List<AssistedStepListDto>>("assisted-steps", new { organizationId = org.OrganizationId, appId = record.AppId });
        var privacy = steps.Data.Single(s => s.Key == "apple.privacy.app-privacy");
        var (_, detail) = await org.Owner.Client.GetAsync<AssistedStepDetailDto>($"assisted-steps/step?organizationId={org.OrganizationId}&stepId={privacy.Id}");
        var (_, handOff) = await org.Owner.Client.PostAsync<AssistedStepHandOffDto>("assisted-steps/step/handoff", new { organizationId = org.OrganizationId, stepId = privacy.Id });
        var (completeHttp, _) = await org.Owner.Client.PutAsync<int>("assisted-steps/step/complete", new { organizationId = org.OrganizationId, stepId = privacy.Id, status = AssistedStepStatus.Done, note = "Published." });
        var (_, after) = await org.Owner.Client.PatchAsync<List<AssistedStepListDto>>("assisted-steps", new { organizationId = org.OrganizationId, appId = record.AppId, status = AssistedStepStatus.Done });
        var (_, timeline) = await org.Owner.Client.GetAsync<List<ComplianceItemDto>>($"apps/compliance?organizationId={org.OrganizationId}&appId={record.AppId}");

        steps.Data.Should().Contain(s => s.Key == "apple.age-rating.shared-location-dating" && s.DueDate == new DateOnly(2027, 1, 12));
        steps.Data.Should().NotContain(s => s.Key.StartsWith("apple.compliance.china", StringComparison.Ordinal), "the app is not distributed in China");
        again.Data.Select(s => s.Id).Should().BeEquivalentTo(steps.Data.Select(s => s.Id), "steps are created once");
        detail.Data.Steps.Should().HaveCount(4);
        detail.Data.StoreUrl.Should().Contain(record.ExternalId);
        detail.Data.Answers.Should().ContainKey("PrivacyLabels");
        handOff.Data.Prompt.Should().Contain(detail.Data.StoreUrl);
        handOff.Data.McpTool.Should().Be("assisted_step_complete");
        completeHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        after.Data.Should().ContainSingle(s => s.Id == privacy.Id && s.LastDoneByUserName == org.Owner.UserName);
        timeline.Data.Should().Contain(i => i.RuleId == "apple.age-rating.shared-location-dating" && i.Status == ComplianceStatus.Missing);
    }

    [Fact]
    public async Task Rules_ShouldExposeTheCatalogWithAffectedApps()
    {
        var org = await NewOrgAsync();
        var (record, _) = await NewAppAsync(org, $"a{org.Suffix}", null, new Storeasy.Domain.JsonModels.AppProfile { DeviceFamilies = ["iphone", "ipad"] });

        var (_, catalog) = await org.Owner.Client.GetAsync<RuleCatalogInfoDto>($"rules/catalog?organizationId={org.OrganizationId}");
        var (_, rules) = await org.Owner.Client.PatchAsync<List<RuleListDto>>("rules", new { organizationId = org.OrganizationId, store = StoreKind.Apple, category = "screenshots" });
        var (_, detail) = await org.Owner.Client.GetAsync<RuleDetailDto>($"rules/rule?organizationId={org.OrganizationId}&ruleId=apple.screenshots.ipad-13");
        var (missing, _) = await org.Owner.Client.GetAsync<RuleDetailDto>($"rules/rule?organizationId={org.OrganizationId}&ruleId=apple.nothing.here");
        var (_, calendar) = await org.Owner.Client.GetAsync<List<ComplianceItemDto>>($"compliance?organizationId={org.OrganizationId}&from=2026-11-01&to=2027-12-31");

        catalog.Data.Version.Should().Be("1.1.0");
        catalog.Data.RuleCount.Should().BeGreaterThan(100);
        rules.Data.Should().OnlyContain(r => r.Store == StoreKind.Apple && r.Category == "screenshots");
        rules.Data.Single(r => r.Id == "apple.screenshots.ipad-13").AffectedAppCount.Should().Be(1);
        detail.Data.AffectedApps.Should().ContainSingle(a => a.AppId == record.AppId && a.Applicability == RuleApplicability.Applies);
        detail.Data.Sources.Should().NotBeEmpty();
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        calendar.Data.Should().Contain(i => i.AppId == record.AppId && i.RuleId == "apple.age-rating.shared-location-dating");
        calendar.Data.Should().BeInAscendingOrder(i => i.DueDate);
    }
}
