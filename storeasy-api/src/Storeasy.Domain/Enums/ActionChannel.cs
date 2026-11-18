using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Where a write came from.
/// </summary>
public enum ActionChannel : byte
{
    /// <summary>
    /// The web app.
    /// </summary>
    [Description("Web")]
    Web = 1,

    /// <summary>
    /// The MCP server (a Claude session).
    /// </summary>
    [Description("MCP")]
    Mcp = 2,

    /// <summary>
    /// The REST API with a personal access token.
    /// </summary>
    [Description("API")]
    Api = 3,

    /// <summary>
    /// storeasy itself (webhook, scheduled sync).
    /// </summary>
    [Description("System")]
    System = 4,

    /// <summary>
    /// A CI pipeline or build service (package intake).
    /// </summary>
    [Description("CI")]
    Ci = 5,
}
