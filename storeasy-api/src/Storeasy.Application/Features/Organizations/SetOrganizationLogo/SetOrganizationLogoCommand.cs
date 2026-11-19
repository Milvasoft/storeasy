using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.SetOrganizationLogo;

/// <summary>
/// Makes an uploaded file (from <c>POST organizations/logo/upload</c>) the organization's logo, replacing the current one.
/// The file must really be a PNG, JPEG or WebP image of the declared type and size; it is redrawn as a square PNG of at
/// most 256 pixels and the upload itself is removed. Returns the new logo URL.
/// </summary>
[OrgAuth(OrgPermission.ManageOrganization)]
public record SetOrganizationLogoCommand : ICommand<string>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The asset id the upload call returned.
    /// </summary>
    public long AssetId { get; set; }
}
