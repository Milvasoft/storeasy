namespace Storeasy.Application.Interfaces;

/// <summary>
/// Reads a user's role in an organization (cached for a short time) and holds the role resolved for the current request.
/// Registered scoped.
/// </summary>
public interface IOrganizationAccess
{
    /// <summary>
    /// Role of <paramref name="userId"/> in <paramref name="organizationId"/>, or null when the user is not a member
    /// (or the organization does not exist).
    /// </summary>
    Task<OrgRole?> GetRoleAsync(int organizationId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the cached role after a membership change, so the next request reads the database.
    /// </summary>
    Task InvalidateAsync(int organizationId, int userId);

    /// <summary>
    /// Role the access behaviour resolved for the current request's organization. Null outside organization requests.
    /// </summary>
    OrgRole? CurrentRole { get; }

    /// <summary>
    /// Records the role resolved for the current request (called by the access behaviour).
    /// </summary>
    void SetCurrentRole(OrgRole role);
}
