namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Price and availability. MSIX: base price tier, trial and visibility. MSI/EXE: markets, discoverability, pricing model and free trial.
/// </summary>
public class MsPricingDto
{
    /// <summary>
    /// MSIX: base price tier (Free, Tier2...). Unknown on Pricing Version 2.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// MSIX: trial (NoFreeTrial, OneDay, SevenDays, FifteenDays, ThirtyDays, TrialNeverExpires).
    /// </summary>
    public string TrialPeriod { get; set; }

    /// <summary>
    /// MSIX: Public, Private, Hidden.
    /// </summary>
    public string Visibility { get; set; }

    /// <summary>
    /// MSI/EXE: market codes (US, TR...).
    /// </summary>
    public List<string> Markets { get; set; }

    /// <summary>
    /// MSI/EXE: DISCOVERABLE or DEEPLINK_ONLY.
    /// </summary>
    public string Discoverability { get; set; }

    /// <summary>
    /// MSI/EXE: FREE, FREEMIUM, SUBSCRIPTION or PAID.
    /// </summary>
    public string PricingModel { get; set; }

    /// <summary>
    /// MSI/EXE: NO_FREE_TRIAL or FREE_TRIAL.
    /// </summary>
    public string FreeTrial { get; set; }
}
