using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a catalog change proposal. Values are explicit and never reused.
/// </summary>
public enum RuleProposalStatus : byte
{
    /// <summary>Waiting for a platform admin.</summary>
    [Description("Pending")]
    Pending = 1,

    /// <summary>Approved.</summary>
    [Description("Approved")]
    Approved = 2,

    /// <summary>Rejected.</summary>
    [Description("Rejected")]
    Rejected = 3,
}
