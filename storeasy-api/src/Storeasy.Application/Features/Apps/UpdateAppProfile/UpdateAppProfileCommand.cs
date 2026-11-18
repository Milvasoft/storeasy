using Milvasoft.Components.CQRS.Command;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.UpdateAppProfile;

/// <summary>
/// Sets what the rules catalog needs to know about an app that the stores do not report: platforms, device families, regions, developer region, app type, features, build SDK, target API level, live or new. Null lists are "not known".
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record UpdateAppProfileCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Platforms (ios, ipados, android...).
    /// </summary>
    public List<string> Platforms { get; set; }

    /// <summary>
    /// Device families (iphone, ipad, phone, tablet-7, tablet-10...).
    /// </summary>
    public List<string> DeviceFamilies { get; set; }

    /// <summary>
    /// Distribution regions (ISO 3166-1 alpha-2); null for every region.
    /// </summary>
    public List<string> Regions { get; set; }

    /// <summary>
    /// Developer account region.
    /// </summary>
    public string DeveloperRegion { get; set; }

    /// <summary>
    /// app or game.
    /// </summary>
    public string AppType { get; set; }

    /// <summary>
    /// Feature flags (subscriptions, aigc, shared-location...).
    /// </summary>
    public List<string> Features { get; set; }

    /// <summary>
    /// iOS SDK of the newest build (e.g. 26.1).
    /// </summary>
    public string BuildSdkVersion { get; set; }

    /// <summary>
    /// Android target API level of the newest bundle.
    /// </summary>
    public int? TargetSdk { get; set; }

    /// <summary>
    /// Live in the stores (update) or new; null to derive it from the store.
    /// </summary>
    public bool? IsLive { get; set; }
}
