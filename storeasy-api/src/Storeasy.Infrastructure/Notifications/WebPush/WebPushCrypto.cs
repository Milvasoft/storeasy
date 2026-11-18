using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.Infrastructure.Notifications.WebPush;

/// <summary>
/// The two cryptographic parts of web push, written against the RFCs with the .NET primitives only (no library):
/// <list type="bullet">
/// <item>Message encryption (RFC 8291 with the <c>aes128gcm</c> content coding of RFC 8188): an ephemeral P-256 key
/// agreement with the browser's key, HKDF-SHA256 with the browser's authentication secret, AES-128-GCM, one record.</item>
/// <item>VAPID (RFC 8292): an ES256 JWT for the push service's origin, sent as <c>Authorization: vapid t=..., k=...</c>.</item>
/// </list>
/// </summary>
public static class WebPushCrypto
{
    /// <summary>
    /// Record size written in the header; one record always fits a push (at most 4 KB).
    /// </summary>
    public const int RecordSize = 4096;

    /// <summary>
    /// Largest plaintext a push service is guaranteed to accept in one push.
    /// </summary>
    public const int MaxPlaintextLength = 3993;

    private static readonly byte[] _keyInfoPrefix = Encoding.ASCII.GetBytes("WebPush: info\0");
    private static readonly byte[] _contentEncryptionKeyInfo = Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0");
    private static readonly byte[] _nonceInfo = Encoding.ASCII.GetBytes("Content-Encoding: nonce\0");

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> to a browser subscription. <paramref name="salt"/> and
    /// <paramref name="ephemeralKey"/> are random when not given (tests pass them for a fixed vector).
    /// </summary>
    /// <param name="plaintext"></param>
    /// <param name="userAgentPublicKey">The subscription's <c>p256dh</c> (65 bytes).</param>
    /// <param name="authSecret">The subscription's <c>auth</c> (16 bytes).</param>
    /// <param name="salt"></param>
    /// <param name="ephemeralKey"></param>
    /// <returns>The request body.</returns>
    public static byte[] Encrypt(ReadOnlySpan<byte> plaintext, byte[] userAgentPublicKey, byte[] authSecret, byte[] salt = null, ECDiffieHellman ephemeralKey = null)
    {
        if (plaintext.Length > MaxPlaintextLength)
            throw new ArgumentException("The push payload is too large.", nameof(plaintext));

        if (userAgentPublicKey?.Length != 65 || userAgentPublicKey[0] != 0x04 || authSecret?.Length != 16)
            throw new ArgumentException("The subscription keys are not a P-256 key and a 16 byte secret.", nameof(userAgentPublicKey));

        salt ??= RandomNumberGenerator.GetBytes(16);

        using var ownedKey = ephemeralKey == null ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256) : null;
        var applicationServerKey = ephemeralKey ?? ownedKey;
        var applicationServerPublicKey = ExportPublicKey(applicationServerKey);

        using var userAgentKey = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = userAgentPublicKey[1..33], Y = userAgentPublicKey[33..65] },
        });

        var sharedSecret = applicationServerKey.DeriveRawSecretAgreement(userAgentKey.PublicKey);

        try
        {
            var keyInfo = new byte[_keyInfoPrefix.Length + 65 + 65];

            _keyInfoPrefix.CopyTo(keyInfo, 0);
            userAgentPublicKey.CopyTo(keyInfo, _keyInfoPrefix.Length);
            applicationServerPublicKey.CopyTo(keyInfo, _keyInfoPrefix.Length + 65);

            var inputKeyMaterial = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);
            var pseudoRandomKey = HKDF.Extract(HashAlgorithmName.SHA256, inputKeyMaterial, salt);
            var contentEncryptionKey = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 16, _contentEncryptionKeyInfo);
            var nonce = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 12, _nonceInfo);

            // One record: the plaintext followed by the last record delimiter 0x02, no padding.
            var padded = new byte[plaintext.Length + 1];

            plaintext.CopyTo(padded);
            padded[^1] = 0x02;

            var body = new byte[16 + 4 + 1 + 65 + padded.Length + 16];

            salt.CopyTo(body, 0);
            BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(16, 4), RecordSize);
            body[20] = 65;
            applicationServerPublicKey.CopyTo(body, 21);

            using var aes = new AesGcm(contentEncryptionKey, 16);

            aes.Encrypt(nonce, padded, body.AsSpan(86, padded.Length), body.AsSpan(86 + padded.Length, 16));

            CryptographicOperations.ZeroMemory(inputKeyMaterial);
            CryptographicOperations.ZeroMemory(pseudoRandomKey);
            CryptographicOperations.ZeroMemory(contentEncryptionKey);

            return body;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedSecret);
        }
    }

    /// <summary>
    /// The <c>Authorization</c> header value for a push to <paramref name="endpoint"/>: an ES256 JWT signed with the VAPID
    /// private key whose audience is the push service's origin, valid for 12 hours, and the VAPID public key.
    /// </summary>
    /// <param name="endpoint"></param>
    /// <param name="subject"></param>
    /// <param name="publicKey">Base64url uncompressed point.</param>
    /// <param name="privateKey">Base64url private scalar.</param>
    /// <param name="now"></param>
    /// <returns></returns>
    public static string CreateVapidAuthorization(Uri endpoint, string subject, string publicKey, string privateKey, DateTimeOffset now)
    {
        var publicPoint = Base64Url.DecodeFromChars(publicKey);

        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.DecodeFromChars(privateKey),
            Q = new ECPoint { X = publicPoint[1..33], Y = publicPoint[33..65] },
        });

        var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}"""));
        var claims = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = endpoint.GetLeftPart(UriPartial.Authority),
            ["exp"] = now.AddHours(12).ToUnixTimeSeconds(),
            ["sub"] = subject,
        }));

        var signingInput = $"{header}.{claims}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"vapid t={signingInput}.{Base64Url.EncodeToString(signature)}, k={publicKey}";
    }

    /// <summary>
    /// The uncompressed point of a P-256 key.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static byte[] ExportPublicKey(ECDiffieHellman key)
    {
        var parameters = key.ExportParameters(false);

        return [0x04, .. parameters.Q.X, .. parameters.Q.Y];
    }
}
