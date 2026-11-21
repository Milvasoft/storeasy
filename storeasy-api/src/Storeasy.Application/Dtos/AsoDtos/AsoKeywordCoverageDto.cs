namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// Apple's keywords field helper for one listing language: which tracked keywords the name, subtitle and keywords field
/// cover, what is missing, what is spent twice and how many of the 100 characters are left.
/// </summary>
public class AsoKeywordCoverageDto
{
    /// <summary>
    /// Listing locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// App name of the locale.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Subtitle of the locale.
    /// </summary>
    public string Subtitle { get; set; }

    /// <summary>
    /// Keywords field of the locale.
    /// </summary>
    public string Keywords { get; set; }

    /// <summary>
    /// Characters used in the keywords field.
    /// </summary>
    public int KeywordsLength { get; set; }

    /// <summary>
    /// Characters left of the 100 (negative when over).
    /// </summary>
    public int Remaining { get; set; }

    /// <summary>
    /// Characters the missing words need, commas included.
    /// </summary>
    public int CharactersNeeded { get; set; }

    /// <summary>
    /// Coverage of each tracked keyword of the app (every country).
    /// </summary>
    public List<AsoKeywordCoverageItemDto> Items { get; set; } = [];

    /// <summary>
    /// Keywords field words already in the name or subtitle, or repeated.
    /// </summary>
    public List<string> RedundantWords { get; set; } = [];

    /// <summary>
    /// Words missing from every field, once.
    /// </summary>
    public List<string> MissingWords { get; set; } = [];
}
