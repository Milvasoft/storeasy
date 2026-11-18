namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// Where the store sends purchase notifications of an app: App Store Server Notifications addresses (read live through
/// the App Store Connect API), or, on Play, the assisted step of Real-time developer notifications (set in Play Console).
/// </summary>
public class ServerNotificationsDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Production address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Notification version of the production address (V1, V2).
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Sandbox address.
    /// </summary>
    public string SandboxUrl { get; set; }

    /// <summary>
    /// Notification version of the sandbox address.
    /// </summary>
    public string SandboxVersion { get; set; }

    /// <summary>
    /// Whether the setting has no API and is managed as an assisted step.
    /// </summary>
    public bool Assisted { get; set; }

    /// <summary>
    /// Key of the assisted step that covers the setting.
    /// </summary>
    public string AssistedStepKey { get; set; }

    /// <summary>
    /// Assisted step record, once one was opened for the app.
    /// </summary>
    public int? AssistedStepId { get; set; }

    /// <summary>
    /// State of the assisted step.
    /// </summary>
    public AssistedStepStatus? AssistedStepStatus { get; set; }
}
