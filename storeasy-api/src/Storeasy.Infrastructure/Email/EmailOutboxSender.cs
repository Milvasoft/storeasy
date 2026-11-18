using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Npgsql;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Persistence.Context;
using System.Globalization;

namespace Storeasy.Infrastructure.Email;

/// <summary>
/// Claims and sends one outbox e-mail (scoped; <see cref="EmailOutboxRunner"/> creates a scope per e-mail). The claim is
/// a single <c>UPDATE ... WHERE Id = (SELECT ... FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING</c>, so concurrent senders and
/// instances never get the same row. A sent row (or a password reset request for an address no account uses) is deleted;
/// a failure is retried with a doubling delay until <see cref="EmailOutboxOptions.MaxAttempts"/>, then the row is deleted
/// and the failure logged. The mail is rendered in the culture it was queued in.
/// </summary>
/// <param name="dbContext"></param>
/// <param name="outboxRepository"></param>
/// <param name="secretProtector"></param>
/// <param name="emailSender"></param>
/// <param name="accountMailer"></param>
/// <param name="emailOptions"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public sealed class EmailOutboxSender(StoreasyDbContext dbContext,
                                      IStoreasyRepositoryBase<EmailOutboxItem> outboxRepository,
                                      ISecretProtector secretProtector,
                                      IEmailSender emailSender,
                                      IAccountMailer accountMailer,
                                      EmailOptions emailOptions,
                                      TimeProvider timeProvider,
                                      IMilvaLogger logger)
{
    private static readonly string _claimSql = $"""
                                                UPDATE "{TableNames.EmailOutbox}" AS o
                                                SET "Attempt" = o."Attempt" + 1,
                                                    "LockedUntil" = @lease
                                                WHERE o."Id" = (
                                                    SELECT c."Id" FROM "{TableNames.EmailOutbox}" AS c
                                                    WHERE c."NextAttemptDate" <= @now AND (c."LockedUntil" IS NULL OR c."LockedUntil" < @now)
                                                    ORDER BY c."NextAttemptDate", c."Id"
                                                    LIMIT 1
                                                    FOR UPDATE SKIP LOCKED)
                                                RETURNING o."Id" AS "Value"
                                                """;

    private static readonly TimeSpan _maxRetryDelay = TimeSpan.FromHours(1);

    private readonly StoreasyDbContext _dbContext = dbContext;
    private readonly IStoreasyRepositoryBase<EmailOutboxItem> _outboxRepository = outboxRepository;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly IAccountMailer _accountMailer = accountMailer;
    private readonly EmailOutboxOptions _options = emailOptions.Outbox ?? new EmailOutboxOptions();
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IMilvaLogger _logger = logger;

    /// <summary>
    /// Claims the next due e-mail and makes one attempt.
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns>Whether an e-mail was claimed.</returns>
    public async Task<bool> SendNextAsync(CancellationToken stoppingToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var claimed = await _dbContext.Database.SqlQueryRaw<long>(_claimSql,
                                                                  new NpgsqlParameter("now", now),
                                                                  new NpgsqlParameter("lease", now.AddSeconds(Math.Max(10, _options.LeaseSeconds))))
                                               .ToListAsync(stoppingToken);

        if (claimed.Count == 0)
            return false;

        var item = await _outboxRepository.GetFirstOrDefaultAsync(o => o.Id == claimed[0],
                                                                  projection: o => new EmailOutboxItem
                                                                  {
                                                                      Id = o.Id,
                                                                      Kind = o.Kind,
                                                                      Language = o.Language,
                                                                      EncryptedPayload = o.EncryptedPayload,
                                                                      EncryptedDataKey = o.EncryptedDataKey,
                                                                      KeyVersion = o.KeyVersion,
                                                                      Attempt = o.Attempt,
                                                                  },
                                                                  cancellationToken: CancellationToken.None);

        if (item != null)
            await SendAsync(item, stoppingToken);

        return true;
    }

    private async Task SendAsync(EmailOutboxItem item, CancellationToken stoppingToken)
    {
        var previousCulture = CultureInfo.CurrentUICulture;

        try
        {
            if (!string.IsNullOrWhiteSpace(item.Language))
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(item.Language);

            var message = item.Kind == EmailOutboxKind.PasswordReset
                ? await _accountMailer.CreatePasswordResetAsync(EmailOutbox.Open<PasswordResetPayload>(_secretProtector, item)?.Email, stoppingToken)
                : EmailOutbox.Open<EmailMessage>(_secretProtector, item);

            if (message != null)
                await _emailSender.SendAsync(message, stoppingToken);

            await _outboxRepository.ExecuteDeleteAsync(o => o.Id == item.Id, cancellationToken: CancellationToken.None);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown: give the attempt back.
            await RescheduleAsync(item, item.Attempt - 1, TimeSpan.Zero, null);
        }
        catch (Exception ex)
        {
            await FailAsync(item, SecretRedactor.RedactAndTrim(ex.Message, EmailOutboxItem.ErrorMaxLength));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    private async Task FailAsync(EmailOutboxItem item, string error)
    {
        if (item.Attempt >= Math.Max(1, _options.MaxAttempts))
        {
            _logger.Error("Outbox e-mail {Id} ({Kind}) was dropped after {Attempt} attempts: {Error}", item.Id, item.Kind, item.Attempt, error);

            await _outboxRepository.ExecuteDeleteAsync(o => o.Id == item.Id, cancellationToken: CancellationToken.None);

            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(0, _options.RetryBaseDelaySeconds) * Math.Pow(2, item.Attempt - 1));

        _logger.Warning("Outbox e-mail {Id} ({Kind}) attempt {Attempt} failed: {Error}", item.Id, item.Kind, item.Attempt, error);

        await RescheduleAsync(item, item.Attempt, delay > _maxRetryDelay ? _maxRetryDelay : delay, error);
    }

    private Task RescheduleAsync(EmailOutboxItem item, int attempt, TimeSpan delay, string error)
        => _outboxRepository.ExecuteUpdateAsync(item.Id,
                                                new SetPropertyBuilder<EmailOutboxItem>().SetPropertyValue(o => o.Attempt, attempt)
                                                                                         .SetPropertyValue(o => o.NextAttemptDate, _timeProvider.GetUtcNow().UtcDateTime.Add(delay))
                                                                                         .SetPropertyValue(o => o.LockedUntil, (DateTime?)null)
                                                                                         .SetPropertyValue(o => o.LastError, error),
                                                CancellationToken.None);
}
