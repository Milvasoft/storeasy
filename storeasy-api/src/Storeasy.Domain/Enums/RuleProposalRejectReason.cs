using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Why a platform admin rejected a proposal. Values are explicit and never reused.
/// </summary>
public enum RuleProposalRejectReason : byte
{
    /// <summary>The page only changed its layout or wording.</summary>
    [Description("Formatting only")]
    FormattingOnly = 1,

    /// <summary>The change was read wrong.</summary>
    [Description("Misread")]
    Misread = 2,

    /// <summary>The source keeps changing; wait.</summary>
    [Description("Source unstable")]
    SourceUnstable = 3,

    /// <summary>Another reason (see the note).</summary>
    [Description("Other")]
    Other = 4,
}
