namespace Storeasy.Application.Interfaces;

/// <summary>
/// Writes organization audit entries (<see cref="Domain.Stores.StoreAuditLog"/>) with the current user, channel and
/// time filled in. Registered scoped; the MCP endpoint sets <see cref="Channel"/> to <see cref="ActionChannel.Mcp"/>.
/// </summary>
public interface IAuditTrail
{
    /// <summary>
    /// Channel of the current request. Defaults to <see cref="ActionChannel.Web"/>.
    /// </summary>
    ActionChannel Channel { get; set; }

    /// <summary>
    /// Adds one entry. <paramref name="summary"/> and <paramref name="details"/> must not carry secrets; both are
    /// passed through the secret redactor anyway.
    /// </summary>
    Task WriteAsync(int organizationId,
                    AuditAction action,
                    string targetId,
                    string summary,
                    StoreKind? store = null,
                    object details = null,
                    CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds several entries in one write. An entry's own actor and channel (set by store jobs, which run without a user)
    /// win over the current user and <see cref="Channel"/>.
    /// </summary>
    Task WriteManyAsync(IReadOnlyList<AuditEntry> entries, CancellationToken cancellationToken = default);
}

/// <summary>
/// One audit entry for <see cref="IAuditTrail.WriteManyAsync"/>.
/// </summary>
/// <param name="OrganizationId">Organization.</param>
/// <param name="Action">What happened.</param>
/// <param name="TargetId">Affected record.</param>
/// <param name="Summary">One line summary without secrets.</param>
/// <param name="Store">Store touched.</param>
/// <param name="Details">Structured details (before and after), without secrets.</param>
/// <param name="UserId">Actor; null to take the current user.</param>
/// <param name="UserName">Actor name; null to take the current user.</param>
/// <param name="Channel">Channel; null to take <see cref="IAuditTrail.Channel"/>.</param>
public sealed record AuditEntry(int OrganizationId,
                                AuditAction Action,
                                string TargetId,
                                string Summary,
                                StoreKind? Store = null,
                                object Details = null,
                                int? UserId = null,
                                string UserName = null,
                                ActionChannel? Channel = null);
