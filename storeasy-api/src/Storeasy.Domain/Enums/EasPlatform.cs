using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Platform of an EAS build (Expo's <c>AppPlatform</c>); <see cref="All"/> only when starting builds.
/// </summary>
public enum EasPlatform : byte
{
    /// <summary>
    /// iOS.
    /// </summary>
    [Description("iOS")]
    Ios = 1,

    /// <summary>
    /// Android.
    /// </summary>
    [Description("Android")]
    Android = 2,

    /// <summary>
    /// Both platforms: one build each.
    /// </summary>
    [Description("iOS and Android")]
    All = 3,
}
