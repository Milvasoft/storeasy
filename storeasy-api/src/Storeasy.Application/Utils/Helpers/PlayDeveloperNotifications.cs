using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Google Play Real-time developer notifications as Cloud Pub/Sub pushes them: <c>{"message":{"data":"&lt;base64&gt;",
/// "messageId":"..."},"subscription":"..."}</c>, the data being a <c>DeveloperNotification</c> with exactly one of
/// <c>subscriptionNotification</c>, <c>oneTimeProductNotification</c>, <c>voidedPurchaseNotification</c>,
/// <c>pendingRefundReviewNotification</c> or <c>testNotification</c>. Purchase and refund tokens are never kept: the stored
/// payload is the decoded notification with those values masked.
/// </summary>
public static class PlayDeveloperNotifications
{
    /// <summary>
    /// Event type of a test notification sent from Play Console.
    /// </summary>
    public const string TestEvent = "TEST_NOTIFICATION";

    /// <summary>
    /// Event type of a voided purchase.
    /// </summary>
    public const string VoidedPurchaseEvent = "VOIDED_PURCHASE";

    /// <summary>
    /// Event type of a chargeback that waits for the developer's review (to be answered within 24 hours).
    /// </summary>
    public const string PendingRefundReviewEvent = "PENDING_REFUND_REVIEW";

    /// <summary>
    /// Event type when the notification has none of the known parts.
    /// </summary>
    public const string UnknownEvent = "PLAY_NOTIFICATION";

    private static readonly string[] _secretFields = ["purchaseToken", "pendingRefundToken"];

    private static readonly Dictionary<int, string> _subscriptionTypes = new()
    {
        [1] = "SUBSCRIPTION_RECOVERED",
        [2] = "SUBSCRIPTION_RENEWED",
        [3] = "SUBSCRIPTION_CANCELED",
        [4] = "SUBSCRIPTION_PURCHASED",
        [5] = "SUBSCRIPTION_ON_HOLD",
        [6] = "SUBSCRIPTION_IN_GRACE_PERIOD",
        [7] = "SUBSCRIPTION_RESTARTED",
        [8] = "SUBSCRIPTION_PRICE_CHANGE_CONFIRMED",
        [9] = "SUBSCRIPTION_DEFERRED",
        [10] = "SUBSCRIPTION_PAUSED",
        [11] = "SUBSCRIPTION_PAUSE_SCHEDULE_CHANGED",
        [12] = "SUBSCRIPTION_REVOKED",
        [13] = "SUBSCRIPTION_EXPIRED",
        [17] = "SUBSCRIPTION_ITEMS_CHANGED",
        [18] = "SUBSCRIPTION_CANCELLATION_SCHEDULED",
        [19] = "SUBSCRIPTION_PRICE_CHANGE_UPDATED",
        [20] = "SUBSCRIPTION_PENDING_PURCHASE_CANCELED",
        [22] = "SUBSCRIPTION_PRICE_STEP_UP_CONSENT_UPDATED",
    };

    private static readonly Dictionary<int, string> _oneTimeTypes = new()
    {
        [1] = "ONE_TIME_PRODUCT_PURCHASED",
        [2] = "ONE_TIME_PRODUCT_CANCELED",
    };

    /// <summary>
    /// Reads a push body. Returns null when it is not a Pub/Sub push with a decodable notification.
    /// </summary>
    /// <param name="body"></param>
    /// <returns></returns>
    public static PlayNotification Parse(string body)
    {
        try
        {
            if (JsonNode.Parse(body ?? string.Empty) is not JsonObject root || root["message"] is not JsonObject message)
                return null;

            var messageId = Text(message["messageId"]) ?? Text(message["message_id"]);
            var data = Text(message["data"]);

            if (string.IsNullOrEmpty(data) || JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(data))) is not JsonObject notification)
                return null;

            var (eventType, productId) = Classify(notification);
            DateTime? eventDate = long.TryParse(Text(notification["eventTimeMillis"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var millis)
                ? DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime
                : null;

            Redact(notification);

            return new PlayNotification(messageId, eventType, Text(notification["packageName"]), productId, eventDate, notification.ToJsonString());
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private static (string EventType, string ProductId) Classify(JsonObject notification)
    {
        if (notification["subscriptionNotification"] is JsonObject subscription)
            return (Named(_subscriptionTypes, subscription["notificationType"], "SUBSCRIPTION_NOTIFICATION"), Text(subscription["subscriptionId"]));

        if (notification["oneTimeProductNotification"] is JsonObject oneTime)
            return (Named(_oneTimeTypes, oneTime["notificationType"], "ONE_TIME_PRODUCT_NOTIFICATION"), Text(oneTime["sku"]));

        if (notification["voidedPurchaseNotification"] is JsonObject voided)
            return (VoidedPurchaseEvent, Number(voided["productType"]) switch { 1 => "SUBSCRIPTION", 2 => "ONE_TIME", _ => null });

        if (notification["pendingRefundReviewNotification"] is JsonObject)
            return (PendingRefundReviewEvent, null);

        if (notification["testNotification"] is JsonObject)
            return (TestEvent, null);

        return (UnknownEvent, null);
    }

    private static string Named(Dictionary<int, string> names, JsonNode type, string fallback)
    {
        var number = Number(type);

        if (!number.HasValue)
            return fallback;

        return names.TryGetValue(number.Value, out var name) ? name : string.Create(CultureInfo.InvariantCulture, $"{fallback}_{number.Value}");
    }

    private static void Redact(JsonObject node)
    {
        foreach (var (key, value) in node.ToList())
        {
            if (value is JsonObject child)
                Redact(child);
            else if (value != null && _secretFields.Contains(key, StringComparer.Ordinal))
                node[key] = Mask(Text(value));
        }
    }

    private static string Mask(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= 8 ? new string('*', value.Length) : $"{value[..4]}***{value[^4..]}";
    }

    private static int? Number(JsonNode node)
    {
        if (node is not JsonValue value)
            return null;

        if (value.TryGetValue<int>(out var number))
            return number;

        return value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ? number : null;
    }

    private static string Text(JsonNode node)
    {
        if (node is not JsonValue value)
            return null;

        if (value.TryGetValue<string>(out var text))
            return text;

        return value.TryGetValue<long>(out var number) ? number.ToString(CultureInfo.InvariantCulture) : null;
    }
}

/// <summary>
/// A decoded Play notification.
/// </summary>
/// <param name="MessageId">Pub/Sub message id (unique per notification; the replay key).</param>
/// <param name="EventType">Notification type name (for example <c>SUBSCRIPTION_RENEWED</c>).</param>
/// <param name="PackageName">Package of the app.</param>
/// <param name="ProductId">Subscription or product id when the notification names one (voided: the product type).</param>
/// <param name="EventDate">UTC instant of the event.</param>
/// <param name="RedactedJson">The decoded notification with the tokens masked.</param>
public sealed record PlayNotification(string MessageId, string EventType, string PackageName, string ProductId, DateTime? EventDate, string RedactedJson);
