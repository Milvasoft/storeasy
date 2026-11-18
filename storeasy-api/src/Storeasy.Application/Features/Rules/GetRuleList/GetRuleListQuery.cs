using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.Rules.GetRuleList;

/// <summary>
/// Lists the catalog rules (deprecated ones left out), with the title in the language of the request and how many of the organization's apps each applies to (or may apply to). Sorted by due date.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetRuleListQuery : IQuery<List<RuleListDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only rules of this store.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// Only rules of this category (screenshots, age-rating...).
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Only rules in this state today.
    /// </summary>
    public RuleStatus? Status { get; set; }

    /// <summary>
    /// Text the id or the title contains.
    /// </summary>
    public string Search { get; set; }
}
