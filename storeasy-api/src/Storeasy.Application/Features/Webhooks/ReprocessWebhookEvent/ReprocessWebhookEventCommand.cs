using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Webhooks.ReprocessWebhookEvent;

/// <summary>
/// Processes an App Store Connect event again (only one with a valid signature). Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record ReprocessWebhookEventCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Event.
    /// </summary>
    public long EventId { get; set; }
}
