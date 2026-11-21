using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoKeywords.SuggestAsoKeywordSeeds;

/// <summary>
/// Asks the organization's AI (the key and model of the reply drafts) for keyword ideas grouped by theme, from the listing
/// texts of the country's language, the catalog profile and the candidates given. The ideas are candidates only: no score
/// comes from the model; research measures them. Nothing is saved; the audit entry keeps the model, tokens and cost.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record SuggestAsoKeywordSeedsCommand : ICommand<AsoKeywordSeedsDto>, IOrganizationRequest, IAppStoreRequest
{
    /// <summary>
    /// Maximum length of <see cref="Instruction"/>.
    /// </summary>
    public const int InstructionMaxLength = 500;

    /// <summary>
    /// Most candidates sent along.
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
    /// Listing locale to read; a locale in the country's language, else the primary one, when empty.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Candidates already found (from the seeds read or typed by the member), so the model adds new ones.
    /// </summary>
    public List<string> Seeds { get; set; } = [];

    /// <summary>
    /// Optional note for the model, at most 500 characters.
    /// </summary>
    public string Instruction { get; set; }
}
