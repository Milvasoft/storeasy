using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.TrustedPublishers.DeleteTrustedPublisher;

/// <summary>
/// Handles <see cref="DeleteTrustedPublisherCommand"/>.
/// </summary>
/// <param name="RuleRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record DeleteTrustedPublisherCommandHandler(IStoreasyRepositoryBase<TrustedPublisher> RuleRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<DeleteTrustedPublisherCommand, int>
{
    private readonly IStoreasyRepositoryBase<TrustedPublisher> _ruleRepository = RuleRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteTrustedPublisherCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _ruleRepository.ExecuteDeleteAsync(r => r.Id == request.Id && r.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        if (deleted == 0)
            return Response<int>.Error(0, MessageKey.TrustedPublisherNotFound);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.TrustedPublisherRemoved, request.Id.ToString(), $"Trusted publisher {request.Id} removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(request.Id);
    }
}
