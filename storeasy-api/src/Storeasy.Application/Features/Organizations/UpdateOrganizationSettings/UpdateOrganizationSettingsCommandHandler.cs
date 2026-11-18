using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.UpdateOrganizationSettings;

/// <summary>
/// Handles <see cref="UpdateOrganizationSettingsCommand"/> with one update of the sent columns and an audit entry.
/// </summary>
/// <param name="OrganizationRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateOrganizationSettingsCommandHandler(IStoreasyRepositoryBase<Organization> OrganizationRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateOrganizationSettingsCommand, int>
{
    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = OrganizationRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateOrganizationSettingsCommand request, CancellationToken cancellationToken)
    {
        if (request.AuditRetentionDays == null && request.DefaultLocale == null)
            return Response<int>.Success(request.OrganizationId);

        var builder = new SetPropertyBuilder<Organization>();

        if (request.AuditRetentionDays.HasValue)
            builder.SetPropertyValue(o => o.AuditRetentionDays, request.AuditRetentionDays.Value);

        if (request.DefaultLocale != null)
            builder.SetPropertyValue(o => o.DefaultLocale, string.IsNullOrWhiteSpace(request.DefaultLocale) ? null : request.DefaultLocale.Trim());

        var updated = await _organizationRepository.ExecuteUpdateAsync(o => o.Id == request.OrganizationId, builder, cancellationToken);

        if (updated == 0)
            return Response<int>.Error(0, MessageKey.OrganizationNotFound);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.OrganizationUpdated,
                                     request.OrganizationId.ToString(),
                                     "Organization settings updated.",
                                     details: new { request.AuditRetentionDays, request.DefaultLocale },
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(request.OrganizationId);
    }
}
