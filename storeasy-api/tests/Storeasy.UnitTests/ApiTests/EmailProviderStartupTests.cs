using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Storeasy.Api.AppStartup;

namespace Storeasy.UnitTests.ApiTests;

/// <summary>
/// <see cref="StartupExtensions.EnsureEmailProvider"/>: the log provider is Development only, and outside Development a
/// provider without its required settings stops the startup.
/// </summary>
[Trait("Api Unit Tests", "E-mail provider check at startup.")]
public class EmailProviderStartupTests
{
    private static readonly Dictionary<string, string> _workingSmtp = new()
    {
        ["Storeasy:Email:Provider"] = "Smtp",
        ["Storeasy:Email:Smtp:Host"] = "smtp.example.com",
        ["Storeasy:Email:Smtp:From"] = "no-reply@example.com",
        ["Storeasy:App:BaseUrl"] = "https://storeasy.example.com",
    };

    [Fact]
    public void LogProvider_OutsideDevelopment_ShouldStopTheStartup()
    {
        // Arrange
        var builder = CreateBuilder("Production", new() { ["Storeasy:Email:Provider"] = "Log", ["Storeasy:App:BaseUrl"] = "https://storeasy.example.com" });

        // Act
        var act = () => builder.EnsureEmailProvider();

        // Assert
        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Development");
    }

    [Fact]
    public void LogProvider_InDevelopment_ShouldBeAccepted()
    {
        // Arrange
        var builder = CreateBuilder("Development", new() { ["Storeasy:Email:Provider"] = "Log" });

        // Act
        var act = () => builder.EnsureEmailProvider();

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Storeasy:Email:Smtp:Host", "Storeasy__Email__Smtp__Host")]
    [InlineData("Storeasy:App:BaseUrl", "Storeasy__App__BaseUrl")]
    public void MissingSetting_OutsideDevelopment_ShouldStopWithAMessageNamingTheSetting(string removedKey, string expectedVariable)
    {
        // Arrange
        var settings = new Dictionary<string, string>(_workingSmtp) { [removedKey] = string.Empty };
        var builder = CreateBuilder("Production", settings);

        // Act
        var act = () => builder.EnsureEmailProvider();

        // Assert
        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(expectedVariable);
    }

    [Fact]
    public void CompleteSmtpSettings_OutsideDevelopment_ShouldBeAccepted()
    {
        // Arrange
        var builder = CreateBuilder("Production", _workingSmtp);

        // Act
        var act = () => builder.EnsureEmailProvider();

        // Assert
        act.Should().NotThrow();
    }

    private static WebApplicationBuilder CreateBuilder(string environmentName, Dictionary<string, string> settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
            ContentRootPath = Directory.CreateTempSubdirectory().FullName
        });

        builder.Configuration.AddInMemoryCollection(settings);

        return builder;
    }
}
