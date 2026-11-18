using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Subscriptions, In-App Purchases and purchase settings against the fake App Store Connect.")]
public class AppleMonetizationTests(StoreasyApiFactory factory) : MonetizationTestBase(factory)
{
    private FakeAppStoreConnect Apple => _factory.Stores.Apple;

    private async Task<(Org Org, Record Record, string AppleId)> SetupAsync()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";

        Apple.AddApp(appleId);

        var (record, _) = await NewAppAsync(org, appleId);

        return (org, record, appleId);
    }

    [Fact]
    public async Task Sync_ShouldStoreGroupsSubscriptionsAndPrices_AndReadsShouldStayInTheOrganization()
    {
        // Arrange
        var (org, record, appleId) = await SetupAsync();
        var other = await NewOrgAsync();
        var productId = $"com.{org.Suffix}.plus.monthly";

        Apple.AddSubscription(appleId, "Plus", productId, "APPROVED", tier: 5, screenshot: true);

        // Act
        var job = await SyncAsync(org.Owner, org.OrganizationId, record.StoreAppId);
        var (_, groups) = await org.Owner.Client.GetAsync<SubscriptionGroupsDto>($"subscriptions/groups?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}");
        var id = groups.Data.Groups.Single().Subscriptions.Single().Id;
        var (_, detail) = await org.Owner.Client.GetAsync<ProductDetailDto>($"subscriptions/subscription?organizationId={org.OrganizationId}&subscriptionId={id}");
        var (_, prices) = await org.Owner.Client.PatchListAsync<ProductPriceDto>("subscriptions/prices", new { organizationId = org.OrganizationId, subscriptionId = id, pageNumber = 1, rowCount = 10 });
        var (_, points) = await org.Owner.Client.GetAsync<List<PricePointDto>>($"subscriptions/price-points?organizationId={org.OrganizationId}&subscriptionId={id}&territory=TUR");
        var (otherOwnOrg, _) = await other.Owner.Client.GetAsync<ProductDetailDto>($"subscriptions/subscription?organizationId={other.OrganizationId}&subscriptionId={id}");
        var (otherForeignOrg, _) = await other.Owner.Client.GetAsync<ProductDetailDto>($"subscriptions/subscription?organizationId={org.OrganizationId}&subscriptionId={id}");
        var (otherPlan, _, _) = await PlanAsync(other.Owner, "subscriptions/plan", other.OrganizationId, record.StoreAppId, new { kind = MonetizationChangeKind.Level, productId, level = 2 });

        // Assert
        job.Status.Should().Be(StoreJobStatus.Succeeded);
        groups.Data.Store.Should().Be(StoreKind.Apple);
        groups.Data.Groups.Single().ReferenceName.Should().Be("Plus");
        groups.Data.Groups.Single().Localizations.Should().ContainSingle(l => l.Locale == "en-US");
        groups.Data.Groups.Single().Subscriptions.Single().BasePrice.Should().BeEquivalentTo(new BasePriceDto { Territory = "USA", Currency = "USD", Amount = "4.99", Source = PriceSource.Store });
        detail.Data.ProductId.Should().Be(productId);
        detail.Data.State.Should().Be(ProductState.Approved);
        detail.Data.SubmitWithVersion.Should().BeFalse();
        detail.Data.ReviewScreenshot.Should().NotBeNull();
        detail.Data.Localizations.Should().ContainSingle(l => l.Name == "Plus");
        prices.TotalDataCount.Should().Be(5);
        prices.Data.Should().Contain(p => p.Territory == "TUR" && p.CustomerPrice == "49.99" && p.Currency == "TRY");
        points.Data.Should().HaveCount(10).And.Contain(p => p.Id == "spp-TUR-3" && p.CustomerPrice == "29.99");
        otherOwnOrg.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherForeignOrg.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherPlan.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePlan_ShouldWriteGroupSubscriptionVersionLocalizationAndScreenshot_ThenPricesOffersAndAvailability()
    {
        // Arrange
        var (org, record, appleId) = await SetupAsync();
        var editor = await MemberAsync(org, OrgRole.Editor);
        var productId = $"com.{org.Suffix}.pro.monthly";
        var png = Png(1290, 2796);
        var assetId = await AssetAsync(org, png, "review.png", "image/png");
        var start = DateTime.UtcNow;

        // Act: the first plan creates the group, the subscription, its localization and its review screenshot.
        var (status, plan, message) = await PlanAsync(editor, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                                       new { kind = MonetizationChangeKind.CreateGroup, groupReferenceName = "Pro", localizations = new[] { new { locale = "en-US", name = "Pro" } } },
                                                       new { kind = MonetizationChangeKind.CreateSubscription, productId, referenceName = "Pro Monthly", duration = "ONE_MONTH", groupReferenceName = "Pro", level = 1 },
                                                       new { kind = MonetizationChangeKind.ProductLocalization, productId, locale = "en-US", name = "Pro Monthly", description = "All features" },
                                                       new { kind = MonetizationChangeKind.ReviewScreenshot, productId, assetId });

        var (applyHttp, applied) = await editor.Client.PostAsync<ChangePlanDto>("subscriptions/apply", new { organizationId = org.OrganizationId, planId = plan?.PlanId });
        var job = await WaitForJobAsync(applied.Data.JobId.Value);
        await WaitForSyncAsync(org.OrganizationId, start);

        var subscription = Apple.Of("subscriptions").Single(s => (string)s.Attributes["productId"] == productId);
        var version = Apple.Of("subscriptionVersions", "subscription", subscription.Id).Single();
        var screenshot = Apple.Of("subscriptionAppStoreReviewScreenshots", "subscription", subscription.Id).Single();
        var (_, groups) = await org.Owner.Client.GetAsync<SubscriptionGroupsDto>($"subscriptions/groups?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}");
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.SubscriptionChanged).CountAsync());

        // Act: the second plan prices it from Turkey (equalized), adds a free week in two territories and sets availability.
        start = DateTime.UtcNow;

        var (_, pricePlan, priceMessage) = await PlanAsync(editor, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                                          new { kind = MonetizationChangeKind.BasePrice, productId, territory = "TUR", pricePointId = "spp-TUR-3", equalize = true },
                                                          new { kind = MonetizationChangeKind.IntroductoryOffer, productId, mode = OfferMode.FreeTrial, duration = "ONE_WEEK", periods = 1, territories = new[] { "TUR", "USA" } },
                                                          new { kind = MonetizationChangeKind.Availability, productId, territories = new[] { "TUR", "USA", "DEU" }, availableInNewTerritories = true });

        var pricedPlan = await ApproveAndWaitAsync(editor, org.OrganizationId, pricePlan.PlanId);
        await WaitForSyncAsync(org.OrganizationId, start);

        var (_, prices) = await org.Owner.Client.PatchListAsync<ProductPriceDto>("subscriptions/prices", new { organizationId = org.OrganizationId, subscriptionId = groups.Data.Groups.Single().Subscriptions.Single().Id, pageNumber = 1, rowCount = 20 });
        var (_, offers) = await org.Owner.Client.GetAsync<SubscriptionOffersDto>($"subscriptions/offers?organizationId={org.OrganizationId}&subscriptionId={groups.Data.Groups.Single().Subscriptions.Single().Id}");

        // Assert
        status.Should().Be(HttpStatusCode.OK, message);
        plan.Kind.Should().Be(ChangePlanKind.Monetization);
        plan.RequiredPermission.Should().Be(OrgPermission.EditContent);
        plan.ConfirmText.Should().BeNull();
        plan.Items.Should().Contain(i => i.Field == $"{productId}/productId" && i.Action == ChangeAction.Create);
        plan.Calls.Should().Contain(c => c.Path == "/v1/subscriptions");
        plan.Files.Should().ContainSingle(f => f.AssetId == assetId);
        applyHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        Apple.Of("subscriptionGroups", "app", appleId).Should().ContainSingle(g => (string)g.Attributes["referenceName"] == "Pro");
        version.Attributes["state"].GetValue<string>().Should().Be("PREPARE_FOR_SUBMISSION");
        Apple.Of("subscriptionLocalizations", "version", version.Id).Should().ContainSingle(l => (string)l.Attributes["name"] == "Pro Monthly");
        screenshot.Attributes["sourceFileChecksum"].GetValue<string>().Should().Be(FakeAppStoreConnect.Md5(png));
        groups.Data.Groups.Single().Subscriptions.Single().HasReviewScreenshot.Should().BeTrue();
        audit.Should().BeGreaterThan(0);

        pricePlan.Should().NotBeNull(priceMessage);
        pricePlan.Items.Count(i => i.Group == "ProductPrice").Should().Be(5);
        pricedPlan.Status.Should().Be(ChangePlanStatus.Applied);
        Apple.Of("subscriptionPrices", "subscription", subscription.Id).Select(p => p.Rels["subscriptionPricePoint"]).Should().BeEquivalentTo(FakeAppStoreConnect.MoneyTerritories.Keys.Select(t => $"spp-{t}-3"));
        Apple.Of("subscriptionIntroductoryOffers", "subscription", subscription.Id).Select(o => o.Rels["territory"]).Should().BeEquivalentTo(["TUR", "USA"]);
        prices.TotalDataCount.Should().Be(5);
        offers.Data.Introductory.Should().ContainSingle(o => o.Mode == OfferMode.FreeTrial && o.Territories.Count == 2);
    }

    [Fact]
    public async Task PriceChangeOnSale_ShouldNeedReleaseProductionAndTypedConfirmation_ForExistingSubscribers()
    {
        // Arrange
        var (org, record, appleId) = await SetupAsync();
        var editor = await MemberAsync(org, OrgRole.Editor);
        var productId = $"com.{org.Suffix}.plus.yearly";
        var subscriptionId = Apple.AddSubscription(appleId, "Plus", productId, "APPROVED", tier: 5);

        // Act
        var (noChoice, _, _) = await PlanAsync(editor, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                                            new { kind = MonetizationChangeKind.BasePrice, productId, territory = "TUR", pricePointId = "spp-TUR-7" });
        var (status, plan, _) = await PlanAsync(editor, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                                new { kind = MonetizationChangeKind.BasePrice, productId, territory = "TUR", pricePointId = "spp-TUR-7", preserveCurrentPrice = false });
        var (editorApply, _) = await editor.Client.PostAsync<ChangePlanDto>("subscriptions/apply", new { organizationId = org.OrganizationId, planId = plan.PlanId });
        var (_, unconfirmed) = await org.Owner.Client.PostAsync<ChangePlanDto>("subscriptions/apply", new { organizationId = org.OrganizationId, planId = plan.PlanId, confirm = "wrong" });
        var applied = await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.PlanId, plan.ConfirmText);
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.PriceScheduled).CountAsync());

        // Assert
        noChoice.Should().Be(HttpStatusCode.BadRequest);
        status.Should().Be(HttpStatusCode.OK);
        plan.RequiredPermission.Should().Be(OrgPermission.ReleaseProduction);
        plan.ConfirmText.Should().Be($"Pusula {productId}");
        plan.Warnings.Select(w => w.Key).Should().Contain(["Monetization.ExistingSubscribersMigrated", "Monetization.PriceIncreaseConsent"]);
        editorApply.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        unconfirmed.IsSuccess.Should().BeFalse();
        applied.Status.Should().Be(ChangePlanStatus.Applied);
        Apple.Writes.Where(w => w.Path == "/v1/subscriptionPrices" && (string)w.Body["data"]["relationships"]["subscription"]["data"]["id"] == subscriptionId)
                    .Should().HaveCount(5)
                    .And.OnlyContain(w => w.Body["data"]["attributes"]["preserveCurrentPrice"].GetValue<bool>() == false);
        audit.Should().Be(5);
    }

    [Fact]
    public async Task Approval_ShouldBeRefused_WhenTheStoreChangedSinceThePlan()
    {
        // Arrange
        var (org, record, appleId) = await SetupAsync();
        var productId = $"com.{org.Suffix}.plus.weekly";
        var subscriptionId = Apple.AddSubscription(appleId, "Plus", productId);
        var (_, plan, _) = await PlanAsync(org.Owner, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                           new { kind = MonetizationChangeKind.UpdateProduct, productId, referenceName = "Plus Weekly" });

        Apple.Of("subscriptions").Single(s => s.Id == subscriptionId).Attributes["name"] = "Changed in App Store Connect";

        // Act
        var (http, approved) = await org.Owner.Client.PostAsync<ChangePlanDto>("subscriptions/apply", new { organizationId = org.OrganizationId, planId = plan.PlanId });

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        approved.Messages.Should().Contain(m => m.Key == "Plan.Conflict");
    }

    [Fact]
    public async Task InvalidChanges_ShouldNotMakeAPlan()
    {
        // Arrange
        var (org, record, appleId) = await SetupAsync();
        var productId = $"com.{org.Suffix}.plus.monthly";

        Apple.AddSubscription(appleId, "Plus", productId);

        // Act
        var (badId, _, _) = await PlanAsync(org.Owner, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                            new { kind = MonetizationChangeKind.CreateSubscription, productId = "bad id!", referenceName = "x", duration = "ONE_MONTH", groupReferenceName = "Plus" });
        var (taken, _, _) = await PlanAsync(org.Owner, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                            new { kind = MonetizationChangeKind.CreateSubscription, productId, referenceName = "x", duration = "ONE_MONTH", groupReferenceName = "Plus" });
        var (offer, _, _) = await PlanAsync(org.Owner, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                            new { kind = MonetizationChangeKind.IntroductoryOffer, productId, mode = OfferMode.PayAsYouGo, duration = "ONE_WEEK", periods = 3, prices = new[] { new { territory = "TUR", pricePointId = "spp-TUR-1" } } });
        var (playChange, _, _) = await PlanAsync(org.Owner, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                                 new { kind = MonetizationChangeKind.BasePlan, productId, basePlanId = "monthly" });
        var (viewer, _, _) = await PlanAsync(await MemberAsync(org, OrgRole.Viewer), "subscriptions/plan", org.OrganizationId, record.StoreAppId,
                                             new { kind = MonetizationChangeKind.Level, productId, level = 2 });

        // Assert
        badId.Should().Be(HttpStatusCode.BadRequest);
        taken.Should().Be(HttpStatusCode.Conflict);
        offer.Should().Be(HttpStatusCode.BadRequest);
        playChange.Should().Be(HttpStatusCode.BadRequest);
        viewer.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InAppPurchase_ShouldBeCreatedLocalizedAndPricedFromABaseTerritory()
    {
        // Arrange
        var (org, record, _) = await SetupAsync();
        var productId = $"com.{org.Suffix}.theme.night";
        var start = DateTime.UtcNow;

        // Act
        var (status, plan, message) = await PlanAsync(org.Owner, "iap/plan", org.OrganizationId, record.StoreAppId,
                                                      new { kind = MonetizationChangeKind.CreateIap, productId, productKind = StoreProductKind.NonConsumable, referenceName = "Night theme" },
                                                      new { kind = MonetizationChangeKind.ProductLocalization, productId, locale = "en-US", name = "Night theme", description = "Dark colors." });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.PlanId);
        await WaitForSyncAsync(org.OrganizationId, start);

        start = DateTime.UtcNow;

        var (_, pricePlan, priceMessage) = await PlanAsync(org.Owner, "iap/plan", org.OrganizationId, record.StoreAppId,
                                                          new { kind = MonetizationChangeKind.BasePrice, productId, territory = "TUR", pricePointId = "ipp-TUR-2" });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, pricePlan.PlanId);
        await WaitForSyncAsync(org.OrganizationId, start);

        var (_, list) = await org.Owner.Client.PatchListAsync<IapListDto>("iap", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, pageNumber = 1, rowCount = 20 });
        var iap = Apple.Of("inAppPurchases").Single(i => (string)i.Attributes["productId"] == productId);
        var schedule = Apple.Of("inAppPurchasePriceSchedules", "inAppPurchase", iap.Id).Single();
        var (_, detail) = await org.Owner.Client.GetAsync<ProductDetailDto>($"iap/product?organizationId={org.OrganizationId}&iapId={list.Data.Single().Id}");
        var (_, prices) = await org.Owner.Client.PatchListAsync<ProductPriceDto>("iap/prices", new { organizationId = org.OrganizationId, iapId = list.Data.Single().Id, source = PriceSource.Store, pageNumber = 1, rowCount = 20 });

        // Assert
        status.Should().Be(HttpStatusCode.OK, message);
        plan.Items.Should().Contain(i => i.Group == "InAppPurchase");
        pricePlan.Should().NotBeNull(priceMessage);
        iap.Attributes["inAppPurchaseType"].GetValue<string>().Should().Be("NON_CONSUMABLE");
        Apple.Of("inAppPurchaseVersions", "inAppPurchase", iap.Id).Should().ContainSingle();
        schedule.Rels["baseTerritory"].Should().Be("TUR");
        list.Data.Should().ContainSingle(p => p.ProductId == productId && p.Kind == StoreProductKind.NonConsumable && p.BasePrice.Amount == "19.99" && p.BasePrice.Currency == "TRY");
        detail.Data.SubmitWithVersion.Should().BeTrue();
        detail.Data.Localizations.Should().ContainSingle(l => l.Name == "Night theme");
        prices.TotalDataCount.Should().Be(5);
        (await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().CountAsync(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.IapChanged))).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ServerNotificationsAndSandboxTesters_ShouldBePlannedWithTheirPermissionsAndWritten()
    {
        // Arrange
        var (org, record, appleId) = await SetupAsync();
        var editor = await MemberAsync(org, OrgRole.Editor);
        var testerId = $"st{org.Suffix}";

        Apple.AddSandboxTester(testerId, $"tester{org.Suffix}@example.test");

        // Act
        var (editorHttp, _) = await editor.Client.PostAsync<ChangePlanDto>("purchasesettings/server-notifications/plan", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, url = "https://example.test/asn" });
        var (insecureHttp, _) = await org.Owner.Client.PostAsync<ChangePlanDto>("purchasesettings/server-notifications/plan", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, url = "http://example.test/asn" });
        var (_, plan) = await org.Owner.Client.PostAsync<ChangePlanDto>("purchasesettings/server-notifications/plan", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, url = "https://example.test/asn", sandboxUrl = "https://example.test/asn-sandbox" });
        var (wrongEndpoint, _) = await org.Owner.Client.PostAsync<ChangePlanDto>("subscriptions/apply", new { organizationId = org.OrganizationId, planId = plan.Data.PlanId });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.Data.PlanId);
        var (_, notifications) = await editor.Client.GetAsync<ServerNotificationsDto>($"purchasesettings/server-notifications?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}");

        var (_, testers) = await editor.Client.GetAsync<List<SandboxTesterDto>>($"purchasesettings/sandbox-testers?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}");
        var (_, testerPlan) = await editor.Client.PostAsync<ChangePlanDto>("purchasesettings/sandbox-testers/plan", new
        {
            organizationId = org.OrganizationId,
            storeAppId = record.StoreAppId,
            changes = new[] { new { kind = MonetizationChangeKind.SandboxTester, testerId, territory = "TUR", clearPurchaseHistory = true } },
        });
        await ApproveAndWaitAsync(editor, org.OrganizationId, testerPlan.Data.PlanId);

        // Assert
        editorHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        insecureHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        plan.Data.Kind.Should().Be(ChangePlanKind.ServerNotifications);
        plan.Data.RequiredPermission.Should().Be(OrgPermission.ManageWebhooks);
        wrongEndpoint.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Apple.Apps[appleId]["subscriptionStatusUrl"].GetValue<string>().Should().Be("https://example.test/asn");
        Apple.Apps[appleId]["subscriptionStatusUrlVersionForSandbox"].GetValue<string>().Should().Be("V2");
        notifications.Data.Url.Should().Be("https://example.test/asn");
        notifications.Data.SandboxUrl.Should().Be("https://example.test/asn-sandbox");
        notifications.Data.Assisted.Should().BeFalse();
        testers.Data.Should().Contain(t => t.Id == testerId);
        testerPlan.Data.Kind.Should().Be(ChangePlanKind.SandboxTesters);
        Apple.Of("sandboxTesters").Single(t => t.Id == testerId).Attributes["territory"].GetValue<string>().Should().Be("TUR");
        Apple.Writes.Should().Contain(w => w.Path == "/v2/sandboxTestersClearPurchaseHistoryRequest" && w.Body.ToJsonString().Contains(testerId));
        (await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().CountAsync(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.PurchaseSettingsChanged))).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Readiness_ShouldAskForReviewScreenshotsAndSubmittingTheFirstSubscriptionWithTheVersion()
    {
        // Arrange
        var (org, record, appleId) = await SetupAsync();

        Apple.AddSubscription(appleId, "Plus", $"com.{org.Suffix}.plus.monthly", "READY_TO_SUBMIT");

        // Act
        var (http, report) = await org.Owner.Client.GetAsync<ReadinessReportDto>($"apps/readiness?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Apple}");

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        report.Data.Items.Should().ContainSingle(i => i.Key == "monetization.review-screenshot" && i.Status == ReadinessStatus.Missing && i.Message.Contains("plus.monthly"));
        report.Data.Items.Should().ContainSingle(i => i.Key == "monetization.localizations" && i.Status == ReadinessStatus.Ok);
        report.Data.Items.Should().ContainSingle(i => i.Key == "monetization.first-with-version" && i.Status == ReadinessStatus.Warning);
        report.Data.CanSubmit.Should().BeFalse();
    }
}
