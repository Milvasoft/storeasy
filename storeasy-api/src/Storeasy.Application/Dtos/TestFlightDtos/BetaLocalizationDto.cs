using Storeasy.Domain.TestFlight;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.TestFlightDtos;

/// <summary>
/// Beta app description and feedback e-mail of one language.
/// </summary>
public class BetaLocalizationDto
{
    /// <summary>Localization id.</summary>
    public int Id { get; set; }

    /// <summary>Locale.</summary>
    public string Locale { get; set; }

    /// <summary>Beta app description.</summary>
    public string Description { get; set; }

    /// <summary>Feedback e-mail.</summary>
    public string FeedbackEmail { get; set; }

    /// <summary>Marketing URL.</summary>
    public string MarketingUrl { get; set; }

    /// <summary>Privacy policy URL.</summary>
    public string PrivacyPolicyUrl { get; set; }

    /// <summary>UTC instant of the last sync.</summary>
    public DateTime SyncedDate { get; set; }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<BetaAppLocalization, BetaLocalizationDto>> Projection { get; } = l => new BetaLocalizationDto
    {
        Id = l.Id,
        Locale = l.Locale,
        Description = l.Description,
        FeedbackEmail = l.FeedbackEmail,
        MarketingUrl = l.MarketingUrl,
        PrivacyPolicyUrl = l.PrivacyPolicyUrl,
        SyncedDate = l.SyncedDate,
    };
}
