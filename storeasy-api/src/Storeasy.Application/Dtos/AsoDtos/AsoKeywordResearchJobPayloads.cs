namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.AsoKeywordResearch"/> job.
/// </summary>
/// <param name="StoreAppId">Store record whose rank is looked for in the results.</param>
/// <param name="Country">Storefront country (lower case).</param>
/// <param name="Language">Search language.</param>
/// <param name="Keywords">Normalized keywords, 1 to 20, in the order they are measured.</param>
public sealed record AsoKeywordResearchPayload(int StoreAppId, string Country, string Language, List<string> Keywords);

/// <summary>
/// Result of a finished <see cref="StoreJobKind.AsoKeywordResearch"/> job, saved as the job's result.
/// </summary>
public sealed class AsoKeywordResearchResult
{
    /// <summary>
    /// The error of a job in which no store source answered for any keyword. The research endpoint shows it as the localized
    /// <c>AsoSourceUnavailable</c> message.
    /// </summary>
    public const string SourceUnavailableError = "The store could not be reached for keyword data.";

    /// <summary>
    /// Name of the step that measures the keywords; its progress is the number of keywords measured.
    /// </summary>
    public const string MeasureStep = "keywords.measure";

    /// <summary>
    /// The measured keywords in the order they were asked for. <see cref="AsoKeywordResearchDto.Tracked"/> is set when the
    /// result is read.
    /// </summary>
    public List<AsoKeywordResearchDto> Items { get; set; } = [];
}
