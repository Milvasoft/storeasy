using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TestFlight.UpdateWhatToTest;

/// <summary>
/// Writes the "what to test" text of a build in one language (at most 4,000 characters). Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record UpdateWhatToTestCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Build.
    /// </summary>
    public long BuildId { get; set; }

    /// <summary>
    /// Locale (e.g. <c>en-US</c>).
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Text.
    /// </summary>
    public string WhatsNew { get; set; }
}
