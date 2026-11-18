namespace Storeasy.Domain.JsonModels;

/// <summary>
/// An App Review guideline cited by a rejection.
/// </summary>
public class RejectionGuideline
{
    /// <summary>
    /// Guideline number as cited (e.g. <c>5.1.1</c>, <c>2.3.10</c>).
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Title next to the number in the text (e.g. <c>Legal - Privacy - Data Collection and Storage</c>), if any.
    /// </summary>
    public string Title { get; set; }
}
