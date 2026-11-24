using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.StartEasBuild;

/// <summary>
/// Asks Expo to build the app from the GitHub repository linked to its EAS project at a git ref, one build per platform. Needs the typed confirmation (the app name and iOS, Android or all). Uses the account's EAS build quota; never automatic.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record StartEasBuildCommand : ICommand<List<EasBuildDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// iOS, Android or both.
    /// </summary>
    public EasPlatform Platform { get; set; }

    /// <summary>
    /// Build profile of eas.json.
    /// </summary>
    public string Profile { get; set; }

    /// <summary>
    /// Branch, tag or commit.
    /// </summary>
    public string GitRef { get; set; }

    /// <summary>
    /// What happens after the build finished; empty for the app's defaults.
    /// </summary>
    public EasChainDto Chain { get; set; }

    /// <summary>
    /// Typed confirmation.
    /// </summary>
    public string Confirm { get; set; }
}
