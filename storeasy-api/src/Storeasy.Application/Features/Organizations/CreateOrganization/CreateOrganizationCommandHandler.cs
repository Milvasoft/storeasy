using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.CreateOrganization;

/// <summary>
/// Handles organization creation. The organization and the owner membership are written together. A single team
/// instance has exactly one organization: once it exists, another one is refused with
/// <see cref="MessageKey.SingleTeamOrganizationExists"/>.
/// </summary>
/// <param name="OrganizationRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="DeploymentOptions"></param>
[Log]
public record CreateOrganizationCommandHandler(IStoreasyRepositoryBase<Organization> OrganizationRepository,
                                               ICurrentUser CurrentUser,
                                               IAuditTrail AuditTrail,
                                               DeploymentOptions DeploymentOptions) : IInterceptable, ICommandHandler<CreateOrganizationCommand, int>
{
    private readonly DeploymentOptions _deploymentOptions = DeploymentOptions;
    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = OrganizationRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();

        if (_deploymentOptions.IsSingleTeam)
        {
            var existing = await _organizationRepository.GetFirstOrDefaultAsync(o => o.Id > 0, projection: o => (int?)o.Id, cancellationToken: cancellationToken);

            if (existing.HasValue)
                return Response<int>.Error(0, MessageKey.SingleTeamOrganizationExists);
        }

        var slug = OrgRoleRules.ToSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);

        var slugTaken = await _organizationRepository.GetFirstOrDefaultAsync(o => o.Slug == slug, projection: o => (int?)o.Id, cancellationToken: cancellationToken);

        if (slugTaken.HasValue)
            return Response<int>.Error(0, MessageKey.OrganizationSlugTaken);

        var organization = new Organization
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Members = [new OrganizationMember { UserId = userId, Role = OrgRole.Owner }],
        };

        await _organizationRepository.AddAsync(organization, cancellationToken);

        await _auditTrail.WriteAsync(organization.Id, AuditAction.OrganizationCreated, organization.Id.ToString(), $"Organization '{organization.Name}' created.", cancellationToken: cancellationToken);

        return Response<int>.Success(organization.Id);
    }
}
