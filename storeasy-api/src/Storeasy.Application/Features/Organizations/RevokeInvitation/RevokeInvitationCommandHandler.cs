using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.RevokeInvitation;

/// <summary>
/// Handles <see cref="RevokeInvitationCommand"/>.
/// </summary>
/// <param name="InvitationRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record RevokeInvitationCommandHandler(IStoreasyRepositoryBase<OrganizationInvitation> InvitationRepository,
                                             IAuditTrail AuditTrail,
                                             TimeProvider TimeProvider) : IInterceptable, ICommandHandler<RevokeInvitationCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationInvitation> _invitationRepository = InvitationRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RevokeInvitationCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var revoked = await _invitationRepository.ExecuteUpdateAsync(i => i.Id == request.InvitationId && i.OrganizationId == request.OrganizationId && i.AcceptDate == null && i.RevokeDate == null,
                                                                     new SetPropertyBuilder<OrganizationInvitation>().SetPropertyValue(i => i.RevokeDate, now),
                                                                     cancellationToken);

        if (revoked == 0)
            return Response<int>.Error(0, MessageKey.InvitationInvalid);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.InvitationRevoked, request.InvitationId.ToString(), "Invitation revoked.", cancellationToken: cancellationToken);

        return Response<int>.Success(request.InvitationId);
    }
}
