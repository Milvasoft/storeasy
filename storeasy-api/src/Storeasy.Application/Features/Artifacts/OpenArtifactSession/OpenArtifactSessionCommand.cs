using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Artifacts.OpenArtifactSession;

/// <summary>
/// Opens an upload session for a package, resumes the open one of the same file or records a duplicate
/// (tools/CONTRACT.md 3.2). Members need ReleaseTest; CI tokens and upload tokens may call it for their apps.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
[AllowMachineToken]
public record OpenArtifactSessionCommand : ICommand<ArtifactUploadDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App (from the route).
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// File name with its extension (.ipa, .aab, .apk, .msix, .msixbundle, .appx, .appxbundle, .msixupload, .appxupload).
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Lower case hex SHA-256 of the whole file.
    /// </summary>
    public string Sha256 { get; set; }

    /// <summary>
    /// MIME type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Where the package comes from.
    /// </summary>
    public ArtifactSource Source { get; set; } = ArtifactSource.Rest;

    /// <summary>
    /// CI details (all optional).
    /// </summary>
    public ArtifactSourceRef SourceRef { get; set; }

    /// <summary>
    /// Target for this session only (test channels only).
    /// </summary>
    public ArtifactDeliveryOverride Delivery { get; set; }
}
