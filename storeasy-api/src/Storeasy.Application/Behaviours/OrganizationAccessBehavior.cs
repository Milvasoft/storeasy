using MediatR;
using Milvasoft.Core.Exceptions;
using Storeasy.Domain.Organizations;
using System.Collections.Concurrent;
using System.Reflection;

namespace Storeasy.Application.Behaviours;

/// <summary>
/// Checks <see cref="OrgAuthAttribute"/> before the handler runs. A caller who is not a member gets
/// <see cref="MessageKey.OrganizationNotFound"/> (404, so organization ids cannot be probed); a member whose role lacks
/// the permission gets <see cref="MessageKey.Forbidden"/> (403). The resolved role is kept on
/// <see cref="IOrganizationAccess.CurrentRole"/> for handlers that need it (member management).
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
/// <param name="currentUser"></param>
/// <param name="organizationAccess"></param>
public sealed class OrganizationAccessBehavior<TRequest, TResponse>(ICurrentUser currentUser, IOrganizationAccess organizationAccess) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, OrgAuthAttribute> _attributeCache = new();

    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IOrganizationAccess _organizationAccess = organizationAccess;

    /// <inheritdoc/>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var attribute = _attributeCache.GetOrAdd(typeof(TRequest), type => type.GetCustomAttribute<OrgAuthAttribute>(inherit: true));

        if (attribute == null)
            return await next(cancellationToken);

        if (request is not IOrganizationRequest organizationRequest)
            throw new InvalidOperationException($"{typeof(TRequest).Name} has [OrgAuth] but does not implement {nameof(IOrganizationRequest)}.");

        var userId = _currentUser.GetRequiredId();

        var role = await _organizationAccess.GetRoleAsync(organizationRequest.OrganizationId, userId, cancellationToken);

        if (role == null)
            throw new MilvaUserFriendlyException(MessageKey.OrganizationNotFound);

        if (!OrgRoleRules.Allows(role.Value, attribute.Permission))
            throw new MilvaUserFriendlyException(MessageKey.OrganizationForbidden);

        _organizationAccess.SetCurrentRole(role.Value);

        return await next(cancellationToken);
    }
}
