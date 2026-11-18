using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.MsStore.CheckMsInstaller;

/// <summary>
/// Queues the Authenticode check of an MSI/EXE product's installers: the job downloads each installer from its own
/// address (public HTTPS hosts only) and records whether it carries a signature. Submitting needs every installer signed.
/// Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record CheckMsInstallerCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Microsoft Store record (an MSI/EXE product).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Installer addresses to check; empty for every installer of the product.
    /// </summary>
    public List<string> PackageUrls { get; set; } = [];
}
