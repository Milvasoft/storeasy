using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaLocalization;

/// <summary>
/// Writes the beta app description, feedback e-mail and links of one language. Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record UpdateBetaLocalizationCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Beta app description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Feedback e-mail.
    /// </summary>
    public string FeedbackEmail { get; set; }

    /// <summary>
    /// Marketing URL.
    /// </summary>
    public string MarketingUrl { get; set; }

    /// <summary>
    /// Privacy policy URL.
    /// </summary>
    public string PrivacyPolicyUrl { get; set; }
}
