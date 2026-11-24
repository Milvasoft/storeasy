using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.SendEasBuildToIntake;

/// <summary>
/// Hands a finished build's package (IPA, AAB, APK) to the build intake; the app's delivery rule then takes it to a test channel as for any upload.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record SendEasBuildToIntakeCommand : ICommand<EasBuildDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy build id.
    /// </summary>
    public long Id { get; set; }
}
