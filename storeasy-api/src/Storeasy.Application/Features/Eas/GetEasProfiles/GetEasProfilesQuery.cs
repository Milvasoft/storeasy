using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasProfiles;

/// <summary>
/// The build profiles to start a build with: from eas.json in the project's GitHub repository when the organization's GitHub token can read it, else the defaults.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetEasProfilesQuery : IQuery<EasProfilesDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Branch, tag or commit to read eas.json at; the default branch when empty.
    /// </summary>
    public string GitRef { get; set; }
}
