using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Features.Registries.SaveTokenReminder;

/// <summary>
/// Handles <see cref="SaveTokenReminderCommand"/>. A reminder or package of another organization is reported as not found.
/// The organization's reminders are checked at once, so a token that already expires soon is warned about right away.
/// </summary>
/// <param name="ReminderRepository"></param>
/// <param name="PackageRepository"></param>
/// <param name="ReminderChecker"></param>
/// <param name="AuditTrail"></param>
[Log]
public record SaveTokenReminderCommandHandler(IStoreasyRepositoryBase<RegistryTokenReminder> ReminderRepository,
                                              IStoreasyRepositoryBase<TrackedPackage> PackageRepository,
                                              IRegistryReminderChecker ReminderChecker,
                                              IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<SaveTokenReminderCommand, int>
{
    private readonly IStoreasyRepositoryBase<RegistryTokenReminder> _reminderRepository = ReminderRepository;
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = PackageRepository;
    private readonly IRegistryReminderChecker _reminderChecker = ReminderChecker;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(SaveTokenReminderCommand request, CancellationToken cancellationToken)
    {
        if (request.TrackedPackageId is int packageId)
        {
            var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == packageId && p.OrganizationId == request.OrganizationId,
                                                                          projection: p => new TrackedPackage { Id = p.Id },
                                                                          cancellationToken: cancellationToken);

            if (package == null)
                return Response<int>.Error(0, MessageKey.TrackedPackageNotFound);
        }

        var reminder = request.Id is int id
            ? await _reminderRepository.GetFirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken)
            : new RegistryTokenReminder { OrganizationId = request.OrganizationId };

        if (reminder == null)
            return Response<int>.Error(0, MessageKey.TokenReminderNotFound);

        var expiry = DateTime.SpecifyKind(request.ExpiryDate.Date, DateTimeKind.Utc);

        if (reminder.ExpiryDate != expiry)
            reminder.WarnedStage = 0;

        reminder.Registry = request.Registry;
        reminder.Label = request.Label.Trim();
        reminder.UsedIn = string.IsNullOrWhiteSpace(request.UsedIn) ? null : request.UsedIn.Trim();
        reminder.ExpiryDate = expiry;
        reminder.TrackedPackageId = request.TrackedPackageId;

        if (request.Id == null)
            await _reminderRepository.AddAsync(reminder, cancellationToken);
        else
            await _reminderRepository.UpdateAsync(reminder, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.RegistryTokenReminderSaved,
                                     reminder.Id.ToString(CultureInfo.InvariantCulture),
                                     $"Token reminder {reminder.Label} ({reminder.Registry}) expires {reminder.ExpiryDate:yyyy-MM-dd}.",
                                     details: new { reminder.Registry, reminder.Label, reminder.UsedIn, ExpiryDate = reminder.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                                     cancellationToken: cancellationToken);

        await _reminderChecker.CheckAsync(request.OrganizationId, cancellationToken);

        return Response<int>.Success(reminder.Id);
    }
}
