namespace Storeasy.UnitTests.TestSupport;

/// <summary>
/// An HTTP handler that answers every request with <c>respond</c> instead of reaching the network. The request content
/// is buffered first, so the callback can read it after the client disposed the request.
/// </summary>
/// <param name="respond"></param>
public sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond = respond;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content != null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

        return _respond(request);
    }
}
