using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasApp;

/// <summary>
/// An app's EAS project link and its recent builds (started in storeasy or elsewhere).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetEasAppQuery : IQuery<EasAppDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }
}
