using FluentAssertions;
using Storeasy.Application.Utils.Helpers;
using System.Text;
using System.Text.Json;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Google Play Real-time developer notifications: Pub/Sub push decoding, type names, token masking.")]
public class PlayDeveloperNotificationsTests
{
    private static string Push(object notification, string messageId = "136969346945")
        => JsonSerializer.Serialize(new
        {
            message = new { attributes = new { }, data = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notification))), messageId },
            subscription = "projects/p/subscriptions/s",
        });

    [Theory]
    [InlineData(2, "SUBSCRIPTION_RENEWED")]
    [InlineData(4, "SUBSCRIPTION_PURCHASED")]
    [InlineData(13, "SUBSCRIPTION_EXPIRED")]
    [InlineData(22, "SUBSCRIPTION_PRICE_STEP_UP_CONSENT_UPDATED")]
    [InlineData(99, "SUBSCRIPTION_NOTIFICATION_99")]
    public void Subscription_ShouldBeNamedAndTheTokenMasked(int type, string expected)
    {
        // Arrange
        var body = Push(new { version = "1.0", packageName = "com.some.thing", eventTimeMillis = "1503349566168", subscriptionNotification = new { version = "1.0", notificationType = type, purchaseToken = "PURCHASE_TOKEN_SECRET_VALUE", subscriptionId = "premium" } });

        // Act
        var parsed = PlayDeveloperNotifications.Parse(body);

        // Assert
        parsed.EventType.Should().Be(expected);
        parsed.MessageId.Should().Be("136969346945");
        parsed.PackageName.Should().Be("com.some.thing");
        parsed.ProductId.Should().Be("premium");
        parsed.EventDate.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1503349566168).UtcDateTime);
        parsed.RedactedJson.Should().NotContain("PURCHASE_TOKEN_SECRET_VALUE").And.Contain("PURC***ALUE");
    }

    [Fact]
    public void OtherKinds_ShouldBeNamed()
    {
        // Arrange
        var oneTime = Push(new { packageName = "a", eventTimeMillis = 1503349566168, oneTimeProductNotification = new { notificationType = 1, purchaseToken = "t", sku = "coins_100" } });
        var voided = Push(new { packageName = "a", voidedPurchaseNotification = new { purchaseToken = "t", orderId = "GS.1", productType = 2, refundType = 1 } });
        var refund = Push(new { packageName = "a", pendingRefundReviewNotification = new { pendingRefundToken = "refund-token-value", orderId = "GPA.1", refundReason = 7 } });
        var test = Push(new { version = "1.0", packageName = "a", testNotification = new { version = "1.0" } });

        // Act
        var parsedOneTime = PlayDeveloperNotifications.Parse(oneTime);
        var parsedVoided = PlayDeveloperNotifications.Parse(voided);
        var parsedRefund = PlayDeveloperNotifications.Parse(refund);
        var parsedTest = PlayDeveloperNotifications.Parse(test);

        // Assert
        parsedOneTime.Should().Match<PlayNotification>(p => p.EventType == "ONE_TIME_PRODUCT_PURCHASED" && p.ProductId == "coins_100" && p.EventDate.HasValue);
        parsedVoided.Should().Match<PlayNotification>(p => p.EventType == PlayDeveloperNotifications.VoidedPurchaseEvent && p.ProductId == "ONE_TIME");
        parsedRefund.EventType.Should().Be(PlayDeveloperNotifications.PendingRefundReviewEvent);
        parsedRefund.RedactedJson.Should().NotContain("refund-token-value");
        parsedTest.EventType.Should().Be(PlayDeveloperNotifications.TestEvent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"message":{"messageId":"1"}}""")]
    [InlineData("""{"message":{"data":"%%%","messageId":"1"}}""")]
    [InlineData("""{"data":{"type":"webhookPingCreated"}}""")]
    public void NotAPush_ShouldBeNull(string body)
        => PlayDeveloperNotifications.Parse(body).Should().BeNull();
}
