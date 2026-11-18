namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// The age rating declaration of an App Store record.
/// </summary>
public class AgeRatingDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Answers.
    /// </summary>
    public List<AgeRatingAnswerDto> Answers { get; set; }

    /// <summary>
    /// Catalog rules answered outside the API (new descriptors).
    /// </summary>
    public List<string> AssistedRuleIds { get; set; }
}
