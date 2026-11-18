using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Stores;
using System.Text;

namespace Storeasy.Application.Features.StoreConnections.VerifyStoreConnection;

/// <summary>
/// Handles <see cref="VerifyStoreConnectionCommand"/>. The secret is opened only for the store call.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="StoreGateway"></param>
/// <param name="SecretProtector"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record VerifyStoreConnectionCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                                  IStoreGateway StoreGateway,
                                                  ISecretProtector SecretProtector,
                                                  IAuditTrail AuditTrail,
                                                  TimeProvider TimeProvider) : IInterceptable, ICommandHandler<VerifyStoreConnectionCommand, StoreConnectionDto>
{
    private const int _errorMaxLength = 500;

    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreGateway _storeGateway = StoreGateway;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<StoreConnectionDto>> Handle(VerifyStoreConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new { c.Id, c.Store, c.Name, c.EncryptedSecret, c.EncryptedDataKey, c.KeyVersion },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<StoreConnectionDto>.Error(null, MessageKey.StoreConnectionNotFound);

        var secret = _secretProtector.Unprotect(new ProtectedSecret(connection.EncryptedSecret, connection.EncryptedDataKey, connection.KeyVersion),
                                                StoreSecretContext.Connection(request.OrganizationId, connection.Store));

        var result = await _storeGateway.VerifyAsync(connection.Store, Encoding.UTF8.GetString(secret), request.PackageName, cancellationToken);

        Array.Clear(secret);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var error = SecretRedactor.RedactAndTrim(result.Error, _errorMaxLength);

        var builder = new SetPropertyBuilder<StoreConnection>().SetPropertyValue(c => c.Status, result.Status)
                                                               .SetPropertyValue(c => c.LastVerifiedDate, (DateTime?)now)
                                                               .SetPropertyValue(c => c.LastVerifyError, error);

        await _connectionRepository.ExecuteUpdateAsync(connection.Id, builder, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.ConnectionVerified,
                                     connection.Id.ToString(),
                                     $"{connection.Store} connection '{connection.Name}' verified: {result.Status}.",
                                     connection.Store,
                                     cancellationToken: cancellationToken);

        var dto = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == connection.Id, projection: StoreConnectionDto.Projection, cancellationToken: cancellationToken);

        return Response<StoreConnectionDto>.Success(dto);
    }
}
