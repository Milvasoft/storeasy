using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.TestFlight;

/// <summary>
/// Beta app description, feedback e-mail and links of one language (App Store Connect beta app localizations).
/// </summary>
[Table(TableNames.BetaAppLocalizations)]
[Index(nameof(StoreAppId), nameof(Locale), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class BetaAppLocalization : BaseEntity<int>
{
    /// <summary>
    /// Longest beta description App Store Connect accepts.
    /// </summary>
    public const int DescriptionMaxLength = 4000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The store's id of the localization.
    /// </summary>
    [MaxLength(200)]
    public string ExternalId { get; set; }

    /// <summary>
    /// Locale (e.g. <c>en-US</c>).
    /// </summary>
    [MaxLength(20)]
    public string Locale { get; set; }

    /// <summary>
    /// Beta app description.
    /// </summary>
    [MaxLength(DescriptionMaxLength)]
    public string Description { get; set; }

    /// <summary>
    /// Feedback e-mail.
    /// </summary>
    [MaxLength(200)]
    public string FeedbackEmail { get; set; }

    /// <summary>
    /// Marketing URL.
    /// </summary>
    [MaxLength(500)]
    public string MarketingUrl { get; set; }

    /// <summary>
    /// Privacy policy URL.
    /// </summary>
    [MaxLength(500)]
    public string PrivacyPolicyUrl { get; set; }

    /// <summary>
    /// UTC instant of the last sync.
    /// </summary>
    public DateTime SyncedDate { get; set; }
}
