using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Storeasy.Api.Mcp;

/// <summary>
/// Builds the input schema of a tool from its MediatR request type (System.Text.Json schema export, enums as names),
/// with property descriptions from the XML documentation of the Application assembly, the hidden properties removed and
/// the file arguments marked with <c>x-storeasy-upload</c>.
/// </summary>
public static class McpSchemaBuilder
{
    /// <summary>
    /// JSON Schema extension keyword of file arguments.
    /// </summary>
    public const string UploadKeyword = "x-storeasy-upload";

    /// <summary>
    /// Serializer options of tool arguments and results: camelCase, enums as names (numbers accepted too).
    /// </summary>
    public static JsonSerializerOptions Json { get; } = CreateOptions();

    private static readonly ConcurrentDictionary<Assembly, Dictionary<string, string>> _docs = new();

    /// <summary>
    /// Input schema of <paramref name="tool"/>.
    /// </summary>
    /// <param name="tool"></param>
    /// <returns></returns>
    public static JsonObject Build(McpToolDefinition tool)
    {
        var schema = tool.Schema?.DeepClone().AsObject() ?? FromType(tool.RequestType);

        if (schema["properties"] is JsonObject properties)
        {
            foreach (var hidden in tool.Hidden.Concat(tool.Fixed.Keys).Append("OrganizationId"))
            {
                properties.Remove(JsonNamingPolicy.CamelCase.ConvertName(hidden));
            }

            foreach (var (name, upload) in tool.Uploads)
            {
                var key = JsonNamingPolicy.CamelCase.ConvertName(name);

                if (properties[key] is not JsonObject property)
                    continue;

                property[UploadKeyword] = new JsonObject
                {
                    ["kind"] = upload.Kind,
                    ["purpose"] = upload.Purpose,
                    ["accept"] = new JsonArray([.. upload.Accept.Select(a => (JsonNode)a)]),
                };
            }
        }

        if (schema["required"] is JsonArray required)
        {
            var keep = required.Select(r => r?.GetValue<string>()).Where(r => schema["properties"]?[r] != null).ToList();

            schema["required"] = new JsonArray([.. keep.Select(r => (JsonNode)r)]);
        }

        schema["type"] = "object";

        ReplaceBooleanSchemas(schema);

        return schema;
    }

    private static JsonObject FromType(Type type)
    {
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, node) =>
            {
                // A free-form value (JsonElement) comes out as the boolean schema true: give it the object form so it
                // can carry its description too.
                if (node is JsonValue value && value.TryGetValue<bool>(out var allowed) && allowed)
                    node = new JsonObject();

                if (node is JsonObject json && context.PropertyInfo?.AttributeProvider is MemberInfo member && Describe(member) is { } description)
                    json["description"] = description;

                return node;
            },
        };

        var node = Json.GetJsonSchemaAsNode(type, exporterOptions) as JsonObject ?? [];

        node.Remove("$schema");

        ReplaceBooleanSchemas(node);

        return node;
    }

    /// <summary>
    /// Replaces boolean subschemas with their object form. The exporter writes <c>true</c> for a free-form value
    /// (<see cref="JsonElement"/>), which JSON Schema allows but MCP clients refuse: they validate every property schema as
    /// an object and then drop the whole tool list.
    /// </summary>
    /// <param name="node"></param>
    public static void ReplaceBooleanSchemas(JsonNode node)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var key in _schemaMapKeywords)
                {
                    if (json[key] is JsonObject map)
                    {
                        foreach (var name in map.Select(p => p.Key).ToList())
                            map[name] = AsObject(map[name]);
                    }
                }

                foreach (var key in _schemaKeywords)
                {
                    if (json.ContainsKey(key) && json[key] is not JsonArray)
                        json[key] = AsObject(json[key]);
                }

                foreach (var key in _schemaListKeywords)
                {
                    if (json[key] is JsonArray list)
                    {
                        for (var i = 0; i < list.Count; i++)
                            list[i] = AsObject(list[i]);
                    }
                }

                break;
        }
    }

    private static readonly string[] _schemaMapKeywords = ["properties", "$defs", "definitions", "patternProperties"];
    private static readonly string[] _schemaKeywords = ["items", "additionalProperties", "not", "contains"];
    private static readonly string[] _schemaListKeywords = ["anyOf", "oneOf", "allOf", "prefixItems"];

    private static JsonNode AsObject(JsonNode node)
    {
        if (node is JsonValue value && value.TryGetValue<bool>(out var allowed))
            return allowed ? new JsonObject() : new JsonObject { ["not"] = new JsonObject() };

        if (node != null)
            ReplaceBooleanSchemas(node);

        return node;
    }

    private static string Describe(MemberInfo member)
    {
        var docs = _docs.GetOrAdd(member.Module.Assembly, LoadDocs);
        var key = $"P:{member.DeclaringType?.FullName?.Replace('+', '.')}.{member.Name}";

        return docs.TryGetValue(key, out var text) ? text : null;
    }

    private static Dictionary<string, string> LoadDocs(Assembly assembly)
    {
        var path = Path.ChangeExtension(assembly.Location, ".xml");
        var docs = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(assembly.Location) || !File.Exists(path))
            return docs;

        foreach (var member in XDocument.Load(path).Descendants("member"))
        {
            var name = member.Attribute("name")?.Value;
            var summary = member.Element("summary");

            if (name == null || summary == null)
                continue;

            var text = string.Join(" ", summary.Nodes().Select(n => n is XElement e ? e.Attribute("cref")?.Value?.Split('.')[^1] ?? e.Attribute("name")?.Value ?? e.Value : n.ToString())
                                                       .SelectMany(t => t.Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                                                       .Select(t => t.Trim())
                                                       .Where(t => t.Length > 0));

            docs[name] = text;
        }

        return docs;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        options.MakeReadOnly();

        return options;
    }
}
