using Storeasy.Domain.Rules;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// A watched official source.
/// </summary>
public class RuleSourceDto
{
    /// <summary>Source id.</summary>
    public int Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; }

    /// <summary>Store.</summary>
    public StoreKind Store { get; set; }

    /// <summary>Address.</summary>
    public string Url { get; set; }

    /// <summary>Whether it is read.</summary>
    public bool IsActive { get; set; }

    /// <summary>State of the last read.</summary>
    public RuleSourceStatus Status { get; set; }

    /// <summary>UTC instant of the last read.</summary>
    public DateTime? LastFetchDate { get; set; }

    /// <summary>UTC instant the content last changed.</summary>
    public DateTime? LastChangeDate { get; set; }

    /// <summary>Hash of the current content.</summary>
    public string ContentHash { get; set; }

    /// <summary>Hash before the last change.</summary>
    public string PreviousContentHash { get; set; }

    /// <summary>Reason of the last failed read.</summary>
    public string LastError { get; set; }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<RuleWatchSource, RuleSourceDto>> Projection { get; } = s => new RuleSourceDto
    {
        Id = s.Id,
        Name = s.Name,
        Store = s.Store,
        Url = s.Url,
        IsActive = s.IsActive,
        Status = s.Status,
        LastFetchDate = s.LastFetchDate,
        LastChangeDate = s.LastChangeDate,
        ContentHash = s.ContentHash,
        PreviousContentHash = s.PreviousContentHash,
        LastError = s.LastError,
    };
}
