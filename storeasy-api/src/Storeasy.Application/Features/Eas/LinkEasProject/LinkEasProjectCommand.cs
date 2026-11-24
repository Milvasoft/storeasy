using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.LinkEasProject;

/// <summary>
/// Links an app to an EAS project (or unlinks it with no project) and queues a read of its builds.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record LinkEasProjectCommand : ICommand<EasAppDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Expo project id; empty to unlink.
    /// </summary>
    public string ProjectId { get; set; }
}
