using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.IntakeSettings.GetIntakeSettings;

/// <summary>
/// Reads the package intake settings of the organization (retention, size limit).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetIntakeSettingsQuery : IQuery<IntakeSettingsDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
