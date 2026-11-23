using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Registries.RemoveTokenReminder;

/// <summary>
/// Removes a registry token reminder and its alerts.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record RemoveTokenReminderCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Reminder.
    /// </summary>
    public int Id { get; set; }
}
