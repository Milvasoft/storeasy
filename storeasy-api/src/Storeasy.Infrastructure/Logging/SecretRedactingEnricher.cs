using Serilog.Core;
using Serilog.Events;
using Storeasy.Application.Utils.Helpers;

namespace Storeasy.Infrastructure.Logging;

/// <summary>
/// Redacts credentials (<see cref="SecretRedactor"/>) in the string properties of every log event that look like they
/// carry one: the request logs of the hosting layer write the query string, and the activity hub's websocket requests
/// carry the access token there (<c>?access_token=</c>).
/// </summary>
public class SecretRedactingEnricher : ILogEventEnricher
{
    private static readonly string[] _markers = ["access_token", "Bearer ", "eyJ"];

    /// <inheritdoc/>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var redacted = logEvent.Properties
                               .Where(pair => pair.Value is ScalarValue { Value: string text } && _markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                               .Select(pair => new LogEventProperty(pair.Key, new ScalarValue(SecretRedactor.Redact((string)((ScalarValue)pair.Value).Value))))
                               .ToList();

        foreach (var property in redacted)
            logEvent.AddOrUpdateProperty(property);
    }
}
