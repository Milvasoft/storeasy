using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Play subscriptions, base plans, offers, price conversion and one-time products against the fake Play API.")]
public class GoogleMonetizationTests(StoreasyApiFactory factory) : MonetizationTestBase(factory)
{
    private FakeGooglePlay Play => _factory.Stores.Google;

    private async Task<(Org Org, Record Record, string Package)> SetupAsync()
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.halka";

        Play.AddApp(package);

        var (_, record) = await NewAppAsync(org, null, package);

        return (org, record, package);
    }

    [Fact]
    public async Task Subscription_ShouldBeCreatedWithABasePlanAndAnOffer_FromConvertedPrices()
    {
        // Arrange
        var (org, record, package) = await SetupAsync();
        var start = DateTime.UtcNow;

        // Act
        var (_, converted) = await org.Owner.Client.PostAsync<ConvertedPricesDto>("subscriptions/prices/convert", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, currency = "TRY", amount = "59.99" });
        var us = converted.Data.Prices.Single(p => p.RegionCode == "US");

        var (status, plan, message) = await PlanAsync(org.Owner, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
            new { kind = MonetizationChangeKind.PlaySubscription, productId = "premium", localizations = new[] { new { locale = "en-US", name = "Premium", benefits = new[] { "Everything" } } } },
            new
            {
                kind = MonetizationChangeKind.BasePlan,
                productId = "premium",
                basePlanId = "monthly",
                autoRenewing = true,
                duration = "P1M",
                regionsVersion = converted.Data.RegionsVersion,
                prices = new[] { new { territory = "TR", currency = "TRY", amount = "59.99" }, new { territory = "US", currency = us.Currency, amount = us.Amount } },
            },
            new { kind = MonetizationChangeKind.BasePlanState, productId = "premium", basePlanId = "monthly", active = true },
            new { kind = MonetizationChangeKind.PlayOffer, productId = "premium", basePlanId = "monthly", code = "free-week", phases = new[] { new { duration = "P1W", recurrence = 1, free = true } } });

        status.Should().Be(HttpStatusCode.OK, message);

        var applied = await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.PlanId);
        await WaitForSyncAsync(org.OrganizationId, start);

        var (_, groups) = await org.Owner.Client.GetAsync<SubscriptionGroupsDto>($"subscriptions/groups?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}");
        var subscription = Play.Subscriptions[$"{package}/premium"];

        // Assert
        converted.Data.RegionsVersion.Should().Be("2025/03");
        us.Amount.Should().Be("119.98");
        status.Should().Be(HttpStatusCode.OK, message);
        plan.RequiredPermission.Should().Be(OrgPermission.EditContent);
        applied.Status.Should().Be(ChangePlanStatus.Applied);
        subscription["basePlans"][0]["state"].GetValue<string>().Should().Be("ACTIVE");
        subscription["basePlans"][0]["regionalConfigs"].AsArray().Should().HaveCount(2);
        Play.Offers.Should().ContainKey($"{package}/premium/monthly/free-week");
        Play.MoneyWrites.Should().Contain(w => w.Path.Contains("regionsVersion.version=2025%2F03") || w.Path.Contains("regionsVersion.version=2025/03"));
        groups.Data.Store.Should().Be(StoreKind.Google);
        groups.Data.PlaySubscriptions.Should().ContainSingle(s => s.ProductId == "premium" && s.State == ProductState.Active);
        groups.Data.PlaySubscriptions.Single().BasePlans.Single().Offers.Should().ContainSingle(o => o.Code == "free-week");
    }

    [Fact]
    public async Task ActiveBasePlanPrices_ShouldNeedReleaseProductionAndMigrateExistingSubscribersOnlyWhenAsked()
    {
        // Arrange
        var (org, record, package) = await SetupAsync();
        var editor = await MemberAsync(org, OrgRole.Editor);

        Play.AddSubscription(package, "plus");

        // Act
        var (invalid, _, _) = await PlanAsync(editor, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
            new { kind = MonetizationChangeKind.BasePlanPrices, productId = "plus", basePlanId = "monthly", preserveCurrentPrice = true, prices = new[] { new { territory = "JP", currency = "JPY", amount = "299.5" } } });
        var (status, plan, _) = await PlanAsync(editor, "subscriptions/plan", org.OrganizationId, record.StoreAppId,
            new { kind = MonetizationChangeKind.BasePlanPrices, productId = "plus", basePlanId = "monthly", preserveCurrentPrice = false, prices = new[] { new { territory = "TR", currency = "TRY", amount = "69.99" } } });
        var (editorApply, _) = await editor.Client.PostAsync<ChangePlanDto>("subscriptions/apply", new { organizationId = org.OrganizationId, planId = plan.PlanId });
        var applied = await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.PlanId, plan.ConfirmText);

        // Assert
        invalid.Should().Be(HttpStatusCode.BadRequest);
        status.Should().Be(HttpStatusCode.OK);
        plan.RequiredPermission.Should().Be(OrgPermission.ReleaseProduction);
        plan.ConfirmText.Should().Be("Pusula plus:monthly");
        plan.Items.Should().ContainSingle(i => i.Locale == "TR" && i.Before == "59.99 TRY" && i.After == "69.99 TRY");
        plan.Warnings.Select(w => w.Key).Should().Contain("Monetization.PriceIncreaseConsent");
        editorApply.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        applied.Status.Should().Be(ChangePlanStatus.Applied);
        Play.Subscriptions[$"{package}/plus"]["basePlans"][0]["regionalConfigs"].AsArray().Single(c => (string)c["regionCode"] == "TR")["price"]["units"].GetValue<string>().Should().Be("69");
        Play.MoneyWrites.Should().Contain(w => w.Path.Contains("/basePlans/monthly:migratePrices") && w.Path.Contains(package));
    }

    [Fact]
    public async Task OneTimeProduct_ShouldBeCreatedActivatedAndListed_AndNotificationsShouldBeAssisted()
    {
        // Arrange
        var (org, record, package) = await SetupAsync();
        var start = DateTime.UtcNow;

        Play.AddSubscription(package, "plus");

        // Act
        var (status, plan, message) = await PlanAsync(org.Owner, "iap/plan", org.OrganizationId, record.StoreAppId,
            new
            {
                kind = MonetizationChangeKind.OneTimeProduct,
                productId = "theme_dark",
                localizations = new[] { new { locale = "en-US", name = "Dark theme", description = "Dark colors." } },
                prices = new[] { new { territory = "TR", currency = "TRY", amount = "34.99" } },
                active = true,
            });

        status.Should().Be(HttpStatusCode.OK, message);

        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.PlanId);
        await WaitForSyncAsync(org.OrganizationId, start);

        var (_, list) = await org.Owner.Client.PatchListAsync<IapListDto>("iap", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, pageNumber = 1, rowCount = 20 });
        var (_, notifications) = await org.Owner.Client.GetAsync<ServerNotificationsDto>($"purchasesettings/server-notifications?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}");
        var (_, readiness) = await org.Owner.Client.GetAsync<ReadinessReportDto>($"apps/readiness?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Google}");
        var (pricePoints, _) = await org.Owner.Client.GetAsync<List<PricePointDto>>($"iap/price-points?organizationId={org.OrganizationId}&iapId={list.Data.Single().Id}&territory=TR");

        // Assert
        status.Should().Be(HttpStatusCode.OK, message);
        Play.OneTimeProducts[$"{package}/theme_dark"]["purchaseOptions"][0]["state"].GetValue<string>().Should().Be("ACTIVE");
        list.Data.Should().ContainSingle(p => p.ProductId == "theme_dark" && p.Kind == StoreProductKind.OneTimeProduct && p.State == ProductState.Active && p.BasePrice.Amount == "34.99");
        list.Data.Single().PurchaseOptions.Should().ContainSingle(o => o.BasePlanId == "buy" && o.State == ProductState.Active);
        notifications.Data.Assisted.Should().BeTrue();
        notifications.Data.AssistedStepKey.Should().Be("google.rtdn");
        readiness.Data.Items.Should().ContainSingle(i => i.Key == "monetization.base-plans" && i.Status == ReadinessStatus.Ok);
        readiness.Data.Items.Should().ContainSingle(i => i.Key == "assisted.google.rtdn" && i.Status == ReadinessStatus.Assisted);
        pricePoints.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().CountAsync(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.IapChanged))).Should().BeGreaterThan(0);
    }
}
