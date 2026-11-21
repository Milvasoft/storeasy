using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.AsoKeywords.ResearchAsoKeywords;

/// <summary>
/// Queues the measurement of keywords for one app in one store and country from the store's public data (a
/// <see cref="StoreJobKind.AsoKeywordResearch"/> job) and returns the job id at once: estimated popularity (the store's
/// search autocomplete), difficulty (the strength of the first ten results), the number of competing apps, the top ten
/// competitors, whether the app itself ranks, and an opportunity score. Store requests are paced, so measuring 20 new
/// keywords takes a few minutes; read the progress and the result with <c>GET aso-keywords/research</c>. The same request
/// while an identical job is still queued or running answers that job's id. Scores are estimates, never AI guesses. App
/// Store and Google Play (competitors and rank on Play only when the operator enabled Play search pages); Microsoft Store
/// is not supported. The public measurements are cached for every organization.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record ResearchAsoKeywordsCommand : ICommand<long>, IOrganizationRequest, IAppStoreRequest
{
    /// <summary>
    /// Most keywords per call.
    /// </summary>
    public const int MaxKeywords = 20;

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
    /// Storefront country, ISO 3166-1 alpha-2 (e.g. TR, US).
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Search language (e.g. tr, en); the country's usual language when empty.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Keywords to measure, 1 to 20.
    /// </summary>
    public List<string> Keywords { get; set; } = [];
}
