using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Features.Registries.RemoveRegistryConnection;

/// <summary>
/// Handles <see cref="RemoveRegistryConnectionCommand"/>. A credential of another organization is reported as not found.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="ReminderRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record RemoveRegistryConnectionCommandHandler(IStoreasyRepositoryBase<RegistryConnection> ConnectionRepository,
                                                     IStoreasyRepositoryBase<RegistryTokenReminder> ReminderRepository,
                                                     IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<RemoveRegistryConnectionCommand, int>
{
    private readonly IStoreasyRepositoryBase<RegistryConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<RegistryTokenReminder> _reminderRepository = ReminderRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RemoveRegistryConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.Id && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new RegistryConnection { Id = c.Id, Kind = c.Kind, Fingerprint = c.Fingerprint, TokenReminderId = c.TokenReminderId },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<int>.Error(0, MessageKey.RegistryConnectionNotFound);

        await _connectionRepository.ExecuteDeleteAsync(c => c.Id == connection.Id && c.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        if (connection.TokenReminderId is int reminderId)
            await _reminderRepository.ExecuteDeleteAsync(r => r.Id == reminderId && r.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RegistryConnectionRemoved, connection.Id.ToString(CultureInfo.InvariantCulture), $"{connection.Kind} credential removed ({connection.Fingerprint}).", cancellationToken: cancellationToken);

        return Response<int>.Success(connection.Id);
    }
}
