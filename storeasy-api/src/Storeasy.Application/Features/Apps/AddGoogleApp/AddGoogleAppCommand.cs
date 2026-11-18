using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AppDtos;

namespace Storeasy.Application.Features.Apps.AddGoogleApp;

/// <summary>
/// Adds a Google Play app by its package name (Play has no API that lists apps). The record is linked to the storeasy app
/// whose other store record has the same identifier (iOS bundle id equal to the package name), otherwise a new app is
/// created. A sync job then proves the service account can open the app (an edit probe), reads its bundles and tracks,
/// and removes the record if Play refuses it. The store secret is opened only inside that job.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record AddGoogleAppCommand : ICommand<AppAddedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Google Play connection to read the app with.
    /// </summary>
    public int ConnectionId { get; set; }

    /// <summary>
    /// Package name (com.example.app).
    /// </summary>
    public string PackageName { get; set; }
}
