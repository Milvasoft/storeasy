using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Studio;

/// <summary>
/// Default proportions of storeasy's device frames, as fractions of the body width. A template's
/// <see cref="JsonModels.MockupFrameSpec.CornerRadius"/> and <see cref="JsonModels.MockupFrameSpec.Bezel"/> are given for
/// the phone and scale every other kind by the same ratio.
/// </summary>
public static class MockupFrameDefaults
{
    /// <summary>
    /// Corner radius of the phone frame.
    /// </summary>
    public const float PhoneRadius = 0.13f;

    /// <summary>
    /// Bezel of the phone frame.
    /// </summary>
    public const float PhoneBezel = 0.035f;

    /// <summary>
    /// Default corner radius and bezel of <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static (float Radius, float Bezel) Of(MockupFrameKind kind) => kind switch
    {
        MockupFrameKind.Phone => (PhoneRadius, PhoneBezel),
        MockupFrameKind.FoldableOuter => (0.11f, PhoneBezel),
        MockupFrameKind.FoldableInner => (0.055f, 0.025f),
        MockupFrameKind.Tablet => (0.06f, 0.04f),
        MockupFrameKind.Desktop => (0.012f, 0f),
        _ => (0.05f, 0f),
    };
}
