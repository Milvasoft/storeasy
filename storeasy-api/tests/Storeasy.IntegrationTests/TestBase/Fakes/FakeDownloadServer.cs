using System.Collections.Concurrent;
using System.Net;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Build services' artifact hosts for pulled packages: a URL serves its registered bytes, or redirects. Records requests.
/// </summary>
public sealed class FakeDownloadServer : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();
    private readonly ConcurrentDictionary<string, string> _redirects = new();

    /// <summary>
    /// Requested URLs.
    /// </summary>
    public ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>
    /// Serves <paramref name="content"/> at <paramref name="url"/>.
    /// </summary>
    public void Serve(string url, byte[] content) => _files[url] = content;

    /// <summary>
    /// Redirects <paramref name="url"/> to <paramref name="location"/>.
    /// </summary>
    public void Redirect(string url, string location) => _redirects[url] = location;

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri.ToString();

        Requests.Enqueue(url);

        if (_redirects.TryGetValue(url, out var location))
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);

            redirect.Headers.Location = new Uri(location);

            return Task.FromResult(redirect);
        }

        return Task.FromResult(_files.TryGetValue(url, out var content)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
