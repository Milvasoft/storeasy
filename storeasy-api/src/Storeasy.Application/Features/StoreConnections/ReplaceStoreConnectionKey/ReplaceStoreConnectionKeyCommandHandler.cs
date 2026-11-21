using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Stores;
using System.Text;
using System.Text.Json;

namespace Storeasy.Application.Features.StoreConnections.ReplaceStoreConnectionKey;

/// <summary>
/// Handles <see cref="ReplaceStoreConnectionKeyCommand"/>. The secret property is <c>[LogMasked]</c>. The new key is
/// parsed with the same offline checks as adding a connection; an Apple key's issuer id is compared with the one sealed in
/// the connection (opened in memory only for that); the key is verified against the store and only then sealed. The update
/// writes the sealed secret, the fingerprint and the verification result in one statement, so a failure anywhere before it
/// leaves the old key, fingerprint and status as they were.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="StoreGateway"></param>
/// <param name="SecretProtector"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
/// <param name="Channels"></param>
[Log]
public record ReplaceStoreConnectionKeyCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                                      IStoreGateway StoreGateway,
                                                      ISecretProtector SecretProtector,
                                                      IAuditTrail AuditTrail,
                                                      TimeProvider TimeProvider,
                                                      IEnumerable<IStoreChannel> Channels) : IInterceptable, ICommandHandler<ReplaceStoreConnectionKeyCommand, StoreConnectionDto>
{
    private const int _errorMaxLength = 500;

    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreGateway _storeGateway = StoreGateway;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;
    private readonly IEnumerable<IStoreChannel> _channels = Channels;

    /// <inheritdoc/>
    public async Task<Response<StoreConnectionDto>> Handle(ReplaceStoreConnectionKeyCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new StoreConnection
                                                                            {
                                                                                Id = c.Id,
                                                                                Store = c.Store,
                                                                                Name = c.Name,
                                                                                Fingerprint = c.Fingerprint,
                                                                                EncryptedSecret = c.EncryptedSecret,
                                                                                EncryptedDataKey = c.EncryptedDataKey,
                                                                                KeyVersion = c.KeyVersion,
                                                                            },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<StoreConnectionDto>.Error(null, MessageKey.StoreConnectionNotFound);

        var channel = _channels.LastOrDefault(c => c.Store == connection.Store);

        if (channel == null && connection.Store is not (StoreKind.Apple or StoreKind.Google))
            return Response<StoreConnectionDto>.Error(null, MessageKey.StoreKeyReplaceNotSupported);

        var inspection = _storeGateway.Inspect(connection.Store, request.Secret);

        if (!inspection.IsValid)
            return Response<StoreConnectionDto>.Error(null, inspection.ErrorKey);

        var current = connection.Fingerprint;
        var next = inspection.Fingerprint;

        if (channel != null && !channel.SameAccount(current, next))
            return Response<StoreConnectionDto>.Error(null, MessageKey.StoreKeyAccountMismatch);

        if (connection.Store == StoreKind.Apple && !SameAppleTeam(request.OrganizationId, connection, inspection.NormalizedSecret))
            return Response<StoreConnectionDto>.Error(null, MessageKey.StoreKeyIssuerMismatch);

        var verification = await _storeGateway.VerifyAsync(connection.Store, inspection.NormalizedSecret, null, cancellationToken);

        if (verification.Status != ConnectionStatus.Verified)
        {
            var refusal = Response<StoreConnectionDto>.Error(null, MessageKey.StoreKeyRejected);
            var reason = SecretRedactor.RedactAndTrim(verification.Error, _errorMaxLength);

            if (!string.IsNullOrWhiteSpace(reason))
                refusal.AddMessage(reason, MessageType.Warning);

            return refusal;
        }

        var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(inspection.NormalizedSecret), StoreSecretContext.Connection(request.OrganizationId, connection.Store));
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // The old credential's expiry does not carry over to the new one.
        var recordsExpiry = StoreChannels.CredentialExpires(connection.Store) && !StoreChannels.CredentialExpiryRequired(connection.Store);

        if (recordsExpiry)
            next.SecretExpiryDate = request.CredentialExpiryDate;

        var builder = new SetPropertyBuilder<StoreConnection>().SetPropertyValue(c => c.EncryptedSecret, sealedSecret.EncryptedSecret)
                                                               .SetPropertyValue(c => c.EncryptedDataKey, sealedSecret.EncryptedDataKey)
                                                               .SetPropertyValue(c => c.KeyVersion, sealedSecret.KeyVersion)
                                                               .SetPropertyValue(c => c.Fingerprint, next)
                                                               .SetPropertyValue(c => c.Status, ConnectionStatus.Verified)
                                                               .SetPropertyValue(c => c.LastVerifiedDate, (DateTime?)now)
                                                               .SetPropertyValue(c => c.LastVerifyError, (string)null);

        if (recordsExpiry)
            builder = builder.SetPropertyValue(c => c.SecretExpiryDate, request.CredentialExpiryDate);

        await _connectionRepository.ExecuteUpdateAsync(connection.Id, builder, cancellationToken);

        var previous = channel != null ? channel.CredentialLabel(current) : connection.Store == StoreKind.Apple ? current?.KeyId : current?.ServiceAccountEmail;
        var replacement = channel != null ? channel.CredentialLabel(next) : connection.Store == StoreKind.Apple ? next.KeyId : next.ServiceAccountEmail;

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.ConnectionKeyReplaced,
                                     connection.Id.ToString(),
                                     $"{connection.Store} connection '{connection.Name}' key replaced: {previous ?? "-"} -> {replacement}.",
                                     connection.Store,
                                     new { Fingerprint = next, Previous = current },
                                     cancellationToken);

        var dto = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == connection.Id, projection: StoreConnectionDto.Projection, cancellationToken: cancellationToken);

        return Response<StoreConnectionDto>.Success(dto);
    }

    /// <summary>
    /// Whether the new Apple key has the issuer id of the sealed one (the same App Store Connect team). The stored secret
    /// is opened in memory for the comparison and cleared; when it cannot be read as JSON the fingerprint's issuer suffix
    /// decides.
    /// </summary>
    private bool SameAppleTeam(int organizationId, StoreConnection connection, string newSecret)
    {
        var stored = _secretProtector.Unprotect(new ProtectedSecret(connection.EncryptedSecret, connection.EncryptedDataKey, connection.KeyVersion),
                                                StoreSecretContext.Connection(organizationId, StoreKind.Apple));

        var storedIssuer = IssuerId(Encoding.UTF8.GetString(stored));

        Array.Clear(stored);

        var newIssuer = IssuerId(newSecret);

        if (storedIssuer == null)
            return connection.Fingerprint?.IssuerIdSuffix != null && newIssuer != null && newIssuer.EndsWith(connection.Fingerprint.IssuerIdSuffix, StringComparison.OrdinalIgnoreCase);

        return string.Equals(storedIssuer, newIssuer, StringComparison.OrdinalIgnoreCase);
    }

    private static string IssuerId(string secretJson)
    {
        try
        {
            using var document = JsonDocument.Parse(secretJson);

            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("issuerId", out var issuer) && issuer.ValueKind == JsonValueKind.String
                ? issuer.GetString()?.Trim()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
