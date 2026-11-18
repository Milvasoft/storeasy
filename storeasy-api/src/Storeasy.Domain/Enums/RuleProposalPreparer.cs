using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Who prepared a catalog change proposal. Values are explicit and never reused.
/// </summary>
public enum RuleProposalPreparer : byte
{
    /// <summary>The rule watcher (source change only).</summary>
    [Description("Watcher")]
    Watcher = 1,

    /// <summary>Claude through MCP (catalog patch written).</summary>
    [Description("Claude (MCP)")]
    Mcp = 2,
}
