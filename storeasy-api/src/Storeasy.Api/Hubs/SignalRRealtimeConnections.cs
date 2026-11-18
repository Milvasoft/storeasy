using Microsoft.AspNetCore.SignalR;
using Milvasoft.Caching.Redis.Accessor;
using StackExchange.Redis;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using System.Text.Json;

namespace Storeasy.Api.Hubs;

/// <summary>
/// <see cref="IRealtimeConnections"/> over <see cref="ActivityHub"/>. A command is applied to this instance's connections
/// (<see cref="HubConnectionRegistry"/>) at once and published on a Redis channel under the application's key prefix,
/// where the other instances apply it to theirs (each ignores its own messages). Without Redis there is one instance and
/// the local step is enough. Registered as a singleton and as a hosted service (the subscription).
/// </summary>
/// <param name="hubContext"></param>
/// <param name="registry"></param>
/// <param name="cache"></param>
/// <param name="cacheKeys"></param>
/// <param name="logger"></param>
public sealed class SignalRRealtimeConnections(IHubContext<ActivityHub> hubContext,
                                               HubConnectionRegistry registry,
                                               IRedisAccessor cache,
                                               CacheKeys cacheKeys,
                                               ILogger<SignalRRealtimeConnections> logger) : IRealtimeConnections, IHostedService
{
    private readonly IHubContext<ActivityHub> _hubContext = hubContext;
    private readonly HubConnectionRegistry _registry = registry;
    private readonly IRedisAccessor _cache = cache;
    private readonly ILogger<SignalRRealtimeConnections> _logger = logger;
    private readonly RedisChannel _channel = RedisChannel.Literal(cacheKeys.Build("hub-control"));
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public Task RemoveFromOrganizationAsync(int userId, int organizationId) => RunAsync(new HubControlCommand(_instanceId, userId, organizationId, null, false, null));

    /// <inheritdoc/>
    public Task DisconnectAsync(int userId, string currentStamp) => RunAsync(new HubControlCommand(_instanceId, userId, null, currentStamp, true, null));

    /// <inheritdoc/>
    public Task DisconnectTokensAsync(int userId, IReadOnlyCollection<string> tokenHashes)
        => tokenHashes == null || tokenHashes.Count == 0 ? Task.CompletedTask : RunAsync(new HubControlCommand(_instanceId, userId, null, null, true, [.. tokenHashes]));

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_cache.IsConnected())
            return;

        try
        {
            await _cache.GetDatabase().Multiplexer.GetSubscriber().SubscribeAsync(_channel, (channel, message) => OnMessageAsync(message).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The live connection control channel could not be subscribed; changes apply to this instance only.");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunAsync(HubControlCommand command)
    {
        await ApplyAsync(command);

        if (!_cache.IsConnected())
            return;

        try
        {
            await _cache.GetDatabase().Multiplexer.GetSubscriber().PublishAsync(_channel, JsonSerializer.Serialize(command));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A live connection command for user {UserId} could not reach the other instances.", command.UserId);
        }
    }

    private async Task OnMessageAsync(RedisValue message)
    {
        try
        {
            var command = JsonSerializer.Deserialize<HubControlCommand>(message.ToString());

            if (command != null && command.InstanceId != _instanceId)
                await ApplyAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A live connection command could not be applied.");
        }
    }

    private async Task ApplyAsync(HubControlCommand command)
    {
        foreach (var connection in _registry.OfUser(command.UserId))
        {
            if (command.Disconnect)
            {
                var matches = command.TokenHashes != null
                    ? connection.TokenHash != null && command.TokenHashes.Contains(connection.TokenHash)
                    : command.Stamp == null || !string.Equals(connection.Stamp, command.Stamp, StringComparison.Ordinal);

                if (matches)
                    connection.Context.Abort();

                continue;
            }

            if (command.OrganizationId is { } organizationId && connection.Organizations.TryRemove(organizationId, out _))
            {
                try
                {
                    await _hubContext.Groups.RemoveFromGroupAsync(connection.Context.ConnectionId, ActivityHub.GroupName(organizationId));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Connection of user {UserId} could not leave organization {OrganizationId}.", command.UserId, organizationId);
                }
            }
        }
    }

    private sealed record HubControlCommand(string InstanceId, int UserId, int? OrganizationId, string Stamp, bool Disconnect, List<string> TokenHashes);
}
