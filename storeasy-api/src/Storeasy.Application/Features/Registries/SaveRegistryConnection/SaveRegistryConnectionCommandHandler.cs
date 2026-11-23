using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;
using System.Globalization;
using System.Text;

namespace Storeasy.Application.Features.Registries.SaveRegistryConnection;

/// <summary>
/// Handles <see cref="SaveRegistryConnectionCommand"/>. The secret never reaches a log, an audit entry or an answer.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="ReminderRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="GitHub"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
public record SaveRegistryConnectionCommandHandler(IStoreasyRepositoryBase<RegistryConnection> ConnectionRepository,
                                                   IStoreasyRepositoryBase<RegistryTokenReminder> ReminderRepository,
                                                   ISecretProtector SecretProtector,
                                                   IRegistryGitHub GitHub,
                                                   IAuditTrail AuditTrail,
                                                   TimeProvider TimeProvider) : IInterceptable, ICommandHandler<SaveRegistryConnectionCommand, int>
{
    private readonly IStoreasyRepositoryBase<RegistryConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<RegistryTokenReminder> _reminderRepository = ReminderRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IRegistryGitHub _gitHub = GitHub;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(SaveRegistryConnectionCommand request, CancellationToken cancellationToken)
    {
        var secret = request.Secret.Trim();
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.OrganizationId == request.OrganizationId && c.Kind == request.Kind, cancellationToken: cancellationToken)
                         ?? new RegistryConnection { OrganizationId = request.OrganizationId, Kind = request.Kind };
        var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(secret), RegistrySecrets.Context(request.OrganizationId, request.Kind));

        connection.EncryptedSecret = sealedSecret.EncryptedSecret;
        connection.EncryptedDataKey = sealedSecret.EncryptedDataKey;
        connection.KeyVersion = sealedSecret.KeyVersion;
        connection.ExpiryDate = request.ExpiryDate?.Date is DateTime expiry ? DateTime.SpecifyKind(expiry, DateTimeKind.Utc) : null;
        connection.Fingerprint = RegistrySecrets.Fingerprint(secret);
        connection.Status = ConnectionStatus.NotVerified;
        connection.LastVerifyError = null;

        if (request.Kind == RegistryConnectionKind.GitHub)
            await VerifyAsync(connection, secret, cancellationToken);

        connection.TokenReminderId = await ReminderAsync(connection, cancellationToken);

        if (connection.Id == 0)
            await _connectionRepository.AddAsync(connection, cancellationToken);
        else
            await _connectionRepository.UpdateAsync(connection, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RegistryConnectionSaved, connection.Id.ToString(CultureInfo.InvariantCulture), $"{request.Kind} credential saved ({connection.Fingerprint}).", details: new { request.Kind, connection.Fingerprint, connection.Status }, cancellationToken: cancellationToken);

        return Response<int>.Success(connection.Id);
    }

    private async Task VerifyAsync(RegistryConnection connection, string secret, CancellationToken cancellationToken)
    {
        try
        {
            connection.Fingerprint = await _gitHub.GetLoginAsync(secret, cancellationToken);
            connection.Status = ConnectionStatus.Verified;
            connection.LastVerifiedDate = _timeProvider.GetUtcNow().UtcDateTime;
        }
        catch (StoreCallException ex)
        {
            connection.Status = ex.Transient ? ConnectionStatus.Unreachable : ConnectionStatus.Rejected;
            connection.LastVerifyError = SecretRedactor.RedactAndTrim(ex.Message, RegistryConnection.ErrorMaxLength);
        }
    }

    /// <summary>
    /// A registry key with an expiry date keeps a token reminder (created, moved or removed with the key).
    /// </summary>
    private async Task<int?> ReminderAsync(RegistryConnection connection, CancellationToken cancellationToken)
    {
        if (RegistrySecrets.RegistryOf(connection.Kind) is not PackageRegistry registry)
            return null;

        var reminder = connection.TokenReminderId is int id
            ? await _reminderRepository.GetFirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == connection.OrganizationId, cancellationToken: cancellationToken)
            : null;

        if (connection.ExpiryDate is not DateTime expiry)
        {
            if (reminder != null)
                await _reminderRepository.ExecuteDeleteAsync(r => r.Id == reminder.Id && r.OrganizationId == connection.OrganizationId, cancellationToken: cancellationToken);

            return null;
        }

        reminder ??= new RegistryTokenReminder { OrganizationId = connection.OrganizationId };

        if (reminder.ExpiryDate != expiry)
            reminder.WarnedStage = 0;

        reminder.Registry = registry;
        reminder.Label = $"storeasy {RegistrySecrets.DisplayName(connection.Kind)}";
        reminder.UsedIn = "storeasy registry operations";
        reminder.ExpiryDate = expiry;

        if (reminder.Id == 0)
            await _reminderRepository.AddAsync(reminder, cancellationToken);
        else
            await _reminderRepository.UpdateAsync(reminder, cancellationToken);

        return reminder.Id;
    }
}
