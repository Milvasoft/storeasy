using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordSeeds;

/// <summary>
/// Keyword candidates for research, from data only: the app's own listing in the store (name, subtitle, App Store keywords
/// field, the description's most used words and pairs, read live), words the top competing apps' names share, and the
/// store's search suggestions for the first candidates. Candidates are not measured; pass them to research.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoKeywordSeedsQuery : IQuery<AsoKeywordSeedsDto>, IOrganizationRequest, IAppStoreRequest
{
    /// <summary>
    /// Most candidates returned.
    /// </summary>
    public const int MaxSeeds = 60;

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
    /// Storefront country, ISO 3166-1 alpha-2.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Listing locale to read (e.g. tr, en-US); a locale in the country's language, else the primary one, when empty.
    /// </summary>
    public string Locale { get; set; }
}
