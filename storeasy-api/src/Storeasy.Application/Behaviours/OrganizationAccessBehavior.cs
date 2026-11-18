using MediatR;
using Milvasoft.Core.Exceptions;
using Storeasy.Domain.Organizations;
using System.Collections.Concurrent;
using System.Reflection;

namespace Storeasy.Application.Behaviours;

/// <summary>
/// Checks <see cref="OrgAuthAttribute"/> before the handler runs. A caller who is not a member gets
/// <see cref="MessageKey.OrganizationNotFound"/> (404, so organization ids cannot be probed); a member whose role lacks
/// the permission gets <see cref="MessageKey.Forbidden"/> (403). Custom roles count with their own permission set. The
/// resolved access is kept on <see cref="IOrganizationAccess.Current"/> for handlers that need it (member management).
/// A request made with a storeasy token acts in the token's organization: the token's organization replaces the one in the
/// request. A read only personal access token acts as a viewer (min of token and role). CI and upload tokens pass only
/// requests marked <see cref="AllowMachineTokenAttribute"/> (the handler limits them to their apps).
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
/// <param name="currentUser"></param>
/// <param name="organizationAccess"></param>
/// <param name="currentToken"></param>
public sealed class OrganizationAccessBehavior<TRequest, TResponse>(ICurrentUser currentUser, IOrganizationAccess organizationAccess, ICurrentToken currentToken) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, OrgAuthAttribute> _attributeCache = new();
    private static readonly ConcurrentDictionary<Type, bool> _machineCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo> _organizationPropertyCache = new();

    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IOrganizationAccess _organizationAccess = organizationAccess;
    private readonly ICurrentToken _currentToken = currentToken;

    /// <inheritdoc/>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IOrganizationRequest boundRequest && _currentToken.OrganizationId is { } tokenOrganizationId && boundRequest.OrganizationId != tokenOrganizationId)
            BindOrganization(request, tokenOrganizationId);

        var attribute = _attributeCache.GetOrAdd(typeof(TRequest), type => type.GetCustomAttribute<OrgAuthAttribute>(inherit: true));

        if (attribute == null)
            return await next(cancellationToken);

        if (request is not IOrganizationRequest organizationRequest)
            throw new InvalidOperationException($"{typeof(TRequest).Name} has [OrgAuth] but does not implement {nameof(IOrganizationRequest)}.");

        if (_currentToken.IsMachine)
        {
            if (!_machineCache.GetOrAdd(typeof(TRequest), type => type.GetCustomAttribute<AllowMachineTokenAttribute>(inherit: true) != null))
                throw new MilvaUserFriendlyException(MessageKey.OrganizationForbidden);

            return await next(cancellationToken);
        }

        var userId = _currentUser.GetRequiredId();

        var access = await _organizationAccess.GetAccessAsync(organizationRequest.OrganizationId, userId, cancellationToken) ?? throw new MilvaUserFriendlyException(MessageKey.OrganizationNotFound);

        var effective = _currentToken.ReadOnly ? access.AsReadOnly() : access;

        if (!effective.Allows(attribute.Permission))
            throw new MilvaUserFriendlyException(MessageKey.OrganizationForbidden);

        _organizationAccess.SetCurrent(effective);

        return await next(cancellationToken);
    }

    private static void BindOrganization(TRequest request, int organizationId)
    {
        var property = _organizationPropertyCache.GetOrAdd(typeof(TRequest), type => type.GetProperty(nameof(IOrganizationRequest.OrganizationId)) is { CanWrite: true } p ? p : null);

        property?.SetValue(request, organizationId);
    }
}
