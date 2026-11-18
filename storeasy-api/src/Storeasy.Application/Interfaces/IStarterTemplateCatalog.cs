using Storeasy.Domain.Studio;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// The built in starter templates (original storeasy designs with sample captions) that new mockup templates copy.
/// Registered as a singleton.
/// </summary>
public interface IStarterTemplateCatalog
{
    /// <summary>
    /// Every starter, in display order.
    /// </summary>
    IReadOnlyList<StarterTemplate> All { get; }

    /// <summary>
    /// The starter with <paramref name="key"/>, or null.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    StarterTemplate Find(string key);
}
