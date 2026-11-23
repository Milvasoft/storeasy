using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Registries.SaveTokenReminder;

/// <summary>
/// Adds (<see cref="Id"/> null) or changes a registry token reminder: a label, the registry, where the token is used and its
/// expiry date. The token itself is never accepted: a label or a "used in" text that looks like a token is refused.
/// storeasy warns 14 and 3 days before and on the expiry date.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SaveTokenReminderCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Reminder to change; null adds one.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// Registry the token is for.
    /// </summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>
    /// A name for the token, for example "npm publish token" (never the token).
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Where the token is used, for example the CI secret name (never the token).
    /// </summary>
    public string UsedIn { get; set; }

    /// <summary>
    /// The day the token expires.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// A tracked package the token publishes.
    /// </summary>
    public int? TrackedPackageId { get; set; }
}
