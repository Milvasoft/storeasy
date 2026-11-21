using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoKeywords.TrackAsoKeywords;

/// <summary>
/// Starts (or resumes) the daily rank tracking of keywords for one app in one store and country. A keyword tracked before
/// continues its old history. The number of active keywords per organization is limited by the server configuration
/// (409 AsoKeywordLimitReached). App Store ranks come from the iTunes Search API; Google Play ranks only when the
/// operator enabled reading Play search pages (409 AsoGooglePlayRanksDisabled otherwise); Microsoft Store is not
/// supported. The first rank is recorded by the next run of the rank job (hourly).
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record TrackAsoKeywordsCommand : ICommand<List<AsoTrackedKeywordDto>>, IOrganizationRequest, IAppStoreRequest
{
    /// <summary>
    /// Most keywords per call.
    /// </summary>
    public const int MaxKeywords = 50;

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
    /// Keywords to track, 1 to 50.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Whether competitors' ranks are recorded too.
    /// </summary>
    public bool TrackCompetitors { get; set; }

    /// <summary>
    /// Competitors to follow (App Store track ids, Play package names), at most 10. Empty with
    /// <see cref="TrackCompetitors"/> follows the top five results of the first check.
    /// </summary>
    public List<string> CompetitorIds { get; set; } = [];
}
