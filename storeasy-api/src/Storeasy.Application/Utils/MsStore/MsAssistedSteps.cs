using Storeasy.Application.Dtos.MsStoreDtos;

namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// The Microsoft Store tasks without an API (docs/urun-plani.md 11.6): name reservation, the first submission with the
/// IARC age rating questionnaire, Pricing Version 2 prices, add-ons and apps the API answers 409 for, submissions changed
/// in Partner Center, flight groups and ranks, certification reports and review replies. Keys follow the
/// <c>ms.{area}.{task}</c> pattern so the generic assisted step table of phase 2 can store their progress.
/// </summary>
public static class MsAssistedSteps
{
    /// <summary>Reserve the app name (the API cannot create apps).</summary>
    public const string ReserveName = "ms.app.reserve-name";

    /// <summary>First submission in Partner Center, with the IARC age rating questionnaire (catalog rule id).</summary>
    public const string FirstSubmission = "microsoft.api-access.first-submission-in-partner-center";

    /// <summary>Price on Pricing Version 2.</summary>
    public const string Pricing = "ms.pricing.v2";

    /// <summary>Add-ons the API refuses (store-managed consumables, mandatory updates).</summary>
    public const string AddonUpdate = "ms.addon.assisted-update";

    /// <summary>Apps with mandatory updates.</summary>
    public const string MandatoryUpdate = "ms.app.mandatory-update";

    /// <summary>A submission changed in Partner Center.</summary>
    public const string ManualEdit = "ms.submission.manual-edit";

    /// <summary>Flight groups and flight ranks.</summary>
    public const string FlightGroups = "ms.flight.groups";

    /// <summary>Certification report of a failed submission.</summary>
    public const string CertificationReport = "ms.submission.certification-report";

    /// <summary>Review replies (the reply API is documented as not working).</summary>
    public const string Reviews = "ms.reviews.reply";

    /// <summary>
    /// The assisted steps of a product. <paramref name="localize"/> turns a message key into text.
    /// </summary>
    /// <param name="product">The product, with <see cref="MsProductDto.ActiveSubmission"/> filled when known.</param>
    /// <param name="assistedAddons">Whether one of its add-ons is Partner Center only.</param>
    /// <param name="failedCertification">Whether its latest submission failed certification.</param>
    /// <param name="productUrlTemplate">Partner Center product page with a <c>{productId}</c> placeholder.</param>
    /// <param name="localize"></param>
    /// <returns></returns>
    public static List<MsAssistedStepDto> For(MsProductDto product, bool assistedAddons, bool failedCertification, string productUrlTemplate, Func<string, string> localize)
    {
        var url = productUrlTemplate.Replace("{productId}", Uri.EscapeDataString(product.ProductId ?? string.Empty), StringComparison.Ordinal);
        var steps = new List<MsAssistedStepDto>
        {
            Step(ReserveName, MessageKey.MsStepReserveName, null, "https://partner.microsoft.com/dashboard/apps-and-games/overview", false, localize),
        };

        if (product.Kind == MsProductKind.Msix)
        {
            steps.Add(Step(FirstSubmission, MessageKey.MsStepFirstSubmission, null, url, !product.FirstSubmissionDone, localize));
            steps.Add(Step(FlightGroups, MessageKey.MsStepFlightGroups, null, url, false, localize));
        }

        if (product.PricingV2)
            steps.Add(Step(Pricing, MessageKey.MsStepPricing, MsAssistedReason.PricingV2, url, true, localize));

        if (product.HasMandatoryUpdate)
            steps.Add(Step(MandatoryUpdate, MessageKey.MsStepMandatoryUpdate, MsAssistedReason.MandatoryUpdate, url, true, localize));

        if (assistedAddons)
            steps.Add(Step(AddonUpdate, MessageKey.MsStepAddon, MsAssistedReason.StoreManagedConsumable, url, true, localize));

        if (product.ActiveSubmission?.ModifiedOutsideApi == true)
            steps.Add(Step(ManualEdit, MessageKey.MsStepManualEdit, MsAssistedReason.ManualEdit, url, true, localize));

        if (failedCertification)
            steps.Add(Step(CertificationReport, MessageKey.MsStepCertificationReport, null, url, true, localize));

        steps.Add(Step(Reviews, MessageKey.MsStepReviews, null, url, false, localize));

        return steps;
    }

    /// <summary>
    /// The assisted step of an API refusal.
    /// </summary>
    /// <param name="reason"></param>
    /// <returns></returns>
    public static string KeyOf(MsAssistedReason reason) => reason switch
    {
        MsAssistedReason.PricingV2 => Pricing,
        MsAssistedReason.MandatoryUpdate => MandatoryUpdate,
        MsAssistedReason.ManualEdit => ManualEdit,
        _ => AddonUpdate,
    };

    /// <summary>
    /// The assisted step a blocked plan belongs to, or null when the block is not a Partner Center task.
    /// </summary>
    /// <param name="plan"></param>
    /// <returns></returns>
    public static string KeyOf(MsChangePlanDto plan)
    {
        if (plan.AssistedReason.HasValue)
            return KeyOf(plan.AssistedReason.Value);

        return plan.BlockKey == MessageKey.MsFirstSubmissionRequired ? FirstSubmission : null;
    }

    private static MsAssistedStepDto Step(string key, string titleKey, MsAssistedReason? reason, string url, bool pending, Func<string, string> localize) => new()
    {
        Key = key,
        Title = localize(titleKey),
        Reason = reason,
        Url = url,
        Pending = pending,
    };
}
