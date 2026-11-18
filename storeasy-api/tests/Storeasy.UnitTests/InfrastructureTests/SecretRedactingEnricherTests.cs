using FluentAssertions;
using Moq;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Infrastructure.Logging;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Log redaction of access tokens.")]
public class SecretRedactingEnricherTests
{
    [Fact]
    public void Enrich_ShouldRedactTheHubAccessTokenInTheQueryString()
    {
        // Arrange
        var logEvent = new LogEvent(DateTimeOffset.UtcNow,
                                    LogEventLevel.Information,
                                    null,
                                    new MessageTemplateParser().Parse("Request starting {Path}{QueryString}"),
                                    [
                                        new LogEventProperty("Path", new ScalarValue("/hubs/activity")),
                                        new LogEventProperty("QueryString", new ScalarValue("?id=abc&access_token=opaque-token-value")),
                                    ]);

        // Act
        new SecretRedactingEnricher().Enrich(logEvent, new Mock<ILogEventPropertyFactory>().Object);

        // Assert
        logEvent.Properties["QueryString"].ToString().Should().NotContain("opaque-token-value").And.Contain("access_token=[redacted]");
        logEvent.Properties["Path"].ToString().Should().Contain("/hubs/activity");
    }

    [Fact]
    public void Redact_ShouldStripAnAccessTokenQueryValue()
    {
        SecretRedactor.Redact("GET /hubs/activity?access_token=abc.def&id=1").Should().Be("GET /hubs/activity?access_token=[redacted]&id=1");
    }
}
