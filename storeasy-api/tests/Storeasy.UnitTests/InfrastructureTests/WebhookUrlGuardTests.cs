using FluentAssertions;
using Moq;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Notifications;
using System.Net;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "SSRF protection of outgoing webhooks.")]
public class WebhookUrlGuardTests
{
    private static WebhookUrlGuard Guard(string resolvesTo = "93.184.216.34", bool allowHttp = false)
    {
        var resolver = new Mock<IHostResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([IPAddress.Parse(resolvesTo)]);

        return new WebhookUrlGuard(resolver.Object, new OrgWebhookOptions { AllowHttp = allowHttp });
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.20.30.40")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.20")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("2002:c0a8:0101::1")]
    public void IsBlocked_ShouldRefusePrivateAndReservedAddresses(string address) => Guard().IsBlocked(IPAddress.Parse(address)).Should().BeTrue();

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    public void IsBlocked_ShouldAllowPublicAddresses(string address) => Guard().IsBlocked(IPAddress.Parse(address)).Should().BeFalse();

    [Theory]
    [InlineData("https://hooks.example.com/x", null)]
    [InlineData("http://hooks.example.com/x", MessageKey.OrgWebhookUrlInvalid)]
    [InlineData("ftp://hooks.example.com/x", MessageKey.OrgWebhookUrlInvalid)]
    [InlineData("not a url", MessageKey.OrgWebhookUrlInvalid)]
    [InlineData("https://a:b@hooks.example.com/x", MessageKey.OrgWebhookUrlInvalid)]
    [InlineData("https://localhost:5000/x", MessageKey.OrgWebhookUrlBlocked)]
    [InlineData("https://printer.local/x", MessageKey.OrgWebhookUrlBlocked)]
    [InlineData("https://192.168.0.1/x", MessageKey.OrgWebhookUrlBlocked)]
    public async Task Check_ShouldValidateTheAddress(string url, string expected)
        => (await Guard().CheckAsync(url, TestContext.Current.CancellationToken)).Should().Be(expected);

    [Fact]
    public async Task Check_ShouldRefuseNamesThatResolveToPrivateAddresses()
        => (await Guard("10.0.0.7").CheckAsync("https://rebind.example.com/x", TestContext.Current.CancellationToken)).Should().Be(MessageKey.OrgWebhookUrlBlocked);

    [Fact]
    public async Task GuardedConnection_ShouldRefuseBlockedAddressesAtConnectTime()
    {
        // Arrange
        var guard = Guard();
        var resolver = new Mock<IHostResolver>();
        resolver.Setup(r => r.ResolveAsync("rebind.example.com", It.IsAny<CancellationToken>())).ReturnsAsync([IPAddress.Loopback]);

        using var client = new HttpClient(GuardedConnection.CreateHandler(guard, resolver.Object));

        // Act
        var act = () => client.GetAsync("https://rebind.example.com/", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
