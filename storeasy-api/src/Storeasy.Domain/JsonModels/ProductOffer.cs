using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// A subscription offer as the store has it: App Store introductory, promotional, offer code and win-back offers, Play
/// base plan offers. One-time codes are never stored, only their counts.
/// </summary>
public class ProductOffer
{
    /// <summary>
    /// Kind.
    /// </summary>
    public OfferKind Kind { get; set; }

    /// <summary>
    /// Store's id (App Store resource id; Play offer id).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Reference name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Offer identifier the app uses (promotional offer code, win-back offer id) or the custom code of an offer code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Payment mode.
    /// </summary>
    public OfferMode? Mode { get; set; }

    /// <summary>
    /// Duration of one period (App Store duration text, Play ISO 8601).
    /// </summary>
    public string Duration { get; set; }

    /// <summary>
    /// Number of periods.
    /// </summary>
    public int? Periods { get; set; }

    /// <summary>
    /// Territories the offer is set for.
    /// </summary>
    public List<string> Territories { get; set; }

    /// <summary>
    /// Number of price records.
    /// </summary>
    public int? PriceCount { get; set; }

    /// <summary>
    /// First day.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Last day.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Store state (ACTIVE, DRAFT...) or whether an offer code is active.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Who can redeem it (NEW, EXISTING, EXPIRED; Play: NewCustomers, Upgrade, DeveloperDetermined).
    /// </summary>
    public List<string> Eligibility { get; set; }

    /// <summary>
    /// Play base plan the offer extends.
    /// </summary>
    public string BasePlanId { get; set; }

    /// <summary>
    /// Number of custom codes of an offer code.
    /// </summary>
    public int? CustomCodeCount { get; set; }

    /// <summary>
    /// Number of one-time use codes created for an offer code (the codes themselves are never stored).
    /// </summary>
    public int? OneTimeCodeCount { get; set; }

    /// <summary>
    /// Phases of a Play offer as "kind duration x recurrence".
    /// </summary>
    public List<string> Phases { get; set; }
}
