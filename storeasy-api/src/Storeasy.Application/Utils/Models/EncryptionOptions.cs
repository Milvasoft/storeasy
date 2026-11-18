namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Master keys of the secret protector, bound from the <c>Storeasy:Encryption</c> section. Values are base64 encoded
/// 32 byte keys and come only from user secrets or environment variables (<c>Storeasy__Encryption__MasterKey</c>),
/// never from appsettings.
/// </summary>
public class EncryptionOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Storeasy:Encryption";

    /// <summary>
    /// Current master key (base64, 32 bytes). New secrets are sealed with it.
    /// </summary>
    public string MasterKey { get; set; }

    /// <summary>
    /// Version number of <see cref="MasterKey"/>. Increase it when rotating and move the old key to <see cref="RetiredKeys"/>.
    /// </summary>
    public int KeyVersion { get; set; } = 1;

    /// <summary>
    /// Older master keys by version, kept so rows sealed before a rotation still open until they are re-sealed.
    /// </summary>
    public Dictionary<int, string> RetiredKeys { get; set; } = [];
}
