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
}
