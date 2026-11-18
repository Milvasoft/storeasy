namespace Storeasy.Application.Interfaces;

/// <summary>
/// A request that acts inside one organization. Together with <see cref="Utils.Attributes.OrgAuthAttribute"/> on the
/// request type, the organization access behaviour checks the caller's role before the handler runs; handlers still
/// filter every query by <see cref="OrganizationId"/>.
/// </summary>
public interface IOrganizationRequest
{
    /// <summary>
    /// Organization the request acts in.
    /// </summary>
    int OrganizationId { get; }
}
