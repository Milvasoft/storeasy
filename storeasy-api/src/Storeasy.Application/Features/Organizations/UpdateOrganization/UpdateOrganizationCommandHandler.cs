using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.UpdateOrganization;

/// <summary>
/// Handles <see cref="UpdateOrganizationCommand"/>.
/// </summary>
/// <param name="OrganizationRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateOrganizationCommandHandler(IStoreasyRepositoryBase<Organization> OrganizationRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateOrganizationCommand, int>
{
    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = OrganizationRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var updatedCount = await _organizationRepository.ExecuteUpdateAsync(request.OrganizationId,
                                                                            new SetPropertyBuilder<Organization>().SetPropertyValue(o => o.Name, name),
                                                                            cancellationToken: cancellationToken);

        if (updatedCount == 0)
            return Response<int>.Error(0, MessageKey.OrganizationNotFound);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.OrganizationUpdated, request.OrganizationId.ToString(), $"Organization renamed to '{name}'.", cancellationToken: cancellationToken);

        return Response<int>.Success(request.OrganizationId);
    }
}
