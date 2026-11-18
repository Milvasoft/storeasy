namespace Storeasy.Domain.JsonModels;

/// <summary>
/// What the rules catalog needs to know about an app that the stores do not report: device families, regions, features. Members fill it; null fields are "not known" and make the rules that depend on them evaluate to unknown.
/// </summary>
public class AppProfile
{
    /// <summary>
    /// Platforms (ios, ipados, macos, android, wear-os...).
    /// </summary>
    public List<string> Platforms { get; set; }

    /// <summary>
    /// Device families the app supports (iphone, ipad, phone, tablet-7, tablet-10...).
    /// </summary>
    public List<string> DeviceFamilies { get; set; }

    /// <summary>
    /// Distribution regions (ISO 3166-1 alpha-2); null means every region.
    /// </summary>
    public List<string> Regions { get; set; }

    /// <summary>
    /// Region of the developer account.
    /// </summary>
    public string DeveloperRegion { get; set; }

    /// <summary>
    /// app or game.
    /// </summary>
    public string AppType { get; set; }

    /// <summary>
    /// Feature flags (subscriptions, aigc, shared-location, uses-location...).
    /// </summary>
    public List<string> Features { get; set; }

    /// <summary>
    /// iOS SDK version the newest build is built with (e.g. 26.1).
    /// </summary>
    public string BuildSdkVersion { get; set; }

    /// <summary>
    /// Android target API level of the newest bundle.
    /// </summary>
    public int? TargetSdk { get; set; }

    /// <summary>
    /// Whether the app is already live in the stores (update) or not yet (new app); null to derive it from the store.
    /// </summary>
    public bool? IsLive { get; set; }
}
