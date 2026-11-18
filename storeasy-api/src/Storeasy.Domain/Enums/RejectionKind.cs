using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What the store rejected. Values are explicit and never reused.
/// </summary>
public enum RejectionKind : byte
{
    /// <summary>App Store REJECTED (binary or app).</summary>
    [Description("Rejected")]
    Rejected = 1,

    /// <summary>App Store METADATA_REJECTED (store listing only, the build stays).</summary>
    [Description("Metadata rejected")]
    MetadataRejected = 2,

    /// <summary>Microsoft Store certification failed.</summary>
    [Description("Certification failed")]
    CertificationFailed = 3,
}
