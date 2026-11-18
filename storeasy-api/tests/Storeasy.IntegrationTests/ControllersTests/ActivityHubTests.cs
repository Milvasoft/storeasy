using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Storeasy.Api.Hubs;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "SignalR activity hub: authentication, organization groups and job events.")]
public class ActivityHubTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private HubConnection Connect(string accessToken, HttpTransportType transport = HttpTransportType.LongPolling)
    {
        var server = _factory.Server;

        return new HubConnectionBuilder().WithUrl(new Uri(server.BaseAddress, ActivityHub.Path.TrimStart('/')), options =>
        {
            options.Transports = transport;
            options.AccessTokenProvider = () => Task.FromResult(accessToken);
            options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            // A browser cannot set headers on a websocket and sends the token in the query string; do the same.
            options.WebSocketFactory = async (context, cancellationToken) =>
            {
                var uri = new UriBuilder(context.Uri) { Query = $"{context.Uri.Query.TrimStart('?')}&access_token={Uri.EscapeDataString(accessToken)}" }.Uri;

                return await server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
            };
        }).Build();
    }

    private static ConcurrentQueue<ActivityEventDto> Collect(HubConnection connection)
    {
        var events = new ConcurrentQueue<ActivityEventDto>();

        connection.On<ActivityEventDto>(ActivityEventTypes.ClientMethod, events.Enqueue);

        return events;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The expected activity events did not arrive.");

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    private async Task<(TestUser Owner, int OrganizationId)> NewOrganizationAsync()
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, body) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });

        return (owner, body.Data);
    }

    [Fact]
    public async Task Member_ShouldReceiveTheJobEventsOfItsOrganization()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        await using var connection = Connect(owner.Token.AccessToken);
        var events = Collect(connection);

        await connection.StartAsync(TestContext.Current.CancellationToken);
        var joined = await connection.InvokeAsync<bool>("JoinOrganization", organizationId, TestContext.Current.CancellationToken);

        // Act
        var jobId = await _factory.WithServiceAsync<IStoreJobQueue, long>(async queue => (await queue.EnqueueAsync([new StoreJobRequest(organizationId, FakeStoreJobHandler.TestKind, new FakeStoreJobHandler.Script(FakeStoreJobHandler.Succeed))], CancellationToken.None))[0]);

        await WaitUntilAsync(() => events.Any(e => e.JobId == jobId && e.Type == ActivityEventTypes.JobSucceeded));

        // Assert
        joined.Should().BeTrue();

        var types = events.Where(e => e.JobId == jobId).Select(e => e.Type).ToList();

        types.Should().Contain([ActivityEventTypes.JobQueued, ActivityEventTypes.JobProgress, ActivityEventTypes.JobStep]);
        events.Should().OnlyContain(e => e.OrganizationId == organizationId);
        events.Where(e => e.Type == ActivityEventTypes.JobStep).Select(e => e.StepName).Should().Contain(["test.call", "test.save"]);
    }

    [Fact]
    public async Task NonMember_ShouldNotJoinAndShouldReceiveNothing()
    {
        // Arrange
        var (_, organizationId) = await NewOrganizationAsync();
        var stranger = await _factory.CreateAndLoginUserAsync();
        await using var connection = Connect(stranger.Token.AccessToken);
        var events = Collect(connection);

        await connection.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var joined = await connection.InvokeAsync<bool>("JoinOrganization", organizationId, TestContext.Current.CancellationToken);
        var jobId = await _factory.WithServiceAsync<IStoreJobQueue, long>(async queue => (await queue.EnqueueAsync([new StoreJobRequest(organizationId, FakeStoreJobHandler.TestKind, new FakeStoreJobHandler.Script(FakeStoreJobHandler.Succeed))], CancellationToken.None))[0]);

        await _factory.DbAsync(async db =>
        {
            var deadline = DateTime.UtcNow + _timeout;

            while (DateTime.UtcNow < deadline && !Storeasy.Domain.Stores.StoreJobRules.IsFinished((await db.StoreJobs.FindAsync(jobId)).Status))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
        });

        // Assert
        joined.Should().BeFalse();
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task WebSocket_ShouldAuthenticateWithTheAccessTokenQueryValue()
    {
        var (owner, organizationId) = await NewOrganizationAsync();
        await using var connection = Connect(owner.Token.AccessToken, HttpTransportType.WebSockets);

        await connection.StartAsync(TestContext.Current.CancellationToken);
        var joined = await connection.InvokeAsync<bool>("JoinOrganization", organizationId, TestContext.Current.CancellationToken);

        joined.Should().BeTrue();
    }

    [Fact]
    public async Task Negotiate_WithoutAToken_ShouldBe401()
    {
        var response = await _factory.CreateClient().PostAsync($"{ActivityHub.Path.TrimStart('/')}/negotiate?negotiateVersion=1", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Negotiate_WithTheTokenInTheQuery_ShouldSucceed()
    {
        var owner = await _factory.CreateAndLoginUserAsync();

        var response = await _factory.CreateClient().PostAsync($"{ActivityHub.Path.TrimStart('/')}/negotiate?negotiateVersion=1&access_token={owner.Token.AccessToken}", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyConnection_ShouldPublishConnectionVerified()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var secret = JsonSerializer.Serialize(new { issuerId = "57246542-96fe-1a63-e053-0824d011072a", keyId = "ABC123DEFG", privateKey = key.ExportPkcs8PrivateKeyPem() });
        var (_, added) = await owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Apple, name = "Key", secret });

        await using var connection = Connect(owner.Token.AccessToken);
        var events = Collect(connection);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await connection.InvokeAsync<bool>("JoinOrganization", organizationId, TestContext.Current.CancellationToken);

        // Act
        await owner.Client.PostAsync<object>("storeconnections/connection/verify", new { organizationId, connectionId = added.Data });
        await WaitUntilAsync(() => events.Any(e => e.Type == ActivityEventTypes.ConnectionVerified));

        // Assert
        var verified = events.Single(e => e.Type == ActivityEventTypes.ConnectionVerified);

        verified.ConnectionId.Should().Be(added.Data);
        verified.ConnectionStatus.Should().Be(ConnectionStatus.Verified);
    }
}
