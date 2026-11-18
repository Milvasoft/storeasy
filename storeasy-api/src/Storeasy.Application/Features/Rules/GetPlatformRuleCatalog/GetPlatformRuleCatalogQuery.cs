using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.Rules.GetPlatformRuleCatalog;

/// <summary>
/// Reads the loaded rules catalog without an organization (platform managers with rule catalog permission).
/// </summary>
public record GetPlatformRuleCatalogQuery : IQuery<PlatformRuleCatalogDto>
{
    /// <summary>
    /// Only the rules of this store.
    /// </summary>
    public StoreKind? Store { get; set; }
}
