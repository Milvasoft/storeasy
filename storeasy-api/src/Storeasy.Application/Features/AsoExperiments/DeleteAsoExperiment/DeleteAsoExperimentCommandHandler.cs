using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Experiments;
using System.Globalization;

namespace Storeasy.Application.Features.AsoExperiments.DeleteAsoExperiment;

/// <summary>
/// Handles <see cref="DeleteAsoExperimentCommand"/>. An App Store record that exists in the store stays, so its history
/// keeps matching App Store Connect.
/// </summary>
/// <param name="ExperimentRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record DeleteAsoExperimentCommandHandler(IStoreasyRepositoryBase<AsoExperiment> ExperimentRepository,
                                                IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<DeleteAsoExperimentCommand, bool>
{
    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = ExperimentRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<bool>> Handle(DeleteAsoExperimentCommand request, CancellationToken cancellationToken)
    {
        var record = await _experimentRepository.FindAsync(request.OrganizationId, request.Id, cancellationToken);

        if (record == null)
            return Response<bool>.Error(false, MessageKey.AsoExperimentNotFound);

        if (AsoExperimentRules.IsApple(record.Kind) && record.ExternalId != null)
            return Response<bool>.Error(false, MessageKey.AsoExperimentNotDeletable);

        await _experimentRepository.ExecuteDeleteAsync(r => r.Id == record.Id && r.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AsoExperimentDeleted,
                                     record.Id.ToString(CultureInfo.InvariantCulture),
                                     $"{record.Kind} '{record.Name}' deleted.",
                                     record.Store,
                                     new { record.Id, record.Kind, record.StoreAppId },
                                     cancellationToken);

        return Response<bool>.Success(true);
    }
}
