namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// App Store version states that matter to the rejection flow (App Store Connect <c>appVersionState</c> and the older
/// <c>appStoreState</c> spellings).
/// </summary>
public static class AppStoreVersionStates
{
    /// <summary>
    /// The version state event type App Store Connect sends (singular spelling, as on the current documentation pages).
    /// </summary>
    public const string StateUpdatedEvent = "APP_STORE_VERSION_APP_VERSION_STATE_UPDATED";

    /// <summary>
    /// Names the version state event arrives with: the singular spelling, the older plural heading and the payload's
    /// <c>data.type</c>. All are treated as <see cref="StateUpdatedEvent"/>.
    /// </summary>
    public static IReadOnlySet<string> StateUpdatedAliases { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        StateUpdatedEvent,
        "APP_STORE_VERSIONS_APP_VERSION_STATE_UPDATED",
        "appStoreVersionAppVersionStateUpdated",
    };

    private static readonly HashSet<string> _submitted = new(StringComparer.OrdinalIgnoreCase)
    {
        "WAITING_FOR_REVIEW", "IN_REVIEW", "PENDING_APPLE_RELEASE", "PENDING_DEVELOPER_RELEASE", "PROCESSING_FOR_DISTRIBUTION",
        "PROCESSING_FOR_APP_STORE", "READY_FOR_DISTRIBUTION", "READY_FOR_SALE", "ACCEPTED",
    };

    /// <summary>
    /// The rejection kind a state means, or null when it is not a rejection.
    /// </summary>
    public static RejectionKind? RejectionOf(string state) => state?.ToUpperInvariant() switch
    {
        "REJECTED" => RejectionKind.Rejected,
        "METADATA_REJECTED" => RejectionKind.MetadataRejected,
        _ => null,
    };

    /// <summary>
    /// Whether the version is (again) with App Review or past it: an open rejection case of the record is then closed.
    /// </summary>
    public static bool IsSubmitted(string state) => state != null && _submitted.Contains(state);

    /// <summary>
    /// Normalizes an event type name: the aliases of the version state event become <see cref="StateUpdatedEvent"/>.
    /// </summary>
    public static string NormalizeEventType(string eventType) => eventType != null && StateUpdatedAliases.Contains(eventType) ? StateUpdatedEvent : eventType;
}
