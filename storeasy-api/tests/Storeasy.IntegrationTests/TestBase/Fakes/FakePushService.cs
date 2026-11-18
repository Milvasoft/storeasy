using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Browsers and their push services: <see cref="Subscribe"/> makes a browser subscription (its own P-256 key pair and
/// authentication secret, as <c>PushSubscription.toJSON()</c> gives them); every push the API sends is recorded and can be
/// decrypted the way the browser would (RFC 8291). An endpoint marked <see cref="Gone"/> answers 410.
/// </summary>
public sealed class FakePushService : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, (ECDiffieHellman Key, byte[] Auth)> _browsers = new();

    /// <summary>
    /// Pushes received, oldest first.
    /// </summary>
    public ConcurrentQueue<ReceivedPush> Received { get; } = new();

    /// <summary>
    /// Endpoints whose subscription expired at the push service.
    /// </summary>
    public ConcurrentDictionary<string, byte> Gone { get; } = new();

    /// <summary>
    /// A new browser subscription on a fake push service host.
    /// </summary>
    public (string Endpoint, string P256dh, string Auth) Subscribe()
    {
        var endpoint = $"https://push.example.test/send/{Guid.NewGuid():N}";
        var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var auth = RandomNumberGenerator.GetBytes(16);
        var parameters = key.ExportParameters(false);

        _browsers[endpoint] = (key, auth);

        return (endpoint, Base64Url([0x04, .. parameters.Q.X, .. parameters.Q.Y]), Base64Url(auth));
    }

    /// <summary>
    /// The pushes received for <paramref name="endpoint"/>.
    /// </summary>
    public List<ReceivedPush> For(string endpoint) => [.. Received.Where(p => p.Endpoint == endpoint)];

    /// <summary>
    /// Decrypts a received push with the browser's keys (RFC 8291, aes128gcm).
    /// </summary>
    public string Decrypt(ReceivedPush push)
    {
        var (key, auth) = _browsers[push.Endpoint];
        var body = push.Body;
        var salt = body[..16];
        var recordSize = BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4));
        var keyIdLength = body[20];
        var serverPublic = body[21..(21 + keyIdLength)];
        var cipher = body[(21 + keyIdLength)..];

        using var serverKey = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = serverPublic[1..33], Y = serverPublic[33..65] } });

        var parameters = key.ExportParameters(false);
        byte[] userAgentPublic = [0x04, .. parameters.Q.X, .. parameters.Q.Y];
        var shared = key.DeriveRawSecretAgreement(serverKey.PublicKey);
        byte[] keyInfo = [.. Encoding.ASCII.GetBytes("WebPush: info\0"), .. userAgentPublic, .. serverPublic];
        var inputKeyMaterial = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, auth, keyInfo);
        var pseudoRandomKey = HKDF.Extract(HashAlgorithmName.SHA256, inputKeyMaterial, salt);
        var contentKey = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));
        var plain = new byte[cipher.Length - 16];

        using var aes = new AesGcm(contentKey, 16);

        aes.Decrypt(nonce, cipher[..^16], cipher[^16..], plain);

        if (recordSize != 4096 || plain[^1] != 0x02)
            throw new InvalidOperationException("The push is not one aes128gcm record.");

        return Encoding.UTF8.GetString(plain, 0, plain.Length - 1);
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var endpoint = request.RequestUri.ToString();
        var headers = request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                                     .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

        Received.Enqueue(new ReceivedPush(endpoint, headers, request.Content == null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken)));

        return new HttpResponseMessage(Gone.ContainsKey(endpoint) ? HttpStatusCode.Gone : HttpStatusCode.Created);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var browser in _browsers.Values)
                browser.Key.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// One push as the push service received it.
/// </summary>
/// <param name="Endpoint">Subscription address.</param>
/// <param name="Headers">Request and content headers.</param>
/// <param name="Body">Encrypted body.</param>
public sealed record ReceivedPush(string Endpoint, Dictionary<string, string> Headers, byte[] Body);
