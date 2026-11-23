using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Features.Registries.RemoveTokenReminder;

/// <summary>
/// Handles <see cref="RemoveTokenReminderCommand"/>. A reminder of another organization is reported as not found.
/// </summary>
/// <param name="ReminderRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record RemoveTokenReminderCommandHandler(IStoreasyRepositoryBase<RegistryTokenReminder> ReminderRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<RemoveTokenReminderCommand, int>
{
    private readonly IStoreasyRepositoryBase<RegistryTokenReminder> _reminderRepository = ReminderRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RemoveTokenReminderCommand request, CancellationToken cancellationToken)
    {
        var reminder = await _reminderRepository.GetFirstOrDefaultAsync(r => r.Id == request.Id && r.OrganizationId == request.OrganizationId,
                                                                        projection: r => new RegistryTokenReminder { Id = r.Id, Label = r.Label, Registry = r.Registry },
                                                                        cancellationToken: cancellationToken);

        if (reminder == null)
            return Response<int>.Error(0, MessageKey.TokenReminderNotFound);

        await _reminderRepository.ExecuteDeleteAsync(r => r.Id == reminder.Id && r.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RegistryTokenReminderRemoved, reminder.Id.ToString(CultureInfo.InvariantCulture), $"Token reminder {reminder.Label} ({reminder.Registry}) removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(reminder.Id);
    }
}
