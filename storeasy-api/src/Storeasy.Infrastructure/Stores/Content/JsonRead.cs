using System.Globalization;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// Tolerant readers over store JSON (JSON:API documents of App Store Connect, Play resources): a missing or differently
/// typed member reads as null instead of throwing.
/// </summary>
public static class JsonRead
{
    /// <summary>
    /// String member, or null.
    /// </summary>
    /// <param name="element"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string Str(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    /// <summary>
    /// Member as an element, or default.
    /// </summary>
    /// <param name="element"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static JsonElement Get(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    /// <summary>
    /// Array member's items, or none.
    /// </summary>
    /// <param name="element"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IEnumerable<JsonElement> Items(JsonElement element, string name)
    {
        var value = Get(element, name);

        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    }

    /// <summary>
    /// Attribute of a JSON:API resource (<c>attributes.name</c>) as text.
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string Attr(JsonElement resource, string name) => Str(Get(resource, "attributes"), name);

    /// <summary>
    /// Raw JSON of an attribute (null when absent).
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string RawAttr(JsonElement resource, string name)
    {
        var value = Get(Get(resource, "attributes"), name);

        return value.ValueKind == JsonValueKind.Undefined ? null : value.GetRawText();
    }

    /// <summary>
    /// Id of a to-one relationship (<c>relationships.name.data.id</c>), or null.
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string RelId(JsonElement resource, string name) => Str(Get(Get(Get(resource, "relationships"), name), "data"), "id");

    /// <summary>
    /// Ids of a to-many relationship in order.
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static List<string> RelIds(JsonElement resource, string name)
        => [.. Items(Get(Get(resource, "relationships"), name), "data").Select(d => Str(d, "id")).Where(id => id != null)];

    /// <summary>
    /// Integer member, or null.
    /// </summary>
    /// <param name="element"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static int? Int(JsonElement element, string name)
        => int.TryParse(Str(element, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Double member, or null.
    /// </summary>
    /// <param name="element"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static double? Double(JsonElement element, string name)
        => double.TryParse(Str(element, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// UTC instant member, or null.
    /// </summary>
    /// <param name="element"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static DateTime? Date(JsonElement element, string name)
        => DateTimeOffset.TryParse(Str(element, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value.UtcDateTime : null;

    /// <summary>
    /// Calendar day member, or null.
    /// </summary>
    /// <param name="element"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static DateOnly? Day(JsonElement element, string name)
        => DateOnly.TryParse(Str(element, name), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
}
