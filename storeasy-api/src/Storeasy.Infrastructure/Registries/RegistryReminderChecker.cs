using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Infrastructure.Registries;

/// <summary>
/// <see cref="IRegistryReminderChecker"/>: reads the reminders within 14 days of their expiry, warned before or with an
/// open alert, decides each with <see cref="TokenReminderRules.Decide"/>, saves the alerts and stages in bulk, and audits
/// and notifies every new stage (14 days, 3 days, expired), suggesting trusted publishing.
/// </summary>
/// <param name="reminderRepository"></param>
/// <param name="alertRepository"></param>
/// <param name="featureFlagService"></param>
/// <param name="auditTrail"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="timeProvider"></param>
public sealed class RegistryReminderChecker(IStoreasyRepositoryBase<RegistryTokenReminder> reminderRepository,
                                            IStoreasyRepositoryBase<PackageAlert> alertRepository,
                                            IFeatureFlagService featureFlagService,
                                            IAuditTrail auditTrail,
                                            INotificationDispatcher notificationDispatcher,
                                            TimeProvider timeProvider) : IRegistryReminderChecker
{
    private readonly IStoreasyRepositoryBase<RegistryTokenReminder> _reminderRepository = reminderRepository;
    private readonly IStoreasyRepositoryBase<PackageAlert> _alertRepository = alertRepository;
    private readonly IFeatureFlagService _featureFlagService = featureFlagService;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<int> CheckAsync(int? organizationId, CancellationToken cancellationToken)
    {
        if (organizationId == null && !await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.Registries, cancellationToken))
            return 0;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var limit = now.Date.AddDays(TokenReminderRules.FirstWarningDays);
        var openAlerts = (await _alertRepository.GetAllAsync(a => a.Kind == PackageAlertKind.TokenExpiring && a.ResolvedDate == null && (organizationId == null || a.OrganizationId == organizationId),
                                                             cancellationToken: cancellationToken))
                         .Where(a => a.TokenReminderId != null)
                         .GroupBy(a => a.TokenReminderId.Value)
                         .ToDictionary(g => g.Key, g => g.First());
        var withAlerts = openAlerts.Keys.ToList();
        var reminders = await _reminderRepository.GetAllAsync(r => (organizationId == null || r.OrganizationId == organizationId)
                                                                    && (r.ExpiryDate <= limit || r.WarnedStage > 0 || withAlerts.Contains(r.Id)),
                                                               cancellationToken: cancellationToken);

        var changed = new List<RegistryTokenReminder>();
        var opened = new List<PackageAlert>();
        var resolved = new List<PackageAlert>();
        var warned = new List<RegistryTokenReminder>();

        foreach (var reminder in reminders)
        {
            var alert = openAlerts.GetValueOrDefault(reminder.Id);
            var decision = TokenReminderRules.Decide(reminder.WarnedStage, alert != null, reminder.ExpiryDate, now);

            if (decision.Open)
                opened.Add(AlertOf(reminder, now));

            if (decision.Resolve)
            {
                alert.ResolvedDate = now;
                resolved.Add(alert);
            }

            if (decision.Warn)
                warned.Add(reminder);

            if (decision.Stage != reminder.WarnedStage)
            {
                reminder.WarnedStage = decision.Stage;
                changed.Add(reminder);
            }
        }

        if (changed.Count > 0)
            await _reminderRepository.UpdateAsync(changed, cancellationToken);

        if (opened.Count > 0)
            await _alertRepository.AddRangeAsync(opened, cancellationToken);

        if (resolved.Count > 0)
            await _alertRepository.UpdateAsync(resolved, cancellationToken);

        await ReportAsync(warned, now, cancellationToken);

        return warned.Count;
    }

    private static PackageAlert AlertOf(RegistryTokenReminder reminder, DateTime now) => new()
    {
        OrganizationId = reminder.OrganizationId,
        TokenReminderId = reminder.Id,
        Kind = PackageAlertKind.TokenExpiring,
        Summary = $"{reminder.Label} ({RegistryNames.DisplayName(reminder.Registry)}): {reminder.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
        OpenedDate = now,
    };

    private async Task ReportAsync(List<RegistryTokenReminder> warned, DateTime now, CancellationToken cancellationToken)
    {
        if (warned.Count == 0)
            return;

        _auditTrail.Channel = ActionChannel.System;

        await _auditTrail.WriteManyAsync([.. warned.Select(r => new AuditEntry(r.OrganizationId,
                                                                               AuditAction.RegistryTokenExpiring,
                                                                               r.Id.ToString(CultureInfo.InvariantCulture),
                                                                               $"Token {r.Label} ({RegistryNames.DisplayName(r.Registry)}) expires {r.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.",
                                                                               Details: new { r.Registry, r.Label, r.UsedIn, ExpiryDate = r.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Stage = r.WarnedStage },
                                                                               UserName: "system",
                                                                               Channel: ActionChannel.System))],
                                         cancellationToken);

        foreach (var reminder in warned)
        {
            var expired = TokenReminderRules.Stage(reminder.ExpiryDate, now) == TokenReminderRules.StageExpired;

            await _notificationDispatcher.DispatchAsync(new OrgNotification
            {
                OrganizationId = reminder.OrganizationId,
                Kind = NotificationType.PackageRegistry,
                Roles = NotificationAudiences.Editors,
                TextKey = expired ? MessageKey.NotificationRegistryTokenExpired : MessageKey.NotificationRegistryTokenExpiring,
                TextArgs = [reminder.Label, RegistryNames.DisplayName(reminder.Registry), reminder.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)],
                Data = new { tokenReminderId = reminder.Id, registry = reminder.Registry.ToString(), expiryDate = reminder.ExpiryDate, stage = reminder.WarnedStage },
                RelatedEntity = NotificationEntity.TrackedPackage,
                RelatedEntityId = $"reminder:{reminder.Id}:{reminder.WarnedStage}",
                ActionLink = "/registries",
            }, cancellationToken);
        }
    }
}
