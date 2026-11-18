using System.Text.Json.Nodes;

namespace Storeasy.Api.Mcp;

/// <summary>
/// What a remote MCP tool does to the stores.
/// </summary>
public enum McpToolKind
{
    /// <summary>
    /// Reads only.
    /// </summary>
    Read = 1,

    /// <summary>
    /// Makes a change plan (always a dry run); nothing reaches a store until <c>plans_approve</c>.
    /// </summary>
    Plan = 2,

    /// <summary>
    /// Writes storeasy data (saved answers, assisted steps, delivery of a held package, a plan decision).
    /// </summary>
    Write = 3,
}

/// <summary>
/// A file argument of a tool, advertised with the <c>x-storeasy-upload</c> JSON Schema keyword (tools/CONTRACT.md 6.1).
/// </summary>
/// <param name="Kind"><c>file</c>, <c>files</c> or <c>folder</c>.</param>
/// <param name="Purpose">Asset purpose (1 Screenshot, 2 Graphic, 3 MockupOutput, 4 BuildArtifact).</param>
/// <param name="Accept">Lower case extensions.</param>
public sealed record McpUploadSpec(string Kind, int Purpose, params string[] Accept);

/// <summary>
/// A remote MCP tool. Most tools are one MediatR request (<see cref="RequestType"/>): the arguments are the request's
/// properties except the hidden ones, so organization authorization, validation and audit run exactly like the REST
/// endpoint. A plan tool always sends <c>DryRun = true</c>. A tool with <see cref="Handler"/> composes requests itself.
/// </summary>
public sealed class McpToolDefinition
{
    /// <summary>
    /// Tool name (snake_case).
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// English description for the model.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// What the tool does.
    /// </summary>
    public McpToolKind Kind { get; init; }

    /// <summary>
    /// MediatR request sent by a plain tool.
    /// </summary>
    public Type RequestType { get; init; }

    /// <summary>
    /// Request properties that are not arguments (the organization always comes from the token).
    /// </summary>
    public IReadOnlyList<string> Hidden { get; init; } = [];

    /// <summary>
    /// Values a plain tool always sends (property name to value).
    /// </summary>
    public IReadOnlyDictionary<string, object> Fixed { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// File arguments by property name.
    /// </summary>
    public IReadOnlyDictionary<string, McpUploadSpec> Uploads { get; init; } = new Dictionary<string, McpUploadSpec>();

    /// <summary>
    /// Input schema of a tool with its own <see cref="Handler"/>.
    /// </summary>
    public JsonObject Schema { get; init; }

    /// <summary>
    /// Own implementation: arguments, request services, cancellation; returns the result envelope.
    /// </summary>
    public Func<JsonObject, IServiceProvider, CancellationToken, Task<object>> Handler { get; init; }
}
