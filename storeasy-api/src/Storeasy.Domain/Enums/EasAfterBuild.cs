using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What storeasy does with a build once it finished, chosen when the build is started (the app's default or per build).
/// </summary>
public enum EasAfterBuild : byte
{
    /// <summary>
    /// Nothing.
    /// </summary>
    [Description("Nothing")]
    None = 1,

    /// <summary>
    /// Send the package to the build intake (the app's delivery rule takes it to a test channel).
    /// </summary>
    [Description("Send to intake")]
    Intake = 2,

    /// <summary>
    /// Submit it with EAS Submit (TestFlight, or a Play track).
    /// </summary>
    [Description("EAS Submit")]
    Submit = 3,
}
