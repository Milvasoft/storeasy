using System.Text.Json;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// The body of an outgoing webhook delivery: <c>{"id","type","organizationId","createdDate","data"}</c> in camelCase.
/// </summary>
public static class OrgWebhookPayloads
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes one event.
    /// </summary>
    /// <param name="eventId"></param>
    /// <param name="eventType"></param>
    /// <param name="organizationId"></param>
    /// <param name="date"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string Envelope(string eventId, string eventType, int organizationId, DateTime date, object data)
        => JsonSerializer.Serialize(new { id = eventId, type = eventType, organizationId, createdDate = date, data }, _json);
}
