using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using System.Text.Json;

namespace Storeasy.Infrastructure.Email;

/// <summary>
/// Default <see cref="IEmailOutbox"/>: one insert per call, the payload sealed with the secret protector
/// (<see cref="SecretScopes.EmailOutbox"/>). <see cref="EmailOutboxSender"/> delivers the rows.
/// </summary>
/// <param name="outboxRepository"></param>
/// <param name="secretProtector"></param>
/// <param name="timeProvider"></param>
public sealed class EmailOutbox(IStoreasyRepositoryBase<EmailOutboxItem> outboxRepository, ISecretProtector secretProtector, TimeProvider timeProvider) : IEmailOutbox
{
    private readonly IStoreasyRepositoryBase<EmailOutboxItem> _outboxRepository = outboxRepository;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public Task EnqueueAsync(IReadOnlyList<EmailMessage> messages, CancellationToken cancellationToken)
    {
        if (messages == null || messages.Count == 0)
            return Task.CompletedTask;

        return _outboxRepository.AddRangeAsync([.. messages.Select(m => Seal(EmailOutboxKind.Message, m.Language, m))], cancellationToken);
    }

    /// <inheritdoc/>
    public Task EnqueuePasswordResetAsync(string email, string language, CancellationToken cancellationToken)
        => _outboxRepository.AddAsync(Seal(EmailOutboxKind.PasswordReset, language, new PasswordResetPayload(email)), cancellationToken);

    /// <summary>
    /// Opens the payload of a row.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="secretProtector"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public static T Open<T>(ISecretProtector secretProtector, EmailOutboxItem item)
    {
        var plain = secretProtector.Unprotect(new ProtectedSecret(item.EncryptedPayload, item.EncryptedDataKey, item.KeyVersion), SecretScopes.EmailOutbox);

        try
        {
            return JsonSerializer.Deserialize<T>(plain);
        }
        finally
        {
            Array.Clear(plain);
        }
    }

    private EmailOutboxItem Seal<T>(EmailOutboxKind kind, string language, T payload)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(payload);

        try
        {
            var sealedPayload = _secretProtector.Protect(plain, SecretScopes.EmailOutbox);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return new EmailOutboxItem
            {
                Kind = kind,
                Language = language?.Length > 16 ? language[..16] : language,
                EncryptedPayload = sealedPayload.EncryptedSecret,
                EncryptedDataKey = sealedPayload.EncryptedDataKey,
                KeyVersion = sealedPayload.KeyVersion,
                NextAttemptDate = now,
                CreationDate = now,
            };
        }
        finally
        {
            Array.Clear(plain);
        }
    }
}

/// <summary>
/// Payload of a <see cref="EmailOutboxKind.PasswordReset"/> row.
/// </summary>
/// <param name="Email">The address someone asked a reset link for.</param>
public sealed record PasswordResetPayload(string Email);
