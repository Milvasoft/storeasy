using System.Text.Json;

namespace Storeasy.Application.Dtos.AssistedStepDtos;

/// <summary>
/// Structured hand-off of an assisted step to a browser agent working in the member's own store session. It carries no password; the agent must never type one.
/// </summary>
public class AssistedStepHandOffDto
{
    /// <summary>
    /// Step id.
    /// </summary>
    public int StepId { get; set; }

    /// <summary>
    /// Definition key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// App name.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// The store's id of the app.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Store page to open.
    /// </summary>
    public string StoreUrl { get; set; }

    /// <summary>
    /// Checklist.
    /// </summary>
    public List<AssistedStepItemDto> Steps { get; set; }

    /// <summary>
    /// Known traps.
    /// </summary>
    public List<string> Pitfalls { get; set; }

    /// <summary>
    /// Prepared answers by kind (no secrets).
    /// </summary>
    public Dictionary<string, JsonElement> Answers { get; set; }

    /// <summary>
    /// Instruction for the agent, in the language of the request.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// MCP tool the agent records the result with.
    /// </summary>
    public string McpTool { get; set; }
}
