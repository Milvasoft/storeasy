using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoTrackedKeywords;

/// <summary>
/// The tracked keywords of one app in one store with the current rank, the change over 1, 7 and 30 days, the last 30 days
/// (sparkline) and the latest popularity and difficulty estimates.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoTrackedKeywordsQuery : IQuery<List<AsoTrackedKeywordDto>>, IOrganizationRequest, IAppStoreRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Only this country (ISO 3166-1 alpha-2); every country when empty.
    /// </summary>
    public string Country { get; set; }
}
