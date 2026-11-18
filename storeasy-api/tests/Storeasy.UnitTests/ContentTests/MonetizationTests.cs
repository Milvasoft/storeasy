using FluentAssertions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Monetization;
using Storeasy.UnitTests.TestSupport;

namespace Storeasy.UnitTests.ContentTests;

[Trait("Content Unit Tests", "Monetization rules, planner and content keys.")]
public class MonetizationTests
{
    private static readonly StoreAppContext _apple = new(1, 1, 1, "Kumbara", StoreKind.Apple, "123", 1, null);
    private static readonly StoreAppContext _google = new(1, 2, 1, "Kumbara", StoreKind.Google, "com.kumbara", 2, null);
    private static readonly DateOnly _today = new(2026, 11, 18);

    [Theory]
    [InlineData("ONE_MONTH", OfferMode.FreeTrial, "ONE_WEEK", 1, true)]
    [InlineData("ONE_MONTH", OfferMode.FreeTrial, "ONE_WEEK", 2, false)]
    [InlineData("ONE_MONTH", OfferMode.PayAsYouGo, "ONE_MONTH", 12, true)]
    [InlineData("ONE_MONTH", OfferMode.PayAsYouGo, "ONE_MONTH", 13, false)]
    [InlineData("ONE_MONTH", OfferMode.PayAsYouGo, "ONE_WEEK", 3, false)]
    [InlineData("ONE_YEAR", OfferMode.PayAsYouGo, "ONE_YEAR", 2, false)]
    [InlineData("ONE_YEAR", OfferMode.PayUpFront, "SIX_MONTHS", 1, true)]
    [InlineData("ONE_YEAR", OfferMode.PayUpFront, "ONE_WEEK", 1, false)]
    public void IsValidOffer_ShouldFollowTheAppStoreCombinations(string period, OfferMode mode, string duration, int periods, bool expected)
        => MonetizationRules.IsValidOffer(period, mode, duration, periods).Should().Be(expected);

    [Theory]
    [InlineData(StoreKind.Apple, "com.kumbara.plus_monthly", true)]
    [InlineData(StoreKind.Apple, "bad id", false)]
    [InlineData(StoreKind.Google, "plus_monthly", true)]
    [InlineData(StoreKind.Google, "Plus", false)]
    [InlineData(StoreKind.Google, "_plus", false)]
    public void IsValidProductId_ShouldFollowTheStoreFormat(StoreKind store, string productId, bool expected)
        => MonetizationRules.IsValidProductId(store, productId).Should().Be(expected);

    [Theory]
    [InlineData("TRY", "59.99", true)]
    [InlineData("TRY", "59.999", false)]
    [InlineData("JPY", "300", true)]
    [InlineData("JPY", "299.5", false)]
    [InlineData("USD", "0", false)]
    [InlineData("USD", "abc", false)]
    public void IsValidAmount_ShouldCheckDecimalsOfTheCurrency(string currency, string amount, bool expected)
        => MonetizationRules.IsValidAmount(currency, amount).Should().Be(expected);

    [Fact]
    public void States_ShouldMapStoreTextsAndDecideWhatIsOnSale()
    {
        MonetizationRules.AppleState("APPROVED").Should().Be(ProductState.Approved);
        MonetizationRules.AppleState("DEVELOPER_REMOVED_FROM_SALE").Should().Be(ProductState.Removed);
        MonetizationRules.GoogleState("INACTIVE_PUBLISHED").Should().Be(ProductState.Inactive);
        MonetizationRules.GoogleProductState([ProductState.Draft, ProductState.Active]).Should().Be(ProductState.Active);
        MonetizationRules.IsOnSale(ProductState.Active).Should().BeTrue();
        MonetizationRules.IsOnSale(ProductState.ReadyToSubmit).Should().BeFalse();
        MonetizationRules.PricePermission(true).Should().Be(OrgPermission.ReleaseProduction);
        MonetizationRules.Highest(OrgPermission.EditContent, OrgPermission.ReleaseProduction).Should().Be(OrgPermission.ReleaseProduction);
        MonetizationRules.AuditActionOf(MonetizationKeys.Price).Should().Be(AuditAction.PriceScheduled);
        MonetizationRules.AuditActionOf(MonetizationKeys.Iap).Should().Be(AuditAction.IapChanged);
    }

    [Fact]
    public void Planner_ShouldEqualizeABasePrice_KeepManualTerritories_AndAskConfirmationForExistingSubscribers()
    {
        // Arrange: an approved subscription; Germany was priced by hand.
        var snapshot = Snapshot(Subscription("com.kumbara.plus", ProductState.Approved,
                                             Price("TUR", "spp-TUR-5", "49.99", "TRY"),
                                             Price("USA", "spp-USA-5", "4.99", "USD"),
                                             Price("DEU", "spp-DEU-9", "8.99", "EUR", PriceSource.Manual)));

        var equalized = new Dictionary<string, IReadOnlyList<PricePointDto>>
        {
            ["spp-TUR-7"] =
            [
                new PricePointDto { Id = "spp-TUR-7", Territory = "TUR", Currency = "TRY", CustomerPrice = "69.99" },
                new PricePointDto { Id = "spp-USA-7", Territory = "USA", Currency = "USD", CustomerPrice = "6.99" },
                new PricePointDto { Id = "spp-DEU-7", Territory = "DEU", Currency = "EUR", CustomerPrice = "6.99" },
            ],
        };

        // Act
        var result = Plan(_apple, snapshot, equalized, new MonetizationChangeDto
        {
            Kind = MonetizationChangeKind.BasePrice,
            ProductId = "com.kumbara.plus",
            Territory = "TUR",
            PricePointId = "spp-TUR-7",
            PreserveCurrentPrice = false,
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Draft.RequiredPermission.Should().Be(OrgPermission.ReleaseProduction);
        result.Draft.ConfirmText.Should().Be("Kumbara com.kumbara.plus");
        result.Draft.Items.Select(i => (i.Locale, i.Before, i.After)).Should().BeEquivalentTo([("TUR", "49.99 TRY", "69.99 TRY"), ("USA", "4.99 USD", "6.99 USD")]);
        result.Draft.Warnings.Select(w => w.Key).Should().Contain([MessageKey.MonetizationManualPricesKept, MessageKey.MonetizationExistingSubscribersMigrated, MessageKey.MonetizationPriceIncreaseConsent]);
        ((MonetizationChangeSet)result.Draft.CustomChanges).Operations.Single().Prices.Select(p => p.PricePointId).Should().BeEquivalentTo(["spp-TUR-7", "spp-USA-7"]);
        result.Draft.BaseKeys.Should().Contain(MonetizationKeys.Of(MonetizationKeys.Price, "com.kumbara.plus", "TUR", "pricePoint"));
    }

    [Fact]
    public void Planner_ShouldRefuseAPriceChangeOnSale_WithoutAChoiceForExistingSubscribers()
    {
        var snapshot = Snapshot(Subscription("com.kumbara.plus", ProductState.Approved));

        var result = Plan(_apple, snapshot, null, new MonetizationChangeDto { Kind = MonetizationChangeKind.TerritoryPrice, ProductId = "com.kumbara.plus", Territory = "TUR", PricePointId = "spp-TUR-7" });

        result.ErrorKey.Should().Be(MessageKey.MonetizationPreserveChoiceRequired);
    }

    [Fact]
    public void Planner_ShouldLeaveOutTerritoriesThatAlreadyHaveAnIntroductoryOffer()
    {
        var product = Subscription("com.kumbara.plus", ProductState.ReadyToSubmit);
        product.Offers = [new ProductOffer { Kind = OfferKind.Introductory, Territories = ["IND"], Mode = OfferMode.FreeTrial, Duration = "ONE_WEEK", Periods = 1 }];

        var result = Plan(_apple, Snapshot(product), null, new MonetizationChangeDto
        {
            Kind = MonetizationChangeKind.IntroductoryOffer,
            ProductId = "com.kumbara.plus",
            Mode = OfferMode.FreeTrial,
            Duration = "ONE_WEEK",
            Periods = 1,
            Territories = ["TUR", "IND"],
        });

        result.IsSuccess.Should().BeTrue();
        result.Draft.RequiredPermission.Should().Be(OrgPermission.EditContent);
        result.Draft.Warnings.Should().ContainSingle(w => w.Key == MessageKey.MonetizationIntroOfferExists);
        ((MonetizationChangeSet)result.Draft.CustomChanges).Operations.Single().Territories.Should().BeEquivalentTo(["TUR"]);
    }

    [Fact]
    public void Planner_ShouldRefuseChangesOfTheOtherStoreAndTakenProductIds()
    {
        var snapshot = Snapshot(Subscription("com.kumbara.plus", ProductState.Approved));

        Plan(_apple, snapshot, null, new MonetizationChangeDto { Kind = MonetizationChangeKind.BasePlan, ProductId = "x" }).ErrorKey.Should().Be(MessageKey.MonetizationChangeUnsupported);
        Plan(_apple, snapshot, null, new MonetizationChangeDto { Kind = MonetizationChangeKind.CreateIap, ProductKind = StoreProductKind.Consumable, ProductId = "com.kumbara.plus", ReferenceName = "x" }).ErrorKey.Should().Be(MessageKey.MonetizationProductIdTaken);
        Plan(_apple, snapshot, null).ErrorKey.Should().Be(MessageKey.PlanNoChanges);
    }

    [Fact]
    public void Planner_ShouldValidatePlayPricesPerRegion()
    {
        var snapshot = new MonetizationSnapshot { Store = StoreKind.Google };

        var result = Plan(_google, snapshot, null,
                          new MonetizationChangeDto { Kind = MonetizationChangeKind.PlaySubscription, ProductId = "premium", Localizations = [new LocalizationInputDto { Locale = "en-US", Name = "Premium" }] },
                          new MonetizationChangeDto { Kind = MonetizationChangeKind.BasePlan, ProductId = "premium", BasePlanId = "monthly", Duration = "P1M", Prices = [new PriceInputDto { Territory = "JP", Currency = "JPY", Amount = "299.5" }] });

        result.ErrorKey.Should().Be(MessageKey.MonetizationPriceInvalid);
        result.ErrorDetail.Should().Be("Prices[JP].Amount");
    }

    [Fact]
    public void Content_ShouldFlattenAndScopeByKeys()
    {
        var product = Subscription("com.kumbara.plus", ProductState.Approved, Price("TUR", "spp-TUR-5", "49.99", "TRY"));
        product.Localizations = [new ProductLocalization { Locale = "tr", Name = "Plus" }];

        var values = MonetizationContent.Flatten(Snapshot(product));
        var priceKey = MonetizationKeys.Of(MonetizationKeys.Price, "com.kumbara.plus", "TUR", "pricePoint");
        var (parts, productIds) = MonetizationContent.ScopeOf([priceKey, MonetizationKeys.Of(MonetizationKeys.Product, "com.kumbara.plus", "tr", "name")]);

        values[priceKey].Should().Be("spp-TUR-5");
        values[MonetizationKeys.Of(MonetizationKeys.Product, "com.kumbara.plus", "tr", "name")].Should().Be("Plus");
        ContentKey.Parse(priceKey).Field.Should().Be("com.kumbara.plus/pricePoint");
        parts.Should().HaveFlag(MonetizationParts.Prices).And.HaveFlag(MonetizationParts.Localizations);
        productIds.Should().BeEquivalentTo(["com.kumbara.plus"]);
        MonetizationContent.ProductIdOf("premium:monthly").Should().Be("premium");
    }

    private static MonetizationPlanResult Plan(StoreAppContext app, MonetizationSnapshot snapshot, Dictionary<string, IReadOnlyList<PricePointDto>> equalized, params MonetizationChangeDto[] changes)
        => MonetizationPlanner.Build(new MonetizationPlanInput(app, snapshot, ChangePlanKind.Monetization, changes, equalized, null, [], [], _today, FakeLocalizer.Create().Object));

    private static MonetizationSnapshot Snapshot(params StoreProduct[] products) => new()
    {
        Store = StoreKind.Apple,
        Parts = MonetizationParts.All,
        Products = [.. products],
    };

    private static StoreProduct Subscription(string productId, ProductState state, params ProductPrice[] prices) => new()
    {
        Store = StoreKind.Apple,
        Kind = StoreProductKind.Subscription,
        ExternalId = "sub-" + productId,
        ProductId = productId,
        ReferenceName = productId,
        Duration = "ONE_MONTH",
        State = state,
        Prices = [.. prices],
    };

    private static ProductPrice Price(string territory, string pricePointId, string customerPrice, string currency, PriceSource source = PriceSource.Store) => new()
    {
        Territory = territory,
        PricePointId = pricePointId,
        CustomerPrice = customerPrice,
        Currency = currency,
        Source = source,
    };
}
