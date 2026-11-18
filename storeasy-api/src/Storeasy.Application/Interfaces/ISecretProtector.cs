namespace Storeasy.Application.Interfaces;

/// <summary>
/// Envelope encryption of stored secrets (store credentials, webhook secrets). Each call to <see cref="Protect"/> makes a
/// fresh random data key, seals the secret with it (AES-256-GCM) and seals the data key with the current master key.
/// </summary>
public interface ISecretProtector
{
    /// <summary>
    /// Version of the master key new secrets are sealed with.
    /// </summary>
    int CurrentKeyVersion { get; }

    /// <summary>
    /// Seals <paramref name="plaintext"/>. <paramref name="associatedData"/> binds the ciphertext to its owner (e.g.
    /// "StoreConnection:{organizationId}") so a sealed value copied to another row does not open.
    /// </summary>
    ProtectedSecret Protect(ReadOnlySpan<byte> plaintext, string associatedData);

    /// <summary>
    /// Opens a value sealed by <see cref="Protect"/>. Throws <see cref="System.Security.Cryptography.CryptographicException"/>
    /// when the data was changed, the associated data differs or the key version is unknown.
    /// </summary>
    byte[] Unprotect(ProtectedSecret secret, string associatedData);
}

/// <summary>
/// A sealed secret as stored in the database.
/// </summary>
/// <param name="EncryptedSecret">Secret sealed with the data key.</param>
/// <param name="EncryptedDataKey">Data key sealed with the master key.</param>
/// <param name="KeyVersion">Master key version.</param>
public sealed record ProtectedSecret(byte[] EncryptedSecret, byte[] EncryptedDataKey, int KeyVersion);
