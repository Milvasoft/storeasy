using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Upload state of a stored file.
/// </summary>
public enum AssetStatus : byte
{
    /// <summary>
    /// Upload URLs were handed out; the file is not confirmed yet.
    /// </summary>
    [Description("Pending")]
    Pending = 1,

    /// <summary>
    /// The file is in the bucket with the declared size.
    /// </summary>
    [Description("Uploaded")]
    Uploaded = 2,

    /// <summary>
    /// The upload did not match its declaration and was removed.
    /// </summary>
    [Description("Rejected")]
    Rejected = 3,
}
