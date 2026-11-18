using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.UpdateOrganizationSettings;

/// <summary>
/// Changes the organization's audit log retention and default language (owners). Only the fields sent are changed. The
/// package retention is changed with <c>artifacts/settings</c>.
/// </summary>
[OrgAuth(OrgPermission.ManageOrganization)]
public record UpdateOrganizationSettingsCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Days the audit log is kept: 90, 180, 365 or 730. Null keeps the current value.
    /// </summary>
    public int? AuditRetentionDays { get; set; }

    /// <summary>
    /// Default language of the store texts (BCP 47, e.g. tr-TR). Null keeps the current value; an empty string clears it.
    /// </summary>
    public string DefaultLocale { get; set; }
}
