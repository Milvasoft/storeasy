using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Inbound webhooks: App Store Connect receiver, signed receiver, endpoint management, event log.")]
public class WebhooksControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<WebhookSecretDto> CreateAppleEndpointAsync(EventOrg org, bool register = false)
    {
        var (http, body) = await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/endpoint", new { organizationId = org.OrganizationId, source = WebhookSource.AppStoreConnect, storeConnectionId = org.AppleConnectionId, registerAtStore = register });

        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.Messages?.FirstOrDefault()?.Message);

        return body.Data;
    }

    private static string StatePayload(string eventId, string versionId, string oldValue, string newValue, DateTime? timestamp = null)
        => JsonSerializer.Serialize(new
        {
            data = new
            {
                type = "appStoreVersionAppVersionStateUpdated",
                id = eventId,
                version = 1,
                attributes = new { oldValue, newValue, timestamp = (timestamp ?? DateTime.UtcNow).ToString("O", CultureInfo.InvariantCulture) },
                relationships = new { instance = new { data = new { type = "appStoreVersions", id = versionId } } },
            },
        });

    private static string Ping(string id) => JsonSerializer.Serialize(new { data = new { type = "webhookPingCreated", id } });

    private async Task<HttpResponseMessage> SendAppleAsync(string url, string body, string signature)
    {
        var client = _factory.CreateClientWithAddress();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url).PathAndQuery) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        request.Headers.TryAddWithoutValidation("x-apple-signature", signature);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AppleEvent_Rejected_ShouldOpenACaseNotifyAndDeliverTheOutgoingWebhook()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (appId, storeAppId, externalId) = await _factory.AddAppAsync(org);
        var admin = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Admin);
        var adminEmail = (await _factory.GetUserRowAsync(admin.UserName)).Email;
        var endpoint = await CreateAppleEndpointAsync(org);
        var versionId = $"v-{org.Suffix}";
        var hookHost = $"hooks-{org.Suffix}.example.com";

        var (_, subscription) = await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/subscription", new { organizationId = org.OrganizationId, name = "CI", url = $"https://{hookHost}/storeasy", events = new[] { "rejection.opened" } });

        _factory.StoreEvents.Versions[versionId] = new StoreVersionRecord(versionId, externalId, "1.4", "IOS", "REJECTED", "52", DateTime.UtcNow);

        var body = StatePayload($"evt-{org.Suffix}", versionId, "IN_REVIEW", "REJECTED");

        // Act
        var response = await SendAppleAsync(endpoint.Url, body, WebhookSignatures.ComputeApple(body, endpoint.Secret));

        await EventTestKit.EventuallyAsync(() => _factory.DbAsync(db => db.RejectionCases.AnyAsync(c => c.StoreAppId == storeAppId)), "no rejection case was opened");
        await EventTestKit.EventuallyAsync(() => Task.FromResult(_factory.WebhookReceiver.For(hookHost).Count > 0), "the outgoing webhook was not delivered");

        var rejectionCase = await _factory.DbAsync(db => db.RejectionCases.AsNoTracking().SingleAsync(c => c.StoreAppId == storeAppId));
        var webhookEvent = await _factory.DbAsync(db => db.WebhookEvents.AsNoTracking().SingleAsync(e => e.OrganizationId == org.OrganizationId));
        var notifications = await _factory.DbAsync(db => db.InternalNotifications.AsNoTracking().Where(n => n.OrganizationId == org.OrganizationId && n.Type == NotificationType.Rejection).ToListAsync());
        var delivery = _factory.WebhookReceiver.For(hookHost).Single();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        rejectionCase.Should().Match<Domain.Feedback.RejectionCase>(c => c.AppId == appId && c.VersionString == "1.4" && c.BuildNumber == "52" && c.Kind == RejectionKind.Rejected && c.State == RejectionState.Open && c.OpenedVia == RejectionSource.Webhook);
        webhookEvent.SignatureValid.Should().BeTrue();
        webhookEvent.EventType.Should().Be(AppStoreVersionStates.StateUpdatedEvent);
        await EventTestKit.EventuallyAsync(() => _factory.DbAsync(db => db.WebhookEvents.AnyAsync(e => e.Id == webhookEvent.Id && e.ProcessResult == WebhookProcessResult.Processed)), "the event was not marked processed");
        notifications.Select(n => n.RecipientUserId).Should().BeEquivalentTo([org.Owner.Id, admin.Id]);
        notifications.Should().OnlyContain(n => n.RelatedEntityType == NotificationEntity.RejectionCase && n.ActionLink.Contains($"/rejections/{rejectionCase.Id}"));
        (await _factory.EmailSender.WaitForAsync(adminEmail)).Should().ContainSingle(m => m.Subject.Contains("Defter"));
        delivery.Body.Should().Contain("rejection.opened");
        var timestamp = long.Parse(delivery.Headers[WebhookSignatures.TimestampHeader], CultureInfo.InvariantCulture);
        WebhookSignatures.VerifyStoreasy(timestamp, delivery.Body, delivery.Headers[WebhookSignatures.SignatureHeader], subscription.Data.Secret).Should().BeTrue();
    }

    [Fact]
    public async Task AppleEvent_InvalidSignature_ShouldBe401KeptWithoutBodyAndAlertOnce()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var endpoint = await CreateAppleEndpointAsync(org);
        var body = StatePayload($"evt-{org.Suffix}", "v1", "IN_REVIEW", "REJECTED");

        // Act
        var first = await SendAppleAsync(endpoint.Url, body, WebhookSignatures.ComputeApple(body, "whsec_wrong"));
        var second = await SendAppleAsync(endpoint.Url, body, "hmacsha256=nothex");

        var events = await _factory.DbAsync(db => db.WebhookEvents.AsNoTracking().Where(e => e.OrganizationId == org.OrganizationId).ToListAsync());
        var alerts = await _factory.DbAsync(db => db.InternalNotifications.CountAsync(n => n.OrganizationId == org.OrganizationId && n.Type == NotificationType.ConnectionAlert));

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        events.Should().HaveCount(2).And.OnlyContain(e => !e.SignatureValid && e.ProcessResult == WebhookProcessResult.InvalidSignature && e.Payload == null && e.ExternalEventId == null);
        events.First().SignatureHeaderMasked.Should().Contain("*");
        alerts.Should().Be(1, "the owner is alerted at most once an hour");
    }

    [Fact]
    public async Task AppleEvent_RepeatedOrStale_ShouldBeStoredOnce()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var endpoint = await CreateAppleEndpointAsync(org);
        var ping = Ping($"ping-{org.Suffix}");
        var stale = StatePayload($"old-{org.Suffix}", "v1", "IN_REVIEW", "REJECTED", DateTime.UtcNow.AddDays(-30));

        // Act
        var first = await SendAppleAsync(endpoint.Url, ping, WebhookSignatures.ComputeApple(ping, endpoint.Secret));
        var again = await SendAppleAsync(endpoint.Url, ping, WebhookSignatures.ComputeApple(ping, endpoint.Secret));
        var old = await SendAppleAsync(endpoint.Url, stale, WebhookSignatures.ComputeApple(stale, endpoint.Secret));
        var unknown = await SendAppleAsync(endpoint.Url.Replace(endpoint.Url.Split('/').Last(), "whe_unknown"), ping, WebhookSignatures.ComputeApple(ping, endpoint.Secret));

        var events = await _factory.DbAsync(db => db.WebhookEvents.AsNoTracking().Where(e => e.OrganizationId == org.OrganizationId).ToListAsync());
        var registration = await _factory.DbAsync(db => db.WebhookEndpoints.AsNoTracking().Where(e => e.Id == endpoint.Id).Select(e => e.Registration).SingleAsync());

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        old.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        events.Count(e => e.ExternalEventId == $"ping-{org.Suffix}").Should().Be(1, "a repeated event is stored once");
        events.Should().ContainSingle(e => e.ProcessResult == WebhookProcessResult.Stale);
        registration.Should().Be(WebhookRegistrationState.Manual, "the first valid event confirms a manual registration");
    }

    [Fact]
    public async Task RotateSecret_ShouldKeepTheOldSecretValidDuringTheGracePeriod()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var endpoint = await CreateAppleEndpointAsync(org);

        // Act
        var (_, rotated) = await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/endpoint/rotate-secret", new { organizationId = org.OrganizationId, endpointId = endpoint.Id });
        var oldBody = Ping($"a-{org.Suffix}");
        var newBody = Ping($"b-{org.Suffix}");
        var withOld = await SendAppleAsync(endpoint.Url, oldBody, WebhookSignatures.ComputeApple(oldBody, endpoint.Secret));
        var withNew = await SendAppleAsync(endpoint.Url, newBody, WebhookSignatures.ComputeApple(newBody, rotated.Data.Secret));

        // Assert
        rotated.Data.Secret.Should().NotBe(endpoint.Secret);
        rotated.Data.PreviousSecretValidUntil.Should().BeAfter(DateTime.UtcNow.AddHours(23));
        withOld.StatusCode.Should().Be(HttpStatusCode.OK);
        withNew.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignedEndpoint_ShouldCheckSignatureAndTimestamp()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (_, endpoint) = await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/endpoint", new { organizationId = org.OrganizationId, source = WebhookSource.Signed, name = "Codemagic" });
        var client = _factory.CreateClientWithAddress();

        async Task<HttpResponseMessage> SendAsync(long timestamp, string secret, string eventId)
        {
            var body = $$"""{"type":"artifact.ready","id":"{{eventId}}"}""";
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint.Data.Url).PathAndQuery) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

            request.Headers.TryAddWithoutValidation(WebhookSignatures.TimestampHeader, timestamp.ToString(CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation(WebhookSignatures.EventIdHeader, eventId);
            request.Headers.TryAddWithoutValidation(WebhookSignatures.SignatureHeader, WebhookSignatures.ComputeStoreasy(timestamp, body, secret));

            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var valid = await SendAsync(now, endpoint.Data.Secret, $"e1-{org.Suffix}");
        var stale = await SendAsync(now - 3600, endpoint.Data.Secret, $"e2-{org.Suffix}");
        var forged = await SendAsync(now, "whsec_other", $"e3-{org.Suffix}");
        var (_, events) = await org.Owner.Client.PatchListAsync<WebhookEventDto>("webhooks/events", new { organizationId = org.OrganizationId, pageNumber = 1, rowCount = 10 });

        // Assert
        endpoint.Data.Url.Should().Contain("/api/webhooks/signed/");
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
        stale.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        forged.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        events.Data.Should().HaveCount(3);
        events.Data.Should().ContainSingle(e => e.SignatureValid && e.EventType == "artifact.ready" && e.ProcessResult == WebhookProcessResult.Processed);
    }

    [Fact]
    public async Task CreateEndpoint_WithRegistration_ShouldRegisterOneWebhookPerAppAndListIt()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (_, _, first) = await _factory.AddAppAsync(org);
        var (_, _, second) = await _factory.AddAppAsync(org, name: "Mola");

        // Act
        var endpoint = await CreateAppleEndpointAsync(org, register: true);
        (await _factory.WaitForJobAsync(endpoint.JobId.Value)).Status.Should().Be(StoreJobStatus.Succeeded);
        var (_, list) = await org.Owner.Client.GetAsync<List<WebhookEndpointDto>>($"webhooks?organizationId={org.OrganizationId}");
        var duplicate = await org.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/endpoint", new { organizationId = org.OrganizationId, storeConnectionId = org.AppleConnectionId });

        // Assert
        _factory.StoreEvents.Webhooks.Where(w => w.AppId == first || w.AppId == second).Should().HaveCount(2)
                                     .And.OnlyContain(w => w.Registration.Url == endpoint.Url && w.Registration.Secret == endpoint.Secret && w.Registration.EventTypes.Contains(AppStoreVersionStates.StateUpdatedEvent));
        list.Data.Should().ContainSingle().Which.Should().Match<WebhookEndpointDto>(e => e.Registration == WebhookRegistrationState.Api && e.ExternalWebhookCount == 2 && e.Url == endpoint.Url);
        duplicate.Http.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateEndpoint_KeyWithoutRole_ShouldMarkTheRegistrationFailed()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (_, _, externalId) = await _factory.AddAppAsync(org);
        _factory.StoreEvents.RefuseWebhooks[externalId] = true;

        // Act
        var endpoint = await CreateAppleEndpointAsync(org, register: true);
        var job = await _factory.WaitForJobAsync(endpoint.JobId.Value);
        var row = await _factory.DbAsync(db => db.WebhookEndpoints.AsNoTracking().SingleAsync(e => e.Id == endpoint.Id));

        // Assert
        job.Status.Should().Be(StoreJobStatus.Failed);
        row.Registration.Should().Be(WebhookRegistrationState.Failed);
        row.RegistrationError.Should().Contain("403");
    }

    [Fact]
    public async Task OtherOrganization_ShouldGet404AndEditorsShouldBeForbidden()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var other = await _factory.NewOrgAsync();
        var endpoint = await CreateAppleEndpointAsync(org);
        var body = Ping($"p-{org.Suffix}");
        await SendAppleAsync(endpoint.Url, body, WebhookSignatures.ComputeApple(body, endpoint.Secret));
        var eventId = await _factory.DbAsync(db => db.WebhookEvents.Where(e => e.OrganizationId == org.OrganizationId).Select(e => e.Id).SingleAsync());
        var editor = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Editor);

        // Act
        var (detail, _) = await other.Owner.Client.GetAsync<WebhookEventDetailDto>($"webhooks/event?organizationId={other.OrganizationId}&eventId={eventId}");
        var rotate = await other.Owner.Client.PostAsync<WebhookSecretDto>("webhooks/endpoint/rotate-secret", new { organizationId = other.OrganizationId, endpointId = endpoint.Id });
        var (own, ownBody) = await org.Owner.Client.GetAsync<WebhookEventDetailDto>($"webhooks/event?organizationId={org.OrganizationId}&eventId={eventId}");
        var (editorList, _) = await editor.Client.GetAsync<List<WebhookEndpointDto>>($"webhooks?organizationId={org.OrganizationId}");

        // Assert
        detail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        rotate.Http.StatusCode.Should().Be(HttpStatusCode.NotFound);
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        ownBody.Data.Payload.Should().Contain("webhookPingCreated");
        editorList.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reprocess_ShouldQueueTheEventAgainAndDeleteShouldRemoveTheStoreWebhooks()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (_, storeAppId, externalId) = await _factory.AddAppAsync(org);
        var endpoint = await CreateAppleEndpointAsync(org, register: true);
        await _factory.WaitForJobAsync(endpoint.JobId.Value);
        var versionId = $"v-{org.Suffix}";
        _factory.StoreEvents.Versions[versionId] = new StoreVersionRecord(versionId, externalId, "2.0", "IOS", "IN_REVIEW", "7", DateTime.UtcNow);
        var body = StatePayload($"evt-{org.Suffix}", versionId, "WAITING_FOR_REVIEW", "IN_REVIEW");
        await SendAppleAsync(endpoint.Url, body, WebhookSignatures.ComputeApple(body, endpoint.Secret));
        await _factory.WaitForJobsAsync(org.OrganizationId, StoreJobKind.ProcessWebhookEvent);
        var eventId = await _factory.DbAsync(db => db.WebhookEvents.Where(e => e.OrganizationId == org.OrganizationId).Select(e => e.Id).SingleAsync());

        // Act
        var (_, jobId) = await org.Owner.Client.PostAsync<long>("webhooks/event/reprocess", new { organizationId = org.OrganizationId, eventId });
        (await _factory.WaitForJobAsync(jobId.Data)).Status.Should().Be(StoreJobStatus.Succeeded);
        var (deleteHttp, _) = await org.Owner.Client.SendAsync<int>(HttpMethod.Delete, "webhooks/endpoint", new { organizationId = org.OrganizationId, endpointId = endpoint.Id });
        await _factory.WaitForJobsAsync(org.OrganizationId, StoreJobKind.RegisterWebhooks);
        var state = await _factory.DbAsync(db => db.AppVersionStates.AsNoTracking().SingleAsync(s => s.StoreAppId == storeAppId));

        // Assert
        state.State.Should().Be("IN_REVIEW");
        state.SubmittedDate.Should().NotBeNull();
        deleteHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.StoreEvents.DeletedWebhooks.Should().Contain(_factory.StoreEvents.Webhooks.Where(w => w.AppId == externalId).Select(w => w.WebhookId));
    }
}
