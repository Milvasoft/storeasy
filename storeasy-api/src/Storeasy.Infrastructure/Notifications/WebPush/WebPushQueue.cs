using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Threading.Channels;

namespace Storeasy.Infrastructure.Notifications.WebPush;

/// <summary>
/// Default <see cref="IWebPushQueue"/>: a bounded in-memory channel read by <see cref="WebPushRunner"/>. When it is full the
/// oldest push is dropped. Registered as a singleton.
/// </summary>
/// <param name="options"></param>
public sealed class WebPushQueue(WebPushOptions options) : IWebPushQueue
{
    /// <summary>
    /// Pushes waiting at most.
    /// </summary>
    public const int Capacity = 1000;

    private readonly WebPushOptions _options = options;
    private readonly Channel<WebPushMessage> _channel = Channel.CreateBounded<WebPushMessage>(new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    /// <summary>
    /// The reading side (the runner).
    /// </summary>
    public ChannelReader<WebPushMessage> Reader => _channel.Reader;

    /// <inheritdoc/>
    public bool TryEnqueue(WebPushMessage message)
    {
        if (!_options.Enabled || message?.UserIds == null || message.UserIds.Count == 0)
            return false;

        return _channel.Writer.TryWrite(message);
    }
}
