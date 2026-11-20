using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// When a Microsoft Store submission publishes after it passed certification (the API's <c>targetPublishMode</c>). The
/// submission API has no call that publishes; a <see cref="Manual"/> submission waits for "Publish now" in Partner Center.
/// </summary>
public enum MsPublishMode : byte
{
    /// <summary>
    /// Publishes as soon as it passed certification.
    /// </summary>
    [Description("Publish immediately")]
    Immediate = 1,

    /// <summary>
    /// Waits for "Publish now" in Partner Center after certification.
    /// </summary>
    [Description("Publish manually")]
    Manual = 2,

    /// <summary>
    /// Publishes at the submission's publish date (<c>targetPublishDate</c>).
    /// </summary>
    [Description("Publish on a date")]
    SpecificDate = 3,
}
