namespace Storeasy.Application.Utils.Attributes;

/// <summary>
/// Declares on a request type (which must implement <see cref="IOrganizationRequest"/>) the organization permission
/// the caller needs. Checked by <see cref="Behaviours.OrganizationAccessBehavior{TRequest, TResponse}"/> for every
/// entry point (controllers and MCP tools send the same request through MediatR).
/// </summary>
/// <param name="permission"></param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class OrgAuthAttribute(OrgPermission permission) : Attribute
{
    /// <summary>
    /// Required permission.
    /// </summary>
    public OrgPermission Permission { get; } = permission;
}
