using MediatR;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Core.Exceptions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Api.Mcp;

/// <summary>
/// The list and call handlers of the remote MCP. A call runs in the HTTP request of <c>/mcp</c>: the request's services
/// (current user from the personal access token, organization access, audit trail with the MCP channel) handle the
/// MediatR request, so the tool is checked and audited exactly like the REST endpoint. Results are the response envelope as
/// JSON text; a failed envelope or a refused request is a tool error.
/// </summary>
public static class McpToolInvoker
{
    private static readonly Lazy<List<Tool>> _tools = new(() => [.. McpToolCatalog.Tools.Select(ToProtocolTool)]);

    /// <summary>
    /// The tools as the protocol lists them.
    /// </summary>
    public static IReadOnlyList<Tool> ProtocolTools => _tools.Value;

    /// <summary>
    /// Handles <c>tools/list</c>.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> context, CancellationToken cancellationToken)
        => ValueTask.FromResult(new ListToolsResult { Tools = [.. ProtocolTools] });

    /// <summary>
    /// Handles <c>tools/call</c>.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async ValueTask<CallToolResult> CallToolAsync(RequestContext<CallToolRequestParams> context, CancellationToken cancellationToken)
    {
        var services = context.Services.GetService<IHttpContextAccessor>()?.HttpContext?.RequestServices ?? context.Services;
        var tool = McpToolCatalog.Find(context.Params?.Name);

        if (tool == null)
            return Error($"Unknown tool: {context.Params?.Name}");

        var auditTrail = services.GetService<IAuditTrail>();

        if (auditTrail != null)
            auditTrail.Channel = ActionChannel.Mcp;

        var arguments = new JsonObject();

        foreach (var (key, value) in context.Params?.Arguments ?? new Dictionary<string, JsonElement>())
        {
            arguments[key] = JsonNode.Parse(value.GetRawText());
        }

        try
        {
            var response = tool.Handler != null
                ? await tool.Handler(arguments, services, cancellationToken)
                : await SendAsync(services, CreateRequest(tool, arguments), cancellationToken);

            return Result(response);
        }
        catch (JsonException ex)
        {
            return Error($"The arguments do not match the tool's schema: {SecretRedactor.Redact(ex.Message)}");
        }
        catch (MilvaUserFriendlyException ex)
        {
            var key = ex.Message?.Trim('~') is { Length: > 0 } message ? message : MessageKey.Forbidden;
            var localizer = services.GetService<IMilvaLocalizer>();

            return Error($"{key}: {localizer?[key] ?? key}");
        }
    }

    /// <summary>
    /// Builds the MediatR request of a plain tool from its arguments, with the tool's fixed values.
    /// </summary>
    /// <param name="tool"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static object CreateRequest(McpToolDefinition tool, JsonObject arguments)
    {
        foreach (var hidden in tool.Hidden.Concat(tool.Fixed.Keys).Append("OrganizationId"))
        {
            arguments.Remove(JsonNamingPolicy.CamelCase.ConvertName(hidden));
        }

        var request = arguments.Deserialize(tool.RequestType, McpSchemaBuilder.Json) ?? Activator.CreateInstance(tool.RequestType);

        foreach (var (property, value) in tool.Fixed)
        {
            tool.RequestType.GetProperty(property)?.SetValue(request, value);
        }

        return request;
    }

    /// <summary>
    /// Sends a request through MediatR with the request's services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<object> SendAsync(IServiceProvider services, object request, CancellationToken cancellationToken)
        => services.GetRequiredService<IMediator>().Send(request, cancellationToken);

    /// <summary>
    /// A failed envelope with a localized message.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="key"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    public static Response<object> Failure(IServiceProvider services, string key, params object[] arguments)
    {
        var localizer = services.GetService<IMilvaLocalizer>();
        var message = localizer == null ? key : arguments.Length > 0 ? localizer[key, arguments] : localizer[key];

        return Response<object>.Error(null, new ResponseMessage(key, message, MessageType.Warning));
    }

    private static CallToolResult Result(object response)
    {
        var success = response is not IResponse envelope || envelope.IsSuccess;
        var text = JsonSerializer.Serialize(response, response?.GetType() ?? typeof(object), McpSchemaBuilder.Json);

        return new CallToolResult { Content = [new TextContentBlock { Text = text }], IsError = !success };
    }

    private static CallToolResult Error(string message) => new() { Content = [new TextContentBlock { Text = message }], IsError = true };

    private static Tool ToProtocolTool(McpToolDefinition tool) => new()
    {
        Name = tool.Name,
        Description = tool.Description,
        InputSchema = JsonSerializer.SerializeToElement(McpSchemaBuilder.Build(tool)),
        Annotations = new ToolAnnotations
        {
            ReadOnlyHint = tool.Kind == McpToolKind.Read,
            DestructiveHint = false,
            OpenWorldHint = tool.Kind != McpToolKind.Write,
        },
    };
}
