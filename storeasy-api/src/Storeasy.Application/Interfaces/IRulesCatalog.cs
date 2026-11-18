using Storeasy.Domain.Rules;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// The store rules catalog loaded at startup (docs/urun-plani.md 12): the readiness check, the compliance timeline, the
/// media slot evaluation and the listing limits read it. Registered as a singleton.
/// </summary>
public interface IRulesCatalog
{
    /// <summary>
    /// The loaded catalog.
    /// </summary>
    RuleCatalog Current { get; }
}
