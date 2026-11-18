using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Plans a studio export (docs 12.10): the classes of the app's stores from the rules catalog, their requirement for the
/// app today, the default selection (required and recommended), size overrides checked against the sizes the store accepts
/// (for example 1320x2868 is refused for the 6.1"/6.3" class), and the image count. Pure; the plan and the export
/// endpoints share it so the export renders exactly what the plan showed.
/// </summary>
public static class MockupExportPlanner
{
    /// <summary>
    /// Plans an export of <paramref name="template"/>: requested locales must be locales of the template (null for all of
    /// them). Returns the plan, or the message key of why there is none.
    /// </summary>
    /// <param name="catalog"></param>
    /// <param name="template"></param>
    /// <param name="appStores"></param>
    /// <param name="locales"></param>
    /// <param name="classKeys"></param>
    /// <param name="targets"></param>
    /// <param name="today"></param>
    /// <returns></returns>
    public static (MockupExportPlanDto Plan, string ErrorKey) PlanForTemplate(ScreenshotSizeCatalog catalog,
                                                                              Models.MockupTemplateSnapshot template,
                                                                              IReadOnlyCollection<StoreKind> appStores,
                                                                              List<string> locales,
                                                                              List<string> classKeys,
                                                                              List<Dtos.StoreJobDtos.MockupExportTarget> targets,
                                                                              DateOnly today)
    {
        var resolved = new List<string>();

        foreach (var locale in locales ?? template.Locales)
        {
            var match = template.Locales.FirstOrDefault(l => string.Equals(l, locale, StringComparison.OrdinalIgnoreCase));

            if (match == null)
                return (null, MessageKey.MockupLocaleInvalid);

            if (!resolved.Contains(match))
                resolved.Add(match);
        }

        var sizes = (targets ?? []).GroupBy(t => t.ClassKey).ToDictionary(g => g.Key, g => new PixelSize(g.Last().Width, g.Last().Height));

        return (Plan(catalog, template.Id, appStores, template.SlotCount, resolved, classKeys, sizes, today), null);
    }

    /// <summary>
    /// Plans an export.
    /// </summary>
    /// <param name="catalog"></param>
    /// <param name="templateId"></param>
    /// <param name="appStores">Stores the app has records in; empty for every store.</param>
    /// <param name="slotCount"></param>
    /// <param name="locales"></param>
    /// <param name="classKeys">Explicit selection; null for the default (required and recommended).</param>
    /// <param name="sizes">Requested sizes by class; a size the class does not accept is refused and the class's export size is used.</param>
    /// <param name="today"></param>
    /// <returns></returns>
    public static MockupExportPlanDto Plan(ScreenshotSizeCatalog catalog,
                                           int templateId,
                                           IReadOnlyCollection<StoreKind> appStores,
                                           int slotCount,
                                           List<string> locales,
                                           IReadOnlyCollection<string> classKeys,
                                           IReadOnlyDictionary<string, PixelSize> sizes,
                                           DateOnly today)
    {
        var classes = catalog.Classes.Where(c => appStores.Count == 0 || appStores.Contains(c.Store)).ToList();
        var defaults = MockupExportRules.DefaultProvided(classes);
        IReadOnlyCollection<string> provided = classKeys ?? defaults;
        var plan = new MockupExportPlanDto
        {
            TemplateId = templateId,
            CatalogVersion = catalog.CatalogVersion,
            Locales = locales,
            Items = [],
            Rejected = [],
            Warnings = [],
        };

        foreach (var screenshotClass in classes.OrderBy(c => c.Store).ThenBy(c => (int)MockupExportRules.PlanRequirement(c, today, provided, defaults)))
        {
            var status = MockupExportRules.PlanRequirement(screenshotClass, today, provided, defaults);
            var size = screenshotClass.ExportSize;

            if (sizes != null && sizes.TryGetValue(screenshotClass.Key, out var requested) && requested != size)
            {
                if (MockupExportRules.Accepts(screenshotClass, requested))
                {
                    size = requested;
                }
                else
                {
                    plan.Rejected.Add(new MockupExportRejectedDto
                    {
                        ClassKey = screenshotClass.Key,
                        Width = requested.Width,
                        Height = requested.Height,
                        Reason = screenshotClass.AcceptedSizes.Count > 0
                            ? $"This class accepts only {string.Join(", ", screenshotClass.AcceptedSizes.Where(s => !s.IsLandscape || screenshotClass.ExportSize.IsLandscape))}."
                            : $"This class accepts sides from {screenshotClass.MinDimension} to {screenshotClass.MaxDimension} pixels.",
                    });
                }
            }

            var perLocale = MockupExportRules.ImagesPerLocale(screenshotClass, slotCount);
            var selected = classKeys != null ? classKeys.Contains(screenshotClass.Key) : status != MockupRequirement.Optional;

            if (selected && !screenshotClass.SingleImage && slotCount > perLocale)
                plan.Warnings.Add(new MockupWarningDto { Code = "TooManySlots", ClassKey = screenshotClass.Key, Message = $"The store accepts {perLocale} images in this class; slots after {perLocale} are left out." });

            plan.Items.Add(new MockupExportPlanItemDto
            {
                ClassKey = screenshotClass.Key,
                Store = screenshotClass.Store,
                Label = screenshotClass.LabelEn,
                LabelTr = screenshotClass.LabelTr,
                Width = size.Width,
                Height = size.Height,
                Frame = screenshotClass.Frame,
                Status = status,
                RequiredFrom = screenshotClass.RequiredFrom,
                Selected = selected,
                PerLocaleCount = perLocale,
                Note = screenshotClass.Note,
            });
        }

        plan.Total = plan.Items.Where(i => i.Selected).Sum(i => i.PerLocaleCount) * locales.Count;

        return plan;
    }
}
