namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// Keyword candidates for research, each with where it came from. Candidates are not measured: research measures them.
/// </summary>
public class AsoKeywordSeedsDto
{
    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Country (lower case).
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Listing language the listing seeds were read from, when the listing was read.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Candidates, most useful first.
    /// </summary>
    public List<AsoKeywordSeedDto> Seeds { get; set; } = [];

    /// <summary>
    /// Message of a part that could not be read (e.g. the listing), when one failed; the other seeds are still given.
    /// </summary>
    public string Warning { get; set; }

    /// <summary>
    /// Model that proposed the AI seeds, for an AI suggestion.
    /// </summary>
    public string Model { get; set; }
}
