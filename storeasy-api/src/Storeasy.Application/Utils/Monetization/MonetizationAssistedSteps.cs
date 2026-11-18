using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Utils.Monetization;

/// <summary>
/// Purchase settings without an API, kept as assisted steps (docs/urun-plani.md 11.4). They are opened on demand from
/// the money screens and the readiness check of apps that sell something, not for every app.
/// </summary>
public static class MonetizationAssistedSteps
{
    /// <summary>
    /// Play Real-time developer notifications (Pub/Sub topic), set in Play Console.
    /// </summary>
    public const string RealTimeNotifications = "google.rtdn";

    /// <summary>
    /// Play license testers, set in Play Console.
    /// </summary>
    public const string LicenseTesters = "google.license-testers";

    private const string _playConsole = "https://play.google.com/console";

    /// <summary>
    /// The steps.
    /// </summary>
    public static IReadOnlyList<AssistedStepDefinition> All { get; } =
    [
        new(RealTimeNotifications, StoreKind.Google, null, _playConsole, [], 3, 1),
        new(LicenseTesters, StoreKind.Google, null, _playConsole, [], 2, 0),
    ];
}
