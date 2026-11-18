namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The primary handler of the store HTTP client in the tests: App Store Connect and its upload hosts go to
/// <see cref="Apple"/>, Google Play and Google's token endpoint to <see cref="Google"/>. Nothing reaches the network.
/// </summary>
public sealed class FakeStoreServer : HttpMessageHandler
{
    public FakeAppStoreConnect Apple { get; } = new();

    public FakeGooglePlay Google { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var host = request.RequestUri.Host;

        if (host is "api.appstoreconnect.apple.com" || host == new Uri(FakeAppStoreConnect.UploadHost).Host)
            return Apple.HandleAsync(request);

        if (host is "androidpublisher.googleapis.com" or "oauth2.googleapis.com")
            return Google.HandleAsync(request);

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway) { Content = new StringContent($"No fake store for {host}") });
    }
}
