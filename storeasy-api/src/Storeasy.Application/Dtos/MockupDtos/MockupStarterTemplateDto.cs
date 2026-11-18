namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// A built in starter design.
/// </summary>
public class MockupStarterTemplateDto
{
    /// <summary>
    /// Stable key (send it as <c>StarterKey</c> to create a template from it).
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// English name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Turkish name.
    /// </summary>
    public string NameTr { get; set; }

    /// <summary>
    /// English description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Turkish description.
    /// </summary>
    public string DescriptionTr { get; set; }

    /// <summary>
    /// Number of slots.
    /// </summary>
    public int SlotCount { get; set; }

    /// <summary>
    /// Languages with sample captions.
    /// </summary>
    public List<string> SampleLanguages { get; set; }

    /// <summary>
    /// The design.
    /// </summary>
    public MockupSpec Spec { get; set; }
}
