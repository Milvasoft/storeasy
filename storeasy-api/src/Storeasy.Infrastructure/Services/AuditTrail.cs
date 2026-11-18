using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using System.Text.Json;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IAuditTrail"/>.
/// </summary>
/// <param name="auditRepository"></param>
/// <param name="currentUser"></param>
/// <param name="timeProvider"></param>
public class AuditTrail(IStoreasyRepositoryBase<StoreAuditLog> auditRepository, ICurrentUser currentUser, TimeProvider timeProvider) : IAuditTrail
{
    private const string _systemUserName = "system";
    private const int _summaryMaxLength = 500;

    private readonly IStoreasyRepositoryBase<StoreAuditLog> _auditRepository = auditRepository;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public ActionChannel Channel { get; set; } = ActionChannel.Web;

    /// <inheritdoc/>
    public Task WriteAsync(int organizationId,
                           AuditAction action,
                           string targetId,
                           string summary,
                           StoreKind? store = null,
                           object details = null,
                           CancellationToken cancellationToken = default)
    {
        var isSystem = Channel == ActionChannel.System || !_currentUser.IsAuthenticated;

        var entry = new StoreAuditLog
        {
            OrganizationId = organizationId,
            UserId = isSystem ? null : _currentUser.Id,
            UserName = isSystem ? _systemUserName : _currentUser.UserName,
            Channel = Channel,
            Action = action,
            Store = store,
            TargetId = targetId,
            Summary = SecretRedactor.RedactAndTrim(summary, _summaryMaxLength),
            Details = details == null ? null : SecretRedactor.Redact(JsonSerializer.Serialize(details)),
            Date = _timeProvider.GetUtcNow().UtcDateTime,
        };

        return _auditRepository.AddAsync(entry, cancellationToken);
    }

    /// <inheritdoc/>
    public Task WriteManyAsync(IReadOnlyList<AuditEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries == null || entries.Count == 0)
            return Task.CompletedTask;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var isSystem = Channel == ActionChannel.System || !_currentUser.IsAuthenticated;

        var rows = entries.Select(e => new StoreAuditLog
        {
            OrganizationId = e.OrganizationId,
            UserId = e.UserName != null ? e.UserId : isSystem ? null : _currentUser.Id,
            UserName = e.UserName ?? (isSystem ? _systemUserName : _currentUser.UserName),
            Channel = e.Channel ?? Channel,
            Action = e.Action,
            Store = e.Store,
            TargetId = e.TargetId,
            Summary = SecretRedactor.RedactAndTrim(e.Summary, _summaryMaxLength),
            Details = e.Details == null ? null : SecretRedactor.Redact(JsonSerializer.Serialize(e.Details)),
            Date = now,
        }).ToList();

        return _auditRepository.AddRangeAsync(rows, cancellationToken);
    }
}
