using Storeasy.Domain.Studio;

namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Everything a render of a mockup template needs, read in one query: design, locales, slots and captions.
/// </summary>
public sealed class MockupTemplateSnapshot
{
    /// <summary>
    /// Template id.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// App of the template.
    /// </summary>
    public int AppId { get; init; }

    /// <summary>
    /// Template name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Primary locale.
    /// </summary>
    public string PrimaryLocale { get; init; }

    /// <summary>
    /// Locales.
    /// </summary>
    public List<string> Locales { get; init; }

    /// <summary>
    /// Number of slots.
    /// </summary>
    public int SlotCount { get; init; }

    /// <summary>
    /// Concurrency version.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// The design as stored.
    /// </summary>
    public MockupSpec Spec { get; init; }

    /// <summary>
    /// Raw screenshots of the slots.
    /// </summary>
    public List<MockupSlot> Slots { get; init; }

    /// <summary>
    /// Captions.
    /// </summary>
    public List<MockupCaption> Captions { get; init; }

    /// <summary>
    /// Asset ids the template draws (slot screenshots and the background image).
    /// </summary>
    /// <param name="spec">The design to draw (the stored one or an unsaved one).</param>
    /// <returns></returns>
    public List<long> AssetIds(MockupSpec spec)
    {
        var ids = Slots.Select(s => s.AssetId).ToList();

        if (spec?.Background?.Kind == MockupSpecRules.BackgroundKinds.Image && spec.Background.AssetId is long backgroundId)
            ids.Add(backgroundId);

        return [.. ids.Distinct()];
    }
}
