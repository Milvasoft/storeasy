namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// A keyword candidate.
/// </summary>
public class AsoKeywordSeedDto
{
    /// <summary>
    /// Normalized term.
    /// </summary>
    public string Term { get; set; }

    /// <summary>
    /// Where it came from: <c>name</c>, <c>subtitle</c>, <c>keywords</c>, <c>description</c>, <c>competitors</c>,
    /// <c>suggestions</c> or <c>ai</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Group the AI put it in (AI suggestions only).
    /// </summary>
    public string Cluster { get; set; }

    /// <summary>
    /// Whether it is already tracked for this store record and country.
    /// </summary>
    public bool Tracked { get; set; }
}
