using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.PurchaseSettings.PlanServerNotifications;

/// <summary>
/// Plans the App Store Server Notifications addresses of an app (production and sandbox). Owner and Admin, like webhooks.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record PlanServerNotificationsCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Production address (https); null keeps it.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Sandbox address (https); null keeps it.
    /// </summary>
    public string SandboxUrl { get; set; }

    /// <summary>
    /// Notification version (V1, V2; default V2).
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
