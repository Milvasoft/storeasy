using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.ArtifactSources.DeleteArtifactSource;

/// <summary>
/// Handles <see cref="DeleteArtifactSourceCommand"/>.
/// </summary>
/// <param name="SourceRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record DeleteArtifactSourceCommandHandler(IStoreasyRepositoryBase<ArtifactPullSource> SourceRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<DeleteArtifactSourceCommand, int>
{
    private readonly IStoreasyRepositoryBase<ArtifactPullSource> _sourceRepository = SourceRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteArtifactSourceCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _sourceRepository.ExecuteDeleteAsync(s => s.Id == request.Id && s.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        if (deleted == 0)
            return Response<int>.Error(0, MessageKey.ArtifactSourceNotFound);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.ArtifactSourceUpdated, request.Id.ToString(), $"Pull source {request.Id} removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(request.Id);
    }
}
