using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Infrastructure.Security;

/// <summary>
/// AES-256-GCM envelope encryption. Layout of each sealed blob: 12 byte nonce, ciphertext, 16 byte tag.
/// The secret blob uses the associated data given by the caller; the data key blob additionally binds the key version.
/// </summary>
public sealed class EnvelopeSecretProtector : ISecretProtector
{
    private const int _keySize = 32;
    private const int _nonceSize = 12;
    private const int _tagSize = 16;

    private readonly Dictionary<int, byte[]> _masterKeys;
    private readonly int _currentVersion;

    /// <summary>
    /// Creates the protector. Throws <see cref="InvalidOperationException"/> naming the environment variable when the
    /// master key is missing or not a base64 encoded 32 byte value.
    /// </summary>
    public EnvelopeSecretProtector(EncryptionOptions options)
    {
        _currentVersion = options.KeyVersion;
        _masterKeys = new Dictionary<int, byte[]>
        {
            [options.KeyVersion] = DecodeKey(options.MasterKey, options.KeyVersion),
        };

        foreach (var (version, key) in options.RetiredKeys ?? [])
        {
            if (version != options.KeyVersion)
            {
                _masterKeys[version] = DecodeKey(key, version);
            }
        }
    }

    /// <inheritdoc/>
    public int CurrentKeyVersion => _currentVersion;

    /// <inheritdoc/>
    public ProtectedSecret Protect(ReadOnlySpan<byte> plaintext, string associatedData)
    {
        var dataKey = RandomNumberGenerator.GetBytes(_keySize);

        try
        {
            var encryptedSecret = Seal(dataKey, plaintext, Encoding.UTF8.GetBytes(associatedData ?? string.Empty));
            var encryptedDataKey = Seal(_masterKeys[_currentVersion], dataKey, KeyAssociatedData(_currentVersion, associatedData));

            return new ProtectedSecret(encryptedSecret, encryptedDataKey, _currentVersion);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public byte[] Unprotect(ProtectedSecret secret, string associatedData)
    {
        ArgumentNullException.ThrowIfNull(secret);

        if (!_masterKeys.TryGetValue(secret.KeyVersion, out var masterKey))
        {
            throw new CryptographicException($"Unknown master key version {secret.KeyVersion}.");
        }

        var dataKey = Open(masterKey, secret.EncryptedDataKey, KeyAssociatedData(secret.KeyVersion, associatedData));

        try
        {
            return Open(dataKey, secret.EncryptedSecret, Encoding.UTF8.GetBytes(associatedData ?? string.Empty));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private static byte[] KeyAssociatedData(int version, string associatedData) => Encoding.UTF8.GetBytes($"dek:v{version}:{associatedData}");

    private static byte[] Seal(byte[] key, ReadOnlySpan<byte> plaintext, byte[] associatedData)
    {
        var output = new byte[_nonceSize + plaintext.Length + _tagSize];
        var nonce = output.AsSpan(0, _nonceSize);
        var cipher = output.AsSpan(_nonceSize, plaintext.Length);
        var tag = output.AsSpan(_nonceSize + plaintext.Length, _tagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, _tagSize);

        aes.Encrypt(nonce, plaintext, cipher, tag, associatedData);

        return output;
    }

    private static byte[] Open(byte[] key, byte[] blob, byte[] associatedData)
    {
        if (blob == null || blob.Length < _nonceSize + _tagSize)
        {
            throw new CryptographicException("Sealed value is too short.");
        }

        var length = blob.Length - _nonceSize - _tagSize;
        var plaintext = new byte[length];

        using var aes = new AesGcm(key, _tagSize);

        aes.Decrypt(blob.AsSpan(0, _nonceSize), blob.AsSpan(_nonceSize, length), blob.AsSpan(_nonceSize + length, _tagSize), plaintext, associatedData);

        return plaintext;
    }

    private static byte[] DecodeKey(string value, int version)
    {
        byte[] key = null;

        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                key = Convert.FromBase64String(value.Trim());
            }
            catch (FormatException)
            {
                key = null;
            }
        }

        if (key == null || key.Length != _keySize)
        {
            throw new InvalidOperationException($"Master key version {version} must be a base64 encoded 32 byte value. Set Storeasy__Encryption__MasterKey (generate one with: openssl rand -base64 32).");
        }

        return key;
    }
}
