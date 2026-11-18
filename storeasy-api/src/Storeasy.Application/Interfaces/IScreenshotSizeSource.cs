using Storeasy.Domain.Studio;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// The screenshot classes and sizes the studio exports to, read from the store rules catalog (rules/*.json). The file
/// based implementation reads the catalog files itself; once the rules catalog service of the readiness check is
/// available, an implementation over it replaces this one without touching the studio. Registered as a singleton.
/// </summary>
public interface IScreenshotSizeSource
{
    /// <summary>
    /// The loaded classes. Throws when the catalog cannot be found or does not hold a class the studio needs.
    /// </summary>
    ScreenshotSizeCatalog Current { get; }
}
