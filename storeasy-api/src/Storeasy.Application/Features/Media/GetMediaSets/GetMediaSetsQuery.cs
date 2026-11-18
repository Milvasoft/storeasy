using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Media.GetMediaSets;

/// <summary>
/// Reads the screenshot (or preview) classes of one locale of a store record live, with the catalog requirement of each class: required, filled by its own items, filled by a fallback class, or missing (iPad classes only when the app supports iPad).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetMediaSetsQuery : IQuery<MediaSetListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Locale; empty for the primary one.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Screenshots (and Play graphics) or previews.
    /// </summary>
    public MediaKind Kind { get; set; } = MediaKind.Screenshot;
}
