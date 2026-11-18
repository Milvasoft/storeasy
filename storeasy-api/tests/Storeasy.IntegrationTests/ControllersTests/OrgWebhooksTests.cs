using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Globalization;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Outgoing webhooks: SSRF checks, signed deliveries, retries, test delivery, delivery log.")]
public class OrgWebhooksTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("http://hooks.example.com/x")]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://10.0.0.5/x")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://[::1]/x")]
    [InlineData("https://localhost/x")]
    [InlineData("https://user:pass@hooks.example.com/x")]
    [InlineData("https://private-dns.example.com/x")]
    public async Task Subscribe_ShouldRefuseAddressesThatAreNotPublicHttps(string url)
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        _factory.HostResolver.Hosts["private-dns.example.com"] = [IPAddress.Parse("192.168.1.20")];

        // Act
        var (http, body) = await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/subscription", new { organizationId = org.OrganizationId, name = "Bad", url });

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Data.Should().BeNull();
    }

    [Fact]
    public async Task TestDelivery_ShouldPostASignedEventAndLogIt()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var host = $"ok-{org.Suffix}.example.com";
        var (_, subscription) = await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/subscription", new { organizationId = org.OrganizationId, name = "Slack relay", url = $"https://{host}/hook" });

        // Act
        var (http, attempt) = await org.Owner.Client.PostAsync<WebhookDeliveryDto>("webhooks/subscription/test", new { organizationId = org.OrganizationId, webhookId = subscription.Data.Id });
        var received = _factory.WebhookReceiver.For(host).Single();
        var (_, log) = await org.Owner.Client.PatchListAsync<WebhookDeliveryDto>("webhooks/deliveries", new { organizationId = org.OrganizationId, pageNumber = 1, rowCount = 10 });
        var (_, list) = await org.Owner.Client.GetAsync<List<OrgWebhookDto>>($"webhooks/subscriptions?organizationId={org.OrganizationId}");

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        attempt.Data.Should().Match<WebhookDeliveryDto>(d => d.Status == WebhookDeliveryStatus.Succeeded && d.ResponseStatusCode == 200 && d.IsTest && d.Attempt == 1);
        received.Headers[WebhookSignatures.EventTypeHeader].Should().Be("webhook.test");
        WebhookSignatures.VerifyStoreasy(long.Parse(received.Headers[WebhookSignatures.TimestampHeader], CultureInfo.InvariantCulture), received.Body, received.Headers[WebhookSignatures.SignatureHeader], subscription.Data.Secret).Should().BeTrue();
        log.Data.Should().ContainSingle(d => d.Id == attempt.Data.Id);
        list.Data.Should().ContainSingle().Which.LastDeliveryStatus.Should().Be(WebhookDeliveryStatus.Succeeded);
    }

    [Fact]
    public async Task FailingReceiver_ShouldBeRetriedThenFail()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var host = $"down-{org.Suffix}.example.com";
        _factory.WebhookReceiver.Statuses[host] = HttpStatusCode.InternalServerError;
        await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/subscription", new { organizationId = org.OrganizationId, name = "Down", url = $"https://{host}/hook" });

        // Act
        var queued = await _factory.WithServiceAsync<Application.Interfaces.IOrgWebhookPublisher, int>(p => p.EnqueueAsync(org.OrganizationId, "review.received", new { count = 1 }, CancellationToken.None));

        await EventTestKit.EventuallyAsync(() => _factory.DbAsync(db => db.WebhookDeliveries.AnyAsync(d => d.OrganizationId == org.OrganizationId && d.Status == WebhookDeliveryStatus.Failed)), "the delivery did not fail for good");

        var delivery = await _factory.DbAsync(db => db.WebhookDeliveries.AsNoTracking().SingleAsync(d => d.OrganizationId == org.OrganizationId));

        // Assert
        queued.Should().Be(1);
        delivery.Attempt.Should().Be(3, "the tests allow three attempts");
        delivery.ResponseStatusCode.Should().Be(500);
        _factory.WebhookReceiver.For(host).Should().HaveCount(3).And.OnlyContain(r => r.Headers[WebhookSignatures.EventIdHeader] == delivery.EventId);
    }

    [Fact]
    public async Task Subscriptions_ShouldOnlyGetTheirEventsAndStayInTheirOrganization()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var other = await _factory.NewOrgAsync();
        var (_, subscription) = await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/subscription", new { organizationId = org.OrganizationId, name = "Only rejections", url = $"https://only-{org.Suffix}.example.com/x", events = new[] { "rejection.opened" } });

        // Act
        var queued = await _factory.WithServiceAsync<Application.Interfaces.IOrgWebhookPublisher, int>(p => p.EnqueueAsync(org.OrganizationId, "review.received", new { }, CancellationToken.None));
        var (update, _) = await org.Owner.Client.PutAsync<int>("webhooks/subscription", new { organizationId = org.OrganizationId, webhookId = subscription.Data.Id, url = "https://10.1.2.3/x" });
        var (foreign, _) = await other.Owner.Client.PostAsync<WebhookDeliveryDto>("webhooks/subscription/test", new { organizationId = other.OrganizationId, webhookId = subscription.Data.Id });
        var (foreignDelete, _) = await other.Owner.Client.SendAsync<int>(HttpMethod.Delete, "webhooks/subscription", new { organizationId = other.OrganizationId, webhookId = subscription.Data.Id });
        var (unknownEvent, _) = await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/subscription", new { organizationId = org.OrganizationId, name = "x", url = "https://x.example.com/x", events = new[] { "everything" } });

        // Assert
        queued.Should().Be(0);
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknownEvent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
