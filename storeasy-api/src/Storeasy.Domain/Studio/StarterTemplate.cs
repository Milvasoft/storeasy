using Storeasy.Domain.JsonModels;

namespace Storeasy.Domain.Studio;

/// <summary>
/// A built in starting point for a new mockup template: an original storeasy design with sample captions. Starters are
/// data that ships with the application (seeded from embedded JSON), not rows; a template copies one when it is created.
/// </summary>
public sealed class StarterTemplate
{
    /// <summary>
    /// Stable key.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// English name.
    /// </summary>
    public string NameEn { get; init; }

    /// <summary>
    /// Turkish name.
    /// </summary>
    public string NameTr { get; init; }

    /// <summary>
    /// English description.
    /// </summary>
    public string DescriptionEn { get; init; }

    /// <summary>
    /// Turkish description.
    /// </summary>
    public string DescriptionTr { get; init; }

    /// <summary>
    /// Number of slots.
    /// </summary>
    public int SlotCount { get; init; }

    /// <summary>
    /// The design.
    /// </summary>
    public MockupSpec Spec { get; init; }

    /// <summary>
    /// Sample captions by language (<c>en</c>, <c>tr</c>), in slot order. A template locale gets the samples of its
    /// language (<c>en-US</c> takes <c>en</c>).
    /// </summary>
    public Dictionary<string, List<StarterCaption>> Captions { get; init; } = [];
}

/// <summary>
/// A sample caption of a starter.
/// </summary>
public sealed class StarterCaption
{
    /// <summary>
    /// Title.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Subtitle.
    /// </summary>
    public string Subtitle { get; init; }
}
