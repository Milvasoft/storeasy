using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.CiTokens.RevokeCiToken;

/// <summary>
/// Handles <see cref="RevokeCiTokenCommand"/>.
/// </summary>
/// <param name="TokenRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record RevokeCiTokenCommandHandler(IStoreasyRepositoryBase<CiToken> TokenRepository,
                                          IAuditTrail AuditTrail,
                                          TimeProvider TimeProvider) : IInterceptable, ICommandHandler<RevokeCiTokenCommand, int>
{
    private readonly IStoreasyRepositoryBase<CiToken> _tokenRepository = TokenRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RevokeCiTokenCommand request, CancellationToken cancellationToken)
    {
        var revoked = await _tokenRepository.ExecuteUpdateAsync(t => t.Id == request.Id && t.OrganizationId == request.OrganizationId && t.RevokeDate == null,
                                                                new SetPropertyBuilder<CiToken>().SetPropertyValue(t => t.RevokeDate, (DateTime?)_timeProvider.GetUtcNow().UtcDateTime),
                                                                cancellationToken);

        if (revoked == 0)
            return Response<int>.Error(0, MessageKey.CiTokenNotFound);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.CiTokenRevoked, request.Id.ToString(), $"CI token {request.Id} revoked.", cancellationToken: cancellationToken);

        return Response<int>.Success(request.Id);
    }
}
