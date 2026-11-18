using Storeasy.Domain.Rules;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// A catalog proposal in the list.
/// </summary>
public class RuleProposalListDto
{
    /// <summary>Proposal id.</summary>
    public int Id { get; set; }

    /// <summary>Title.</summary>
    public string Title { get; set; }

    /// <summary>Store.</summary>
    public StoreKind Store { get; set; }

    /// <summary>Rules it changes.</summary>
    public List<string> RuleIds { get; set; }

    /// <summary>Source that changed.</summary>
    public int? RuleSourceId { get; set; }

    /// <summary>State.</summary>
    public RuleProposalStatus Status { get; set; }

    /// <summary>Who prepared it.</summary>
    public RuleProposalPreparer PreparedBy { get; set; }

    /// <summary>User whose token prepared the patch.</summary>
    public string PreparedByUserName { get; set; }

    /// <summary>Catalog version when opened.</summary>
    public string CatalogVersionFrom { get; set; }

    /// <summary>Catalog version published by the approval.</summary>
    public string CatalogVersionTo { get; set; }

    /// <summary>UTC instant opened.</summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<RuleProposal, RuleProposalListDto>> Projection { get; } = p => new RuleProposalListDto
    {
        Id = p.Id,
        Title = p.Title,
        Store = p.Store,
        RuleIds = p.RuleIds,
        RuleSourceId = p.RuleSourceId,
        Status = p.Status,
        PreparedBy = p.PreparedBy,
        PreparedByUserName = p.PreparedByUserName,
        CatalogVersionFrom = p.CatalogVersionFrom,
        CatalogVersionTo = p.CatalogVersionTo,
        CreationDate = p.CreationDate,
    };
}
