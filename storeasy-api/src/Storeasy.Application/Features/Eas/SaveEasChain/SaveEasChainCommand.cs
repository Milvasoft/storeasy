using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.SaveEasChain;

/// <summary>
/// Saves the app's defaults of what happens after a build finished: nothing, send to intake or EAS Submit (with the Play track and release status), and whether the review or production plan is prepared (never approved) once the build reached its test channel.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SaveEasChainCommand : ICommand<EasAppDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// The defaults.
    /// </summary>
    public EasChainDto Chain { get; set; }
}
