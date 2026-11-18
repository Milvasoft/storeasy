using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Builds the render request of one image of a template (shared by the preview and the export job, so both draw the
/// same thing): the frame of the class, the slot's raw screenshot for the class's family and locale, the caption with
/// the primary locale fallback, and the background image.
/// </summary>
public static class MockupRenderPlanner
{
    /// <summary>
    /// The request for slot <paramref name="slotOrder"/> of <paramref name="template"/> in <paramref name="locale"/> and
    /// <paramref name="screenshotClass"/> at <paramref name="size"/>, with the notes the choice produced (caption or family
    /// fallback).
    /// </summary>
    /// <param name="template"></param>
    /// <param name="spec">The design to draw (upgraded).</param>
    /// <param name="screenshotClass"></param>
    /// <param name="size"></param>
    /// <param name="locale"></param>
    /// <param name="slotOrder"></param>
    /// <param name="images">Encoded images by asset id.</param>
    /// <param name="outputScale"></param>
    /// <param name="failWhenTooSmall"></param>
    /// <returns></returns>
    public static (MockupRenderRequest Request, List<MockupRenderIssue> Notes) Build(MockupTemplateSnapshot template,
                                                                                     MockupSpec spec,
                                                                                     ScreenshotClass screenshotClass,
                                                                                     PixelSize size,
                                                                                     string locale,
                                                                                     int slotOrder,
                                                                                     IReadOnlyDictionary<long, byte[]> images,
                                                                                     float outputScale,
                                                                                     bool failWhenTooSmall)
    {
        var notes = new List<MockupRenderIssue>();
        var frame = MockupExportRules.ResolveFrame(screenshotClass, spec.Frame.Kind);
        var captionSlot = screenshotClass.SingleImage ? 1 : slotOrder;
        var (title, subtitle, captionFellBack) = MockupExportRules.PickCaption(template.Captions, locale, template.PrimaryLocale, captionSlot);

        if (captionFellBack)
            notes.Add(new MockupRenderIssue(MockupWarningCodes.CaptionFallback, $"No caption in {locale}; the {template.PrimaryLocale} caption is drawn."));

        byte[] screenshot = null;

        if (!screenshotClass.SingleImage)
        {
            var (slot, familyFellBack) = MockupExportRules.PickSlotAsset(template.Slots, slotOrder, screenshotClass.Family, locale);

            if (familyFellBack)
                notes.Add(new MockupRenderIssue(MockupWarningCodes.FamilyFallback, $"No {screenshotClass.Family} screenshot in the slot; the phone screenshot is used."));

            if (slot != null)
                screenshot = images.GetValueOrDefault(slot.AssetId);
        }

        var background = spec.Background.Kind == MockupSpecRules.BackgroundKinds.Image && spec.Background.AssetId is long backgroundId
            ? images.GetValueOrDefault(backgroundId)
            : null;

        var request = new MockupRenderRequest
        {
            Spec = spec,
            Size = size,
            Frame = frame,
            Locale = locale,
            Title = title,
            Subtitle = subtitle,
            Screenshot = screenshot,
            BackgroundImage = background,
            OutputScale = outputScale,
            FailWhenTooSmall = failWhenTooSmall,
        };

        return (request, notes);
    }

    /// <summary>
    /// The class whose caption box is narrowest relative to the type size (the caption overflows there first): the
    /// smallest width to shorter side ratio among <paramref name="classes"/>.
    /// </summary>
    /// <param name="classes"></param>
    /// <returns></returns>
    public static ScreenshotClass NarrowestCaptionClass(IEnumerable<ScreenshotClass> classes)
        => classes.Where(c => !c.SingleImage)
                  .OrderBy(c => c.ExportSize.Width / (float)Math.Min(c.ExportSize.Width, c.ExportSize.Height))
                  .ThenBy(c => c.ExportSize.Width)
                  .FirstOrDefault();
}
