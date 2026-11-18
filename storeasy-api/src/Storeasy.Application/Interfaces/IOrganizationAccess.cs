using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Reads a user's access (role and permissions, custom roles included) in an organization, cached for a short time, and
/// holds the access resolved for the current request. Registered scoped.
/// </summary>
public interface IOrganizationAccess
{
    /// <summary>
    /// Access of <paramref name="userId"/> in <paramref name="organizationId"/>, or null when the user is not a member
    /// (or the organization does not exist).
    /// </summary>
    Task<OrgAccess> GetAccessAsync(int organizationId, int userId, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the cached access after a membership change, so the next request reads the database.
    /// </summary>
    Task InvalidateAsync(int organizationId, int userId);

    /// <summary>
    /// Drops the cached access of several members at once (a custom role they hold changed).
    /// </summary>
    Task InvalidateManyAsync(int organizationId, IReadOnlyCollection<int> userIds);

    /// <summary>
    /// Access the access behaviour resolved for the current request's organization. Null outside organization requests.
    /// </summary>
    OrgAccess Current { get; }

    /// <summary>
    /// Records the access resolved for the current request (called by the access behaviour).
    /// </summary>
    void SetCurrent(OrgAccess access);
}
