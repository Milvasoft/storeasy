namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// One tracked keyword's coverage in a listing language.
/// </summary>
public class AsoKeywordCoverageItemDto
{
    /// <summary>
    /// Keyword.
    /// </summary>
    public string Keyword { get; set; }

    /// <summary>
    /// Whether every word is in the name, subtitle or keywords field.
    /// </summary>
    public bool Covered { get; set; }

    /// <summary>
    /// Words found in none of them.
    /// </summary>
    public List<string> MissingWords { get; set; } = [];

    /// <summary>
    /// Fields holding at least one of its words (<c>name</c>, <c>subtitle</c>, <c>keywords</c>).
    /// </summary>
    public List<string> Fields { get; set; } = [];
}
